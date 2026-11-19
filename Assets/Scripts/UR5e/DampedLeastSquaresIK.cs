using System;
using UnityEngine;

/// <summary>Parametri del solver IK.</summary>
[Serializable]
public class IKSettings
{
    [Tooltip("Iterazioni massime prima di dichiarare fallimento.")]
    public int maxIterations = 100;

    [Tooltip("Errore di posizione accettato (unità Unity, di norma metri). 0.0005 = 0.5 mm.")]
    public float positionTolerance = 0.0005f;

    [Tooltip("Errore di orientamento accettato (gradi).")]
    public float rotationToleranceDeg = 0.1f;

    [Tooltip("Fattore di smorzamento λ. Più alto = più stabile vicino alle singolarità, ma più lento.")]
    public float damping = 0.05f;

    [Tooltip("Massima variazione di un singolo giunto per iterazione (gradi).")]
    public float maxStepDeg = 10f;

    [Tooltip("Passo usato per calcolare il Jacobiano per differenze finite (gradi).")]
    public float finiteDifferenceDeg = 0.1f;

    [Range(0f, 1f)]
    [Tooltip("Peso dell'orientamento. 1 = posizione+orientamento, 0 = solo posizione.")]
    public float orientationWeight = 1f;
}

public struct IKResult
{
    public bool success;
    public int iterations;
    public float positionError;      // unità Unity
    public float rotationErrorDeg;   // gradi
    public float[] angles;           // miglior soluzione trovata (gradi)
}

/// <summary>
/// Cinematica inversa numerica con Jacobiano e Damped Least Squares:
///     Δθ = Jᵀ (J Jᵀ + λ² I)⁻¹ e
/// Parte sempre da una configurazione iniziale e rimane nella soluzione "vicina",
/// proprio come fa il controller del robot reale.
/// </summary>
public static class DampedLeastSquaresIK
{
    const int N = UR5eRobotModel.JointCount; // giunti
    const int M = 6;                          // dimensioni del task (3 pos + 3 rot)

    // Buffer statici per evitare allocazioni a ogni chiamata (uso solo dal main thread).
    static readonly double[,] J = new double[M, N];
    static readonly double[,] A = new double[M, M];
    static readonly double[] err = new double[M];
    static readonly double[] x = new double[M];
    static readonly double[] dTheta = new double[N];
    static readonly float[] qTmp = new float[N];

    public static IKResult Solve(UR5eRobotModel robot, Vector3 targetPos, Quaternion targetRot,
                                 float[] startAnglesDeg, IKSettings settings)
    {
        float[] q = (float[])startAnglesDeg.Clone();
        float lambda2 = settings.damping * settings.damping;
        float w = settings.orientationWeight;
        float deltaDeg = Mathf.Max(0.001f, settings.finiteDifferenceDeg);
        float deltaRad = deltaDeg * Mathf.Deg2Rad;

        IKResult result = new IKResult { angles = q };

        for (int it = 0; ; it++)
        {
            // --- 1) errore attuale ---
            robot.ForwardKinematics(q, out Vector3 pos, out Quaternion rot);

            Vector3 dp = targetPos - pos;
            Vector3 dr = RotationVector(targetRot * Quaternion.Inverse(rot)); // vettore di rotazione (rad, frame mondo)

            float posErr = dp.magnitude;
            float rotErrDeg = dr.magnitude * Mathf.Rad2Deg;

            result.iterations = it;
            result.positionError = posErr;
            result.rotationErrorDeg = rotErrDeg;

            bool posOk = posErr <= settings.positionTolerance;
            bool rotOk = w <= 0f || rotErrDeg <= settings.rotationToleranceDeg;
            if (posOk && rotOk) { result.success = true; break; }
            if (it >= settings.maxIterations) break;

            err[0] = dp.x; err[1] = dp.y; err[2] = dp.z;
            err[3] = dr.x * w; err[4] = dr.y * w; err[5] = dr.z * w;

            // --- 2) Jacobiano per differenze finite ---
            for (int j = 0; j < N; j++)
            {
                Array.Copy(q, qTmp, N);
                qTmp[j] += deltaDeg;
                robot.ForwardKinematics(qTmp, out Vector3 pos2, out Quaternion rot2);

                Vector3 lin = (pos2 - pos) / deltaRad;
                Vector3 ang = RotationVector(rot2 * Quaternion.Inverse(rot)) * (w / deltaRad);

                J[0, j] = lin.x; J[1, j] = lin.y; J[2, j] = lin.z;
                J[3, j] = ang.x; J[4, j] = ang.y; J[5, j] = ang.z;
            }

            // --- 3) A = J Jᵀ + λ² I ---
            for (int r = 0; r < M; r++)
            {
                for (int c = 0; c < M; c++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < N; k++) sum += J[r, k] * J[c, k];
                    A[r, c] = sum;
                }
                A[r, r] += lambda2;
            }

            // --- 4) risolvi A x = e ---
            if (!SolveLinearSystem(A, err, x, M)) break;

            // --- 5) Δθ = Jᵀ x (in rad) -> gradi, con limitazione del passo ---
            double maxAbsDeg = 0.0;
            for (int j = 0; j < N; j++)
            {
                double s = 0.0;
                for (int r = 0; r < M; r++) s += J[r, j] * x[r];
                dTheta[j] = s * Mathf.Rad2Deg;
                maxAbsDeg = Math.Max(maxAbsDeg, Math.Abs(dTheta[j]));
            }

            double scale = maxAbsDeg > settings.maxStepDeg ? settings.maxStepDeg / maxAbsDeg : 1.0;

            for (int j = 0; j < N; j++)
            {
                q[j] += (float)(dTheta[j] * scale);
                q[j] = Mathf.Clamp(q[j], robot.minAnglesDeg[j], robot.maxAnglesDeg[j]);
            }
        }

        return result;
    }

    // ------------------------------------------------------------------
    // Utilità
    // ------------------------------------------------------------------

    /// <summary>Converte un quaternione nel vettore di rotazione (asse * angolo in rad), via più breve.</summary>
    static Vector3 RotationVector(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);

        q.ToAngleAxis(out float angleDeg, out Vector3 axis);

        if (float.IsNaN(angleDeg) || float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z) ||
            float.IsInfinity(axis.x) || float.IsInfinity(axis.y) || float.IsInfinity(axis.z))
            return Vector3.zero;

        return axis * (angleDeg * Mathf.Deg2Rad);
    }

    /// <summary>Eliminazione di Gauss con pivoting parziale. Modifica A e b.</summary>
    static bool SolveLinearSystem(double[,] a, double[] b, double[] result, int n)
    {
        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            double max = Math.Abs(a[col, col]);
            for (int r = col + 1; r < n; r++)
            {
                double v = Math.Abs(a[r, col]);
                if (v > max) { max = v; pivot = r; }
            }
            if (max < 1e-12) return false;

            if (pivot != col)
            {
                for (int c = 0; c < n; c++)
                {
                    double tmp = a[col, c]; a[col, c] = a[pivot, c]; a[pivot, c] = tmp;
                }
                double tb = b[col]; b[col] = b[pivot]; b[pivot] = tb;
            }

            for (int r = col + 1; r < n; r++)
            {
                double f = a[r, col] / a[col, col];
                for (int c = col; c < n; c++) a[r, c] -= f * a[col, c];
                b[r] -= f * b[col];
            }
        }

        for (int r = n - 1; r >= 0; r--)
        {
            double sum = b[r];
            for (int c = r + 1; c < n; c++) sum -= a[r, c] * result[c];
            result[r] = sum / a[r, r];
        }
        return true;
    }
}
