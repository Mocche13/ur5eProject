using UnityEngine;

[DisallowMultipleComponent]
public class UR5eRobotModel : MonoBehaviour
{
    public const int JointCount = 6;

    [Header("Gerarchia del modello")]
    public Transform baseFrame;
    public Transform[] joints = new Transform[JointCount];
    public Transform tcp;

    [Header("Assi di rotazione (usati SOLO se useArticulationBodies = false)")]
    public Vector3[] localAxes = new Vector3[JointCount]
    {
        Vector3.up, Vector3.right, Vector3.right, Vector3.right, Vector3.up, Vector3.right
    };

    [Header("Limiti dei giunti (gradi, relativi alla posa iniziale)")]
    public float[] minAnglesDeg = new float[JointCount] { -360f, -360f, -360f, -360f, -360f, -360f };
    public float[] maxAnglesDeg = new float[JointCount] { 360f, 360f, 360f, 360f, 360f, 360f };

    [Header("Configurazione URDF / ArticulationBody")]
    public bool useArticulationBodies = true;
    public ArticulationBody[] articulationBodies = new ArticulationBody[JointCount];
    public float[] articulationSigns = new float[JointCount] { 1f, 1f, 1f, 1f, 1f, 1f };

    [Header("Fisica e Motori URDF")]
    public bool ignoreSelfCollisions = true;
    public bool autoConfigureDrives = true;
    public float driveStiffness = 20000f;
    public float driveDamping = 1000f;
    public float driveForceLimit = 2000f;

    [Header("Test manuale (solo Play Mode)")]
    public bool manualMode = false;
    public float[] manualAnglesDeg = new float[JointCount];

    // ---- stato interno ----
    Matrix4x4[] relative = new Matrix4x4[JointCount];
    Matrix4x4 relativeTcp;
    Quaternion[] initialLocalRot = new Quaternion[JointCount];
    Vector3[] axes = new Vector3[JointCount];
    float[] angles = new float[JointCount];
    float[] initialDriveTargets = new float[JointCount];
    bool initialized;

    public bool IsReady => initialized;

    void Awake()
    {
        Initialize();
    }

    void Update()
    {
        if (manualMode && initialized && manualAnglesDeg != null && manualAnglesDeg.Length == JointCount)
            SetJointAngles(manualAnglesDeg, true);
    }

    public bool Initialize()
    {
        if (initialized) return true;
        if (!Validate()) return false;

        if (useArticulationBodies)
        {
            if (baseFrame != null)
            {
                var baseAb = baseFrame.GetComponent<ArticulationBody>();
                if (baseAb != null && baseAb.isRoot) baseAb.immovable = true;
            }

            if (ignoreSelfCollisions)
            {
                Transform rootT = baseFrame != null ? baseFrame : transform;
                Collider[] cols = rootT.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                    for (int j = i + 1; j < cols.Length; j++)
                        Physics.IgnoreCollision(cols[i], cols[j], true);
            }
        }

        // Usa esattamente le matrici Transform come nel modello originale
        Transform baseT = GetBaseTransform();
        Matrix4x4 baseWorldToLocal = baseT != null ? baseT.worldToLocalMatrix : Matrix4x4.identity;

        relative[0] = baseWorldToLocal * joints[0].localToWorldMatrix;
        for (int i = 1; i < JointCount; i++)
            relative[i] = joints[i - 1].worldToLocalMatrix * joints[i].localToWorldMatrix;

        relativeTcp = joints[JointCount - 1].worldToLocalMatrix * tcp.localToWorldMatrix;

        for (int i = 0; i < JointCount; i++)
        {
            initialLocalRot[i] = joints[i].localRotation;
            angles[i] = 0f;

            if (useArticulationBodies)
            {
                ArticulationBody ab = articulationBodies[i];

                // Asse di rotazione espresso nello spazio LOCALE di joints[i]
                // Nota: negli URDF di Unity, ab.anchorPosition è quasi sempre (0,0,0)
                // perché l'URDF Importer crea l'origine del link proprio sul giunto!
                axes[i] = (ab.anchorRotation * Vector3.right).normalized;

                float currentDeg = ab.jointPosition.dofCount > 0 ? ab.jointPosition[0] * Mathf.Rad2Deg : 0f;
                initialDriveTargets[i] = currentDeg;

                if (autoConfigureDrives)
                {
                    ArticulationDrive drive = ab.xDrive;
                    drive.stiffness = driveStiffness;
                    drive.damping = driveDamping;
                    drive.forceLimit = driveForceLimit;
                    drive.target = currentDeg;
                    ab.xDrive = drive;
                }
            }
            else
            {
                axes[i] = localAxes[i].normalized;
            }
        }

        initialized = true;
        return true;
    }

    Transform GetBaseTransform()
    {
        return baseFrame != null ? baseFrame : joints[0].parent;
    }

    Matrix4x4 BaseLocalToWorld()
    {
        Transform b = GetBaseTransform();
        return b != null ? b.localToWorldMatrix : Matrix4x4.identity;
    }

