using System.Collections;
using UnityEngine;

/// <summary>
/// Esegue i movimenti del robot come il controller UR reale:
///  - MoveJ : interpolazione nello spazio dei giunti (tutti i giunti partono e arrivano insieme)
///  - MoveL : il TCP segue una linea retta (la traiettoria viene pianificata PRIMA con l'IK;
///            se in un punto non c'è soluzione o c'è una singolarità, il robot non parte)
/// Entrambi usano un profilo di velocità trapezoidale.
///
/// I movimenti sono coroutine: puoi usarli con StartCoroutine(...) oppure concatenarli
/// con "yield return motion.MoveJ(...)" dentro un'altra coroutine.
/// </summary>
[RequireComponent(typeof(UR5eRobotModel))]
public class RobotMotionController : MonoBehaviour
{
    public UR5eRobotModel robot;

    [Header("MoveJ (spazio dei giunti)")]
    [Tooltip("Velocità del giunto più lento da spostare (°/s). UR5e: max 180°/s.")]
    public float jointSpeedDeg = 60f;
    [Tooltip("Accelerazione (°/s²).")]
    public float jointAccelDeg = 120f;

    [Header("MoveL (linea retta del TCP)")]
    [Tooltip("Velocità lineare del TCP (unità/s, di norma m/s).")]
    public float linearSpeed = 0.25f;
    public float linearAccel = 0.8f;
    [Tooltip("Velocità angolare massima del TCP (°/s) quando cambia anche l'orientamento.")]
    public float angularSpeedDeg = 90f;
    public float angularAccelDeg = 180f;

    [Header("Pianificazione MoveL")]
    [Tooltip("Distanza tra due campioni della retta (unità).")]
    public float pathSampleDistance = 0.005f;
    [Tooltip("Rotazione massima tra due campioni (°).")]
    public float pathSampleAngleDeg = 2f;
    [Tooltip("Se tra due campioni un giunto salta più di questo valore -> singolarità o cambio di configurazione, MoveL annullato.")]
    public float maxJointJumpDeg = 10f;

    [Header("Cinematica inversa")]
    [Tooltip("Impostazioni per la soluzione finale di MoveJ.")]
    public IKSettings ikSettings = new IKSettings();
    [Tooltip("Impostazioni per ogni campione di MoveL (parte dal campione precedente, bastano poche iterazioni).")]
    public IKSettings ikSettingsPath = new IKSettings
    {
        maxIterations = 30,
        positionTolerance = 0.0002f,
        rotationToleranceDeg = 0.05f
    };

    public bool IsMoving { get; private set; }
    public bool LastMoveSucceeded { get; private set; } = true;

    int runId; // serve a interrompere un movimento quando ne parte un altro o si chiama Stop()

    void Awake()
    {
        if (robot == null) robot = GetComponent<UR5eRobotModel>();
    }

    bool Ready()
    {
        if (robot != null && robot.Initialize()) return true;
        Debug.LogError("[RobotMotionController] UR5eRobotModel mancante o non configurato.", this);
        return false;
    }

    /// <summary>Interrompe il movimento in corso (il robot si ferma dov'è).</summary>
    public void Stop()
    {
        runId++;
        IsMoving = false;
    }

    void Finish(int id, bool success)
    {
        if (id != runId) return; // il movimento è stato sostituito/interrotto
        LastMoveSucceeded = success;
        IsMoving = false;
    }

    /// <summary>Prova a risolvere l'IK senza muovere il robot (utile per controllare la raggiungibilità).</summary>
    public bool TrySolveIK(Vector3 worldPos, Quaternion worldRot, out float[] anglesDeg)
    {
        anglesDeg = null;
        if (!Ready()) return false;
        IKResult r = DampedLeastSquaresIK.Solve(robot, worldPos, worldRot, robot.GetAngles(), ikSettings);
        anglesDeg = r.angles;
        return r.success;
    }

    // ------------------------------------------------------------------
    // MoveJ
    // ------------------------------------------------------------------

    /// <summary>MoveJ verso una posa del TCP (posizione e rotazione in coordinate mondo).</summary>
    public IEnumerator MoveJ(Vector3 worldPos, Quaternion worldRot, float speedDeg = -1f)
    {
        if (!Ready()) { LastMoveSucceeded = false; yield break; }

        IKResult r = DampedLeastSquaresIK.Solve(robot, worldPos, worldRot, robot.GetAngles(), ikSettings);
        if (!r.success)
        {
            Debug.LogWarning("[RobotMotionController] MoveJ annullato: nessuna soluzione IK (target fuori portata o irraggiungibile). " +
                             "Errore residuo: " + (r.positionError * 1000f).ToString("F1") + " mm, " +
                             r.rotationErrorDeg.ToString("F1") + "°");
            LastMoveSucceeded = false;
            yield break;
        }

        yield return MoveJToAngles(r.angles, speedDeg);
    }

