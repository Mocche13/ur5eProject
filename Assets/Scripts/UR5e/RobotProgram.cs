using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RobotMoveType
{
    MoveJ,       // verso la posa di un Transform, interpolando i giunti
    MoveL,       // verso la posa di un Transform, in linea retta
    MoveJoints   // verso angoli di giunto precisi
}

[System.Serializable]
public class RobotWaypoint
{
    public string name = "Waypoint";
    public RobotMoveType type = RobotMoveType.MoveJ;

    [Tooltip("Transform che definisce posizione e orientamento desiderati del TCP (MoveJ / MoveL).")]
    public Transform target;

    [Tooltip("Solo per MoveJoints: i 6 angoli in gradi (relativi alla posa iniziale).")]
    public float[] jointAnglesDeg = new float[UR5eRobotModel.JointCount];

    [Tooltip("<= 0 usa la velocità di default. MoveJ/MoveJoints: °/s. MoveL: unità/s.")]
    public float speed = 0f;

    [Tooltip("Pausa dopo aver raggiunto il waypoint (s).")]
    public float pauseAfterSeconds = 0.2f;
}

/// <summary>
/// Pilota il robot in due modi:
///  1) "Live target": trascini un oggetto in scena e premi J (MoveJ) o L (MoveL).
///  2) Programma: una lista di waypoint (MoveJ / MoveL / MoveJoints) eseguita in sequenza con Spazio.
/// I metodi pubblici si possono collegare anche ai pulsanti della UI.
/// </summary>
public class RobotProgram : MonoBehaviour
{
    [Header("Riferimenti")]
    public RobotMotionController motion;
    [Tooltip("Oggetto che trascini in scena per comandare il TCP.")]
    public Transform liveTarget;

    [Header("Programma")]
    public List<RobotWaypoint> waypoints = new List<RobotWaypoint>();
    public bool loop = false;

    [Header("Posa di home (angoli relativi alla posa iniziale)")]
    [Tooltip("Meglio partire da una posa con il braccio piegato: la posa tutta stesa è una singolarità.")]
    public bool applyHomeOnStart = false;
    public float[] homeAnglesDeg = new float[UR5eRobotModel.JointCount];

    [Header("Tasti (Input Manager classico)")]
    public KeyCode moveJKey = KeyCode.J;
    public KeyCode moveLKey = KeyCode.L;
    public KeyCode runProgramKey = KeyCode.Space;
    public KeyCode homeKey = KeyCode.H;
    public KeyCode stopKey = KeyCode.X;

    bool running;
    public bool IsRunning => running;

    void Awake()
    {
        if (motion == null) motion = GetComponent<RobotMotionController>();
    }

    void Start()
    {
        if (applyHomeOnStart && motion != null && motion.robot != null)
            motion.robot.SetJointAngles(homeAnglesDeg);
    }

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
    void Update()
    {
        if (Input.anyKeyDown) Debug.Log("[RobotProgram] Tasto premuto: '" + Input.inputString + "'");

        if (Input.GetKeyDown(moveJKey)) { Debug.Log("[RobotProgram] moveJKey riconosciuto"); MoveJToLiveTarget(); }
        if (Input.GetKeyDown(moveLKey)) MoveLToLiveTarget();
        if (Input.GetKeyDown(runProgramKey)) RunProgram();
        if (Input.GetKeyDown(homeKey)) GoHome();
        if (Input.GetKeyDown(stopKey)) StopAll();
    }
#endif

    // ------------------------------------------------------------------
    // API pubblica (utilizzabile dai pulsanti UI)
    // ------------------------------------------------------------------

    public void MoveJToLiveTarget()
    {
        if (!CanStartSingleMove(true)) return;
        StartCoroutine(motion.MoveJ(liveTarget.position, liveTarget.rotation));
    }

    public void MoveLToLiveTarget()
    {
        if (!CanStartSingleMove(true)) return;
        StartCoroutine(motion.MoveL(liveTarget.position, liveTarget.rotation));
    }

    public void GoHome()
    {
        if (!CanStartSingleMove(false)) return;
        StartCoroutine(motion.MoveJToAngles(homeAnglesDeg));
    }