    bool Validate()
    {
        if (joints == null || joints.Length != JointCount) return Fail("'joints' deve contenere esattamente 6 elementi.");
        for (int i = 0; i < JointCount; i++)
            if (joints[i] == null) return Fail("Joint " + (i + 1) + " non assegnato.");
        if (tcp == null) return Fail("TCP non assegnato.");

        if (useArticulationBodies)
        {
            if (articulationBodies == null || articulationBodies.Length != JointCount)
                return Fail("'articulationBodies' deve contenere 6 elementi.");
            for (int i = 0; i < JointCount; i++)
            {
                if (articulationBodies[i] == null)
                    articulationBodies[i] = joints[i].GetComponent<ArticulationBody>();
                if (articulationBodies[i] == null)
                    return Fail("ArticulationBody del Joint " + (i + 1) + " mancante.");
            }
        }
        else
        {
            if (localAxes == null || localAxes.Length != JointCount) return Fail("'localAxes' deve contenere 6 elementi.");
            for (int i = 0; i < JointCount; i++)
                if (localAxes[i].sqrMagnitude < 1e-8f) return Fail("L'asse del Joint " + (i + 1) + " è nullo.");
        }
        return true;
    }

    bool Fail(string message)
    {
        Debug.LogError("[UR5eRobotModel] " + message, this);
        return false;
    }

    public float[] GetAngles()
    {
        if (!initialized && !Initialize()) return new float[JointCount];
        return (float[])angles.Clone();
    }

    public float[] ClampToLimits(float[] anglesDeg)
    {
        float[] r = new float[JointCount];
        for (int i = 0; i < JointCount; i++)
            r[i] = Mathf.Clamp(anglesDeg[i], minAnglesDeg[i], maxAnglesDeg[i]);
        return r;
    }

    public void SetJointAngles(float[] anglesDeg, bool clampToLimits = true)
    {
        if (!initialized && !Initialize()) return;
        if (anglesDeg == null || anglesDeg.Length < JointCount) return;

        for (int i = 0; i < JointCount; i++)
        {
            float a = anglesDeg[i];
            if (clampToLimits) a = Mathf.Clamp(a, minAnglesDeg[i], maxAnglesDeg[i]);
            angles[i] = a;
            ApplyJoint(i);
        }
    }

    void ApplyJoint(int i)
    {
        if (useArticulationBodies)
        {
            ArticulationBody ab = articulationBodies[i];
            ArticulationDrive drive = ab.xDrive;
            drive.target = initialDriveTargets[i] + (angles[i] * articulationSigns[i]);
            ab.xDrive = drive;
        }
        else
        {
            joints[i].localRotation = initialLocalRot[i] * Quaternion.AngleAxis(angles[i], axes[i]);
        }
    }

    public Matrix4x4 ForwardKinematics(float[] anglesDeg)
    {
        if (!initialized && !Initialize()) return Matrix4x4.identity;

        Matrix4x4 m = Matrix4x4.identity;
        for (int i = 0; i < JointCount; i++)
        {
            float s = useArticulationBodies ? articulationSigns[i] : 1f;
            m = m * relative[i] * Matrix4x4.Rotate(Quaternion.AngleAxis(anglesDeg[i] * s, axes[i]));
        }
        m = m * relativeTcp;

        return BaseLocalToWorld() * m;
    }

    public void ForwardKinematics(float[] anglesDeg, out Vector3 position, out Quaternion rotation)
    {
        Matrix4x4 m = ForwardKinematics(anglesDeg);
        position = (Vector3)m.GetColumn(3);
        rotation = Quaternion.LookRotation((Vector3)m.GetColumn(2), (Vector3)m.GetColumn(1));
    }

    [ContextMenu("Verifica FK")]
    public void VerifyForwardKinematics()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[UR5eRobotModel] Eseguire in Play Mode."); return; }
        if (!initialized && !Initialize()) return;

        float[] realAnglesDeg = new float[JointCount];
        string debugJoints = "";
        for (int i = 0; i < JointCount; i++)
        {
            if (useArticulationBodies && articulationBodies[i].jointPosition.dofCount > 0)
            {
                float currentDeg = articulationBodies[i].jointPosition[0] * Mathf.Rad2Deg;
                realAnglesDeg[i] = (currentDeg - initialDriveTargets[i]) * articulationSigns[i];
                debugJoints += $"J{i}: cmd={angles[i]:F1}° real={realAnglesDeg[i]:F1}° (axis={axes[i]}) | ";
            }
            else
            {
                realAnglesDeg[i] = angles[i];
            }
        }

        ForwardKinematics(realAnglesDeg, out Vector3 p, out Quaternion q);
        float dPos = Vector3.Distance(p, tcp.position);
        float dRot = Quaternion.Angle(q, tcp.rotation);
        Debug.Log($"[UR5eRobotModel] FK vs TCP reale -> errore pos: {(dPos * 1000f):F3} mm, rot: {dRot:F3}°\n{debugJoints}");
    }

    void OnDrawGizmosSelected()
    {
        if (joints == null) return;

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;
            Vector3 localAxis = Vector3.up;

            if (useArticulationBodies && articulationBodies != null && i < articulationBodies.Length && articulationBodies[i] != null)
                localAxis = articulationBodies[i].anchorRotation * Vector3.right;
            else if (localAxes != null && i < localAxes.Length)
                localAxis = localAxes[i].normalized;

            Vector3 a = joints[i].TransformDirection(localAxis);
            Vector3 p = joints[i].position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(p - a * 0.15f, p + a * 0.15f);
            Gizmos.DrawSphere(p + a * 0.15f, 0.01f);
        }

        if (tcp != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(tcp.position, 0.02f);
        }
    }
}