using UnityEngine;

public class MimicJoint : MonoBehaviour
{
    [Header("Target to Follow")]
    public ArticulationBody targetJoint; // Drag the Left Finger here

    [Header("Settings")]
    public float multiplier = 1.0f; // Use -1.0f if it needs to move opposite
    public float offset = 0.0f;

    private ArticulationBody myBody;

    void Start()
    {
        myBody = GetComponent<ArticulationBody>();
    }

    void Update()
    {
        if (targetJoint != null && myBody != null)
        {
            // Read the target's current drive target
            float targetValue = targetJoint.xDrive.target;

            // Apply to self with multiplier
            var drive = myBody.xDrive;
            drive.target = (targetValue * multiplier) + offset;
            myBody.xDrive = drive;
        }
    }
}