    public void RunProgram()
    {
        if (motion == null) { Debug.LogError("[RobotProgram] RobotMotionController mancante.", this); return; }
        if (running) return;
        if (waypoints == null || waypoints.Count == 0) { Debug.LogWarning("[RobotProgram] Nessun waypoint.", this); return; }
        StartCoroutine(ProgramRoutine());
    }

    public void StopAll()
    {
        StopAllCoroutines();
        running = false;
        if (motion != null) motion.Stop();
    }

    // ------------------------------------------------------------------
    // Interno
    // ------------------------------------------------------------------

    bool CanStartSingleMove(bool needsLiveTarget)
    {
        if (motion == null) { Debug.LogError("[RobotProgram] RobotMotionController mancante.", this); return false; }
        if (running) { Debug.LogWarning("[RobotProgram] Programma in esecuzione: premi lo stop prima.", this); return false; }
        if (needsLiveTarget && liveTarget == null) { Debug.LogWarning("[RobotProgram] Live target non assegnato.", this); return false; }
        return true;
    }

    IEnumerator ProgramRoutine()
    {
        running = true;

        do
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                RobotWaypoint wp = waypoints[i];
                bool ok = true;

                switch (wp.type)
                {
                    case RobotMoveType.MoveJ:
                        if (wp.target == null) { ok = false; break; }
                        yield return motion.MoveJ(wp.target.position, wp.target.rotation, wp.speed);
                        ok = motion.LastMoveSucceeded;
                        break;

                    case RobotMoveType.MoveL:
                        if (wp.target == null) { ok = false; break; }
                        yield return motion.MoveL(wp.target.position, wp.target.rotation, wp.speed);
                        ok = motion.LastMoveSucceeded;
                        break;

                    case RobotMoveType.MoveJoints:
                        yield return motion.MoveJToAngles(wp.jointAnglesDeg, wp.speed);
                        ok = motion.LastMoveSucceeded;
                        break;
                }

                if (!ok)
                {
                    Debug.LogWarning("[RobotProgram] Programma interrotto al waypoint " + i + " (" + wp.name + ").", this);
                    running = false;
                    yield break;
                }

                if (wp.pauseAfterSeconds > 0f) yield return new WaitForSeconds(wp.pauseAfterSeconds);
                yield return null; // evita blocchi se tutti i movimenti sono di lunghezza zero
            }
        }
        while (loop);

        running = false;
    }

    // ------------------------------------------------------------------
    // Strumenti editor
    // ------------------------------------------------------------------

    /// <summary>Porta il live target esattamente sulla posa attuale del TCP (poi lo sposti da lì).</summary>
    [ContextMenu("Allinea Live Target al TCP")]
    public void SnapLiveTargetToTcp()
    {
        if (liveTarget == null || motion == null || motion.robot == null || motion.robot.tcp == null)
        {
            Debug.LogWarning("[RobotProgram] Servono liveTarget e TCP assegnati.", this);
            return;
        }
        liveTarget.SetPositionAndRotation(motion.robot.tcp.position, motion.robot.tcp.rotation);
    }

    void OnDrawGizmos()
    {
        // percorso dei waypoint
        if (waypoints != null)
        {
            Vector3? prev = null;
            for (int i = 0; i < waypoints.Count; i++)
            {
                RobotWaypoint wp = waypoints[i];
                if (wp == null || wp.target == null) continue;

                Gizmos.color = wp.type == RobotMoveType.MoveL ? Color.cyan : Color.yellow;
                Gizmos.DrawWireSphere(wp.target.position, 0.015f);
                if (prev.HasValue) Gizmos.DrawLine(prev.Value, wp.target.position);
                prev = wp.target.position;
            }
        }

        // live target: assi e linea dal TCP
        if (liveTarget != null)
        {
            Vector3 p = liveTarget.position;
            float L = 0.08f;
            Gizmos.color = Color.red;   Gizmos.DrawLine(p, p + liveTarget.right * L);
            Gizmos.color = Color.green; Gizmos.DrawLine(p, p + liveTarget.up * L);
            Gizmos.color = Color.blue;  Gizmos.DrawLine(p, p + liveTarget.forward * L);

            if (motion != null && motion.robot != null && motion.robot.tcp != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(motion.robot.tcp.position, p);
            }
        }
    }
}
