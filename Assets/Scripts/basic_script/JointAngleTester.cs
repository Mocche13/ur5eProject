using UnityEngine;
using System.Collections.Generic;

public class JointAngleTester : MonoBehaviour
{
    [Header("Robot Joints")]
    [Tooltip("Drag your 6 ArticulationBody joints here in order: Base -> Shoulder -> Elbow -> Wrist1 -> Wrist2 -> Wrist3")]
    public List<ArticulationBody> robotJoints;

    [Header("Test Controls (Radians/Meters)")]
    [Tooltip("Enter values here. Radians for Revolute joints, Meters for Prismatic joints.")]
    public float[] targetRadians;

    [Header("Gripper Settings")]
    [Tooltip("Multiplier for Prismatic joints (e.g. 0.5 to map 0-0.05 input to 0-0.025 motion).")]
    public float prismaticMultiplier = 0.5f;

    void Start()
    {
        // Initialize the array if empty
        if (targetRadians == null || targetRadians.Length != robotJoints.Count)
        {
            targetRadians = new float[robotJoints.Count];
        }
    }

    void Update()
    {
        // Continuously apply the values from the Inspector
        ApplyJoints();
    }

    void ApplyJoints()
    {
        for (int i = 0; i < robotJoints.Count; i++)
        {
            if (robotJoints[i] != null)
            {
                // 1. Get raw value from Inspector (Radians for arm, Meters for gripper)
                float value = targetRadians[i];

                // 2. Conversion Logic based on Joint Type
                // Unity ArticulationBody.xDrive.target expects Degrees for Revolute, but Meters for Prismatic.
                if (robotJoints[i].jointType == ArticulationJointType.RevoluteJoint)
                {
                    value *= Mathf.Rad2Deg;
                }
                // If it is PrismaticJoint, we use the raw value (Meters) BUT scaled.
                else if (robotJoints[i].jointType == ArticulationJointType.PrismaticJoint)
                {
                    // Apply the multiplier (0.5) so 0.05 input becomes 0.025 motion
                    // This creates a linear mapping without dead bands.
                    value *= prismaticMultiplier;

                    // Optional: Still clamp to physical limits to prevent explosion if input > 0.05
                    float lower = robotJoints[i].xDrive.lowerLimit;
                    float upper = robotJoints[i].xDrive.upperLimit;
                    if (lower != upper)
                    {
                        value = Mathf.Clamp(value, lower, upper);
                    }
                }

                // 3. Apply to Joint Drive
                var drive = robotJoints[i].xDrive;
                drive.target = value;
                robotJoints[i].xDrive = drive;
            }
        }
    }
}