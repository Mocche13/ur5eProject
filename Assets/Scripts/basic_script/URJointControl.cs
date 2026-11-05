using UnityEngine;

/// <summary>
/// Standalone per-joint controller. No URDF Importer dependency.
/// Added automatically by URController to each non-fixed ArticulationBody.
/// direction: -1 = negative, 0 = stopped, 1 = positive
/// </summary>
public class URJointControl : MonoBehaviour
{
    // Set by URController
    public int   direction  = 0;
    public float speed      = 30f;
    public float torque     = 100f;
    public float acceleration = 5f;

    private URController    _controller;
    private ArticulationBody _joint;

    public void Initialize(URController controller, ArticulationBody joint)
    {
        _controller  = controller;
        _joint       = joint;
        speed        = controller.speed;
        torque       = controller.torque;
        acceleration = controller.acceleration;
    }

    void FixedUpdate()
    {
        if (_controller == null || _joint == null) return;
        if (_joint.jointType == ArticulationJointType.FixedJoint) return;

        // Keep in sync with controller settings
        speed        = _controller.speed;
        torque       = _controller.torque;
        acceleration = _controller.acceleration;

        ArticulationDrive drive      = _joint.xDrive;
        float             delta      = direction * Time.fixedDeltaTime * speed;
        float             newTarget  = drive.target + delta;

        if (_joint.twistLock == ArticulationDofLock.LimitedMotion ||
            _joint.linearLockX == ArticulationDofLock.LimitedMotion)
        {
            newTarget = Mathf.Clamp(newTarget, drive.lowerLimit, drive.upperLimit);
        }

        drive.target  = newTarget;
        _joint.xDrive = drive;
    }
}
