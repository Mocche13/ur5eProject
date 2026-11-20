using System.Collections;
using UnityEngine;

public class RobotProgram : MonoBehaviour
{
    [Header("Riferimenti")]
    public RobotMotionController motion;
    [Tooltip("Oggetto che definisce la posa di presa (target) del TCP.")]
    public Transform liveTarget;
    [Tooltip("Trascina qui le due ganasce che hanno lo script Clamping.")]
    public Clamping[] grippers;

    [Header("Posa di Home (angoli in gradi)")]
    [Tooltip("Meglio partire da una posa con il braccio piegato: la posa tutta stesa è una singolarità.")]
    public bool applyHomeOnStart = false;
    public float[] homeAnglesDeg = new float[UR5eRobotModel.JointCount];

    [Header("Posa Intermedia (tra Home e Pick, angoli in gradi)")]
    [Tooltip("Attiva il passaggio da questa posa prima di andare sul target di presa.")]
    public bool useIntermediatePose = true;
    public float[] intermediateAnglesDeg = new float[UR5eRobotModel.JointCount];

    private bool running;
    public bool IsRunning => running;

    private void Awake()
    {
        if (motion == null) motion = GetComponent<RobotMotionController>();
    }

    private void Start()
    {
        if (applyHomeOnStart && motion != null && motion.robot != null)
            motion.robot.SetJointAngles(homeAnglesDeg);
    }

    // ------------------------------------------------------------------
    // SEQUENZA AUTOMATICA COMPLETA (Collega questo metodo al bottone UI)
    // ------------------------------------------------------------------

    /// <summary>Esegue in sequenza: Intermedia -> MoveJ al target -> Grab -> Home -> Release.</summary>
    public void RunPickAndPlace()
    {
        if (motion == null)
        {
            Debug.LogError("[RobotProgram] RobotMotionController mancante.", this);
            return;
        }
        if (running)
        {
            Debug.LogWarning("[RobotProgram] Ciclo Pick & Place già in esecuzione.", this);
            return;
        }
        if (liveTarget == null)
        {
            Debug.LogWarning("[RobotProgram] Live target non assegnato.", this);
            return;
        }

        StartCoroutine(PickAndPlaceRoutine());
    }

    private IEnumerator PickAndPlaceRoutine()
    {
        running = true;

        // 1. Posizione intermedia (avvicinamento ad angoli precisi)
        if (useIntermediatePose && intermediateAnglesDeg != null && intermediateAnglesDeg.Length == UR5eRobotModel.JointCount)
        {
            yield return motion.MoveJToAngles(intermediateAnglesDeg);
            if (!motion.LastMoveSucceeded)
            {
                Debug.LogWarning("[RobotProgram] Sequenza interrotta durante il passaggio alla posa intermedia.");
                running = false;
                yield break;
            }
        }

        // 2. Movimento verso il target (MoveJ)
        yield return motion.MoveJ(liveTarget.position, liveTarget.rotation);
        if (!motion.LastMoveSucceeded)
        {
            Debug.LogWarning("[RobotProgram] Sequenza interrotta: target irraggiungibile.");
            running = false;
            yield break;
        }

        // 3. Chiusura pinza (Grab) e attesa del completamento
        if (grippers != null && grippers.Length > 0)
        {
            foreach (var g in grippers)
            {
                if (g != null) g.CloseGripper();
            }

            yield return new WaitForFixedUpdate();
            while (AreGrippersMoving())
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.2f); // breve pausa di stabilizzazione

        // 4. Ritorno in posizione Home
        yield return motion.MoveJToAngles(homeAnglesDeg);
        if (!motion.LastMoveSucceeded)
        {
            Debug.LogWarning("[RobotProgram] Sequenza interrotta durante il ritorno a Home.");
            running = false;
            yield break;
        }

        // 5. Apertura pinza e rilascio oggetto (Release)
        if (grippers != null && grippers.Length > 0)
        {
            foreach (var g in grippers)
            {
                if (g != null) g.OpenGripper();
            }

            yield return new WaitForFixedUpdate();
            while (AreGrippersMoving())
            {
                yield return null;
            }
        }

        running = false;
    }

    private bool AreGrippersMoving()
    {
        if (grippers == null) return false;
        for (int i = 0; i < grippers.Length; i++)
        {
            if (grippers[i] != null && grippers[i].IsMoving)
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Strumenti editor
    // ------------------------------------------------------------------

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

    private void OnDrawGizmos()
    {
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