    /// <summary>MoveJ verso angoli di giunto precisi (gradi).</summary>
    public IEnumerator MoveJToAngles(float[] targetAnglesDeg, float speedDeg = -1f)
    {
        if (!Ready()) { LastMoveSucceeded = false; yield break; }

        int myId = ++runId;
        IsMoving = true;
        LastMoveSucceeded = false;

        int n = UR5eRobotModel.JointCount;
        float[] start = robot.GetAngles();
        float[] goal = robot.ClampToLimits(targetAnglesDeg);
        float[] cur = new float[n];

        // Il giunto che deve fare più strada comanda la durata; gli altri si sincronizzano.
        float maxDelta = 0f;
        for (int i = 0; i < n; i++) maxDelta = Mathf.Max(maxDelta, Mathf.Abs(goal[i] - start[i]));

        float v = speedDeg > 0f ? speedDeg : jointSpeedDeg;
        TrapezoidalProfile profile = new TrapezoidalProfile(maxDelta, v, jointAccelDeg);

        YieldInstruction wait = robot.useArticulationBodies ? new WaitForFixedUpdate() : null;

        float t = 0f;
        while (t < profile.Duration)
        {
            if (myId != runId) yield break;

            t += Time.deltaTime;
            float s = profile.EvaluateNormalized(t);
            for (int i = 0; i < n; i++) cur[i] = Mathf.LerpUnclamped(start[i], goal[i], s);
            robot.SetJointAngles(cur, false);

            yield return wait;
        }

        if (myId != runId) yield break;
        robot.SetJointAngles(goal, false);
        Finish(myId, true);
    }

    // ------------------------------------------------------------------
    // MoveL
    // ------------------------------------------------------------------

    /// <summary>MoveL: il TCP va in linea retta verso la posa target (coordinate mondo).</summary>
    public IEnumerator MoveL(Vector3 worldPos, Quaternion worldRot, float speed = -1f)
    {
        if (!Ready()) { LastMoveSucceeded = false; yield break; }

        int myId = ++runId;
        IsMoving = true;
        LastMoveSucceeded = false;

        float[] start = robot.GetAngles();
        robot.ForwardKinematics(start, out Vector3 p0, out Quaternion q0);

        float linDist = Vector3.Distance(p0, worldPos);
        float angDist = Quaternion.Angle(q0, worldRot);

        // ---- 1) Pianificazione: campiono la retta e risolvo l'IK a ogni campione ----
        float sampleDist = Mathf.Max(1e-4f, pathSampleDistance);
        float sampleAng = Mathf.Max(0.01f, pathSampleAngleDeg);
        int steps = Mathf.Max(10, Mathf.CeilToInt(Mathf.Max(linDist / sampleDist, angDist / sampleAng)));

        float[][] samples = new float[steps + 1][];
        samples[0] = start;
        float[] prev = start;

        for (int k = 1; k <= steps; k++)
        {
            float s = (float)k / steps;
            Vector3 p = Vector3.Lerp(p0, worldPos, s);
            Quaternion q = Quaternion.Slerp(q0, worldRot, s);

            IKResult r = DampedLeastSquaresIK.Solve(robot, p, q, prev, ikSettingsPath);
            if (!r.success)
            {
                Debug.LogWarning("[RobotMotionController] MoveL annullato: nessuna soluzione IK a " +
                                 (s * 100f).ToString("F0") + "% del percorso (fuori portata, limite di giunto o singolarità).");
                Finish(myId, false);
                yield break;
            }

            float jump = MaxAbsDiff(r.angles, prev);
            if (jump > maxJointJumpDeg)
            {
                Debug.LogWarning("[RobotMotionController] MoveL annullato: salto di " + jump.ToString("F1") +
                                 "° in un giunto a " + (s * 100f).ToString("F0") +
                                 "% del percorso (singolarità o cambio di configurazione). Prova MoveJ o un target diverso.");
                Finish(myId, false);
                yield break;
            }

            samples[k] = r.angles;
            prev = r.angles;
        }

        // ---- 2) Esecuzione: profilo trapezoidale sull'avanzamento lungo il percorso ----
        TrapezoidalProfile linProfile = new TrapezoidalProfile(linDist, speed > 0f ? speed : linearSpeed, linearAccel);
        TrapezoidalProfile angProfile = new TrapezoidalProfile(angDist, angularSpeedDeg, angularAccelDeg);
        TrapezoidalProfile profile = linProfile.Duration >= angProfile.Duration ? linProfile : angProfile;

        YieldInstruction wait = robot.useArticulationBodies ? new WaitForFixedUpdate() : null;
        float[] cur = new float[UR5eRobotModel.JointCount];

        float t = 0f;
        while (t < profile.Duration)
        {
            if (myId != runId) yield break;

            t += Time.deltaTime;
            float f = profile.EvaluateNormalized(t) * steps;
            int i0 = Mathf.Clamp((int)f, 0, steps - 1);
            float frac = f - i0;

            for (int j = 0; j < cur.Length; j++)
                cur[j] = Mathf.Lerp(samples[i0][j], samples[i0 + 1][j], frac);

            robot.SetJointAngles(cur, false);
            yield return wait;
        }

        if (myId != runId) yield break;
        robot.SetJointAngles(samples[steps], false);
        Finish(myId, true);
    }

    static float MaxAbsDiff(float[] a, float[] b)
    {
        float m = 0f;
        for (int i = 0; i < a.Length; i++) m = Mathf.Max(m, Mathf.Abs(a[i] - b[i]));
        return m;
    }
}
