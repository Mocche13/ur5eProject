using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Standalone Jacobian-based Inverse Kinematics. No URDF Importer dependency.
/// Requires URFK on the same GameObject.
/// Call SolveIK(targetPosition) to move end effector toward a target.
/// </summary>
public class URIK : MonoBehaviour
{
    [Header("IK Settings")]
    [Tooltip("How far to perturb each joint to estimate the Jacobian (radians/meters)")]
    public float delta      = 0.005f;
    [Tooltip("How much to scale the Jacobian step each iteration")]
    public float stepSize   = 0.5f;
    [Tooltip("Max IK iterations per frame")]
    public int   iterations = 10;
    [Tooltip("Stop when end effector is within this distance of target (meters)")]
    public float tolerance  = 0.001f;
    [Tooltip("If true, IK runs automatically every FixedUpdate toward IKTarget")]
    public bool  autoSolve  = false;

    [Header("Auto-Solve Target")]
    public Transform IKTarget;

    private URFK _fk;
    private List<ArticulationBody> _joints;

    void Start()
    {
        _fk = GetComponent<URFK>();
        if (_fk == null)
        {
            Debug.LogError("[URIK] URFK component required on the same GameObject.");
            enabled = false;
            return;
        }
        _joints = _fk.GetJoints();
    }

    void FixedUpdate()
    {
        if (autoSolve && IKTarget != null)
            SolveIK(IKTarget.position);
    }

    /// <summary>
    /// Moves joints toward targetWorldPosition using Jacobian IK.
    /// </summary>
    public void SolveIK(Vector3 targetWorldPosition)
    {
        if (_fk == null || _joints == null || _joints.Count == 0) return;

        for (int iter = 0; iter < iterations; iter++)
        {
            List<float> angles    = _fk.GetJointPositions();
            Matrix4x4   currentT  = _fk.ComputeFK(angles);
            Vector3     currentPos = new Vector3(currentT[0, 3], currentT[1, 3], currentT[2, 3]);

            Vector3 error = targetWorldPosition - currentPos;
            if (error.magnitude < tolerance) break;

            // Build Jacobian (3 x n positional part)
            int       n        = _joints.Count;
            float[,]  jacobian = new float[3, n];

            for (int i = 0; i < n; i++)
            {
                List<float> perturbed = new List<float>(angles);
                perturbed[i] += delta;

                Matrix4x4 perturbedT   = _fk.ComputeFK(perturbed);
                Vector3   perturbedPos = new Vector3(perturbedT[0, 3], perturbedT[1, 3], perturbedT[2, 3]);
                Vector3   diff         = (perturbedPos - currentPos) / delta;

                jacobian[0, i] = diff.x;
                jacobian[1, i] = diff.y;
                jacobian[2, i] = diff.z;
            }

            // Jacobian transpose step: dq = J^T * error * stepSize
            float[] dq = new float[n];
            for (int i = 0; i < n; i++)
            {
                dq[i] = stepSize * (
                    jacobian[0, i] * error.x +
                    jacobian[1, i] * error.y +
                    jacobian[2, i] * error.z
                );
            }

            // Apply joint deltas
            for (int i = 0; i < n; i++)
            {
                if (_joints[i] == null) continue;
                var drive    = _joints[i].xDrive;
                drive.target = Mathf.Clamp(
                    drive.target + dq[i] * Mathf.Rad2Deg,
                    drive.lowerLimit,
                    drive.upperLimit
                );
                _joints[i].xDrive = drive;
            }
        }
    }
}
