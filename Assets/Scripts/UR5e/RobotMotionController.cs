using System.Collections;
using UnityEngine;

/// <summary>
/// Esegue il movimento MoveJ del robot UR5e (interpolazione nello spazio dei giunti
/// con profilo di velocità trapezoidale, tutti i giunti partono e arrivano insieme).
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

    [Header("Cinematica inversa")]
    [Tooltip("Impostazioni per la soluzione finale di MoveJ.")]
    public IKSettings ikSettings = new IKSettings();

    public bool IsMoving { get; private set; }
    public bool LastMoveSucceeded { get; private set; } = true;

    private int runId; // serve a interrompere un movimento quando ne parte un altro o si chiama Stop()

    private void Awake()
    {
        if (robot == null) robot = GetComponent<UR5eRobotModel>();
    }

    private bool Ready()
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

    private void Finish(int id, bool success)
    {
        if (id != runId) return;
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
}