using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Standalone Forward Kinematics. No URDF Importer dependency.
/// Attach to the root robot GameObject (added automatically by URController).
/// Computes end-effector transform from current joint positions using DH parameters.
/// Set DH parameters via the Inspector or at runtime before calling FK().
/// </summary>
public class URFK : MonoBehaviour
{
    [System.Serializable]
    public class DHRow
    {
        [Tooltip("alpha (rad) - twist angle")]
        public float alpha;
        [Tooltip("a (m) - link length")]
        public float a;
        [Tooltip("theta (rad) - joint angle (overwritten at runtime for revolute)")]
        public float theta;
        [Tooltip("d (m) - link offset (overwritten at runtime for prismatic)")]
        public float d;
    }

    [Header("DH Parameters (one row per non-fixed joint)")]
    public List<DHRow> dhParameters = new List<DHRow>();

    [Header("Results (Read Only)")]
    public Vector3    endEffectorPosition;
    public Quaternion endEffectorRotation;

    private List<ArticulationBody> _joints = new List<ArticulationBody>();

    void Start()
    {
        CollectJoints();
    }

    public void CollectJoints()
    {
        _joints.Clear();
        foreach (var body in GetComponentsInChildren<ArticulationBody>())
        {
            if (body.jointType != ArticulationJointType.FixedJoint)
                _joints.Add(body);
        }
    }

    void FixedUpdate()
    {
        if (dhParameters.Count == _joints.Count && _joints.Count > 0)
            ComputeFK();
    }

    /// <summary>
    /// Computes FK using current joint positions.
    /// Returns the end-effector homogeneous transform matrix.
    /// </summary>
    public Matrix4x4 ComputeFK(List<float> overrideAngles = null)
    {
        Matrix4x4 T = Matrix4x4.identity;

        for (int i = 0; i < dhParameters.Count && i < _joints.Count; i++)
        {
            DHRow row = dhParameters[i];

            // Update theta/d from joint position
            if (_joints[i].jointType == ArticulationJointType.RevoluteJoint)
                row.theta = overrideAngles != null ? overrideAngles[i] : _joints[i].jointPosition[0];
            else if (_joints[i].jointType == ArticulationJointType.PrismaticJoint)
                row.d = overrideAngles != null ? overrideAngles[i] : _joints[i].jointPosition[0];

            T = T * DHMatrix(row);
        }

        endEffectorPosition = new Vector3(T[0, 3], T[1, 3], T[2, 3]);
        endEffectorRotation = MatrixToQuaternion(T);

        return T;
    }

    /// <summary>
    /// Returns current joint positions in radians/meters.
    /// </summary>
    public List<float> GetJointPositions()
    {
        var result = new List<float>();
        foreach (var j in _joints)
            result.Add(j.jointPosition[0]);
        return result;
    }

    public List<ArticulationBody> GetJoints() => _joints;

    // Standard DH transform matrix (modified DH convention)
    private Matrix4x4 DHMatrix(DHRow row)
    {
        float ca = Mathf.Cos(row.alpha), sa = Mathf.Sin(row.alpha);
        float ct = Mathf.Cos(row.theta), st = Mathf.Sin(row.theta);

        return new Matrix4x4(
            new Vector4( ct,    -st,     0,      row.a),
            new Vector4( st*ca,  ct*ca, -sa,    -sa*row.d),
            new Vector4( st*sa,  ct*sa,  ca,     ca*row.d),
            new Vector4( 0,      0,      0,      1)
        );
    }

    private Quaternion MatrixToQuaternion(Matrix4x4 m)
    {
        return Quaternion.LookRotation(
            new Vector3(m[0, 2], m[1, 2], m[2, 2]),
            new Vector3(m[0, 1], m[1, 1], m[2, 1])
        );
    }
}
