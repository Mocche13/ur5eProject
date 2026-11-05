using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using System.Collections.Generic;
using System.Collections;

public class JointStateSubscriber : MonoBehaviour
{
    public string topicName = "/joint_states";
    public List<ArticulationBody> robotJoints;
    public bool[] invertJoint;
    public float prismaticMultiplier = 0.5f;

    void Start()
    {
        if (invertJoint == null || invertJoint.Length != robotJoints.Count)
            invertJoint = new bool[robotJoints.Count];

        StartCoroutine(SubscribeRoutine());
    }

    IEnumerator SubscribeRoutine()
    {
        // Wait one frame so ROSConfigurator.Start() runs first
        yield return null;

        ROSConnection ros = ROSConnection.GetOrCreateInstance();

        // Wait for ROSConfigurator to establish connection (up to 15s)
        float timeout = 15f;
        float elapsed = 0f;

        while (!ros.HasConnectionThread && elapsed < timeout)
        {
            elapsed += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }

        if (!ros.HasConnectionThread)
        {
            Debug.LogError("[JointStateSubscriber] ROS not connected after 15s.");
            yield break;
        }

        ros.Subscribe<JointStateMsg>(topicName, UpdateJoints);
        Debug.Log($"[JointStateSubscriber] Subscribed to {topicName}");
    }

    void UpdateJoints(JointStateMsg msg)
    {
        if (msg.name.Length != robotJoints.Count) return;

        for (int i = 0; i < msg.name.Length; i++)
        {
            if (robotJoints[i] == null) continue;

            float value = (float)msg.position[i];
            if (invertJoint[i]) value = -value;

            if (robotJoints[i].jointType == ArticulationJointType.RevoluteJoint)
                value *= Mathf.Rad2Deg;
            else if (robotJoints[i].jointType == ArticulationJointType.PrismaticJoint)
                value *= prismaticMultiplier;

            var xDrive = robotJoints[i].xDrive;
            xDrive.target = value;
            robotJoints[i].xDrive = xDrive;
        }
    }
}