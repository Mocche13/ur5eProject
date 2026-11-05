using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.Ur5eMoveitActions;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

public class MoveItActionClient : MonoBehaviour
{
    [Header("ROS Topics")]
    public string planGoalTopic = "plan_to_pose/goal";
    public string planResultTopic = "plan_to_pose/result";
    public string executeGoalTopic = "execute_plan/goal";
    public string executeResultTopic = "execute_plan/result";

    [Header("Gripper Topics")]
    public string gripperOpenTopic = "gripper/open";
    public string gripperCloseTopic = "gripper/close";
    public string gripperResultTopic = "gripper/result";

    [Header("Planning Parameters")]
    public float planningTime = 30.0f;
    public float velocityScaling = 0.3f;
    public float accelerationScaling = 0.3f;
    public string plannerId = "RRTConnect";

    [Header("Frame Reference")]
    public Transform robotBase;

    private ROSConnection ros;

    public class PlanToPoseResult
    {
        public bool success;
        public string planId;
        public Vector3[] tcpWaypoints;
        public string errorMessage;
    }

    public class ExecutePlanResult
    {
        public bool success;
        public string errorMessage;
    }

    private Action<PlanToPoseResult> currentPlanCallback;
    private Action<ExecutePlanResult> currentExecuteCallback;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        ros.RegisterPublisher<PlanToPoseGoalMsg>(planGoalTopic);
        ros.RegisterPublisher<ExecutePlanGoalMsg>(executeGoalTopic);
        ros.RegisterPublisher<BoolMsg>(gripperOpenTopic);
        ros.RegisterPublisher<BoolMsg>(gripperCloseTopic);

        ros.Subscribe<PlanToPoseResultMsg>(planResultTopic, OnPlanResponse);
        ros.Subscribe<ExecutePlanResultMsg>(executeResultTopic, OnExecuteResponse);
        ros.Subscribe<BoolMsg>(gripperResultTopic, OnGripperResult);

        Debug.Log("[MoveItActionClient] Initialized.");
    }

    public void PlanToPose(Vector3 unityWorldPos, Quaternion unityWorldRot,
                            Action<PlanToPoseResult> callback)
    {
        currentPlanCallback = callback;

        Vector3 localPos = robotBase.InverseTransformPoint(unityWorldPos);
        Quaternion localRot = Quaternion.Inverse(robotBase.rotation) * unityWorldRot;

        var rosPos = localPos.To<FLU>();
        var rosRot = localRot.To<FLU>();

        Debug.Log($"[MoveItActionClient] ROS pose: pos=({rosPos.x:F3}, {rosPos.y:F3}, {rosPos.z:F3})");

        var goal = new PlanToPoseGoalMsg();
        goal.target_pose.header.frame_id = "base_link";
        goal.target_pose.pose.position.x = rosPos.x;
        goal.target_pose.pose.position.y = rosPos.y;
        goal.target_pose.pose.position.z = rosPos.z;
        goal.target_pose.pose.orientation.x = rosRot.x;
        goal.target_pose.pose.orientation.y = rosRot.y;
        goal.target_pose.pose.orientation.z = rosRot.z;
        goal.target_pose.pose.orientation.w = rosRot.w;

        ros.Publish(planGoalTopic, goal);
        Debug.Log($"[MoveItActionClient] Plan goal sent: pos=({rosPos.x:F3}, {rosPos.y:F3}, {rosPos.z:F3})");
    }

    public void ExecutePlan(string planId, Action<ExecutePlanResult> callback)
    {
        currentExecuteCallback = callback;
        var goal = new ExecutePlanGoalMsg { plan_id = planId };
        ros.Publish(executeGoalTopic, goal);
        Debug.Log($"[MoveItActionClient] Execute goal sent: planId={planId}");
    }

    public void OpenGripper()
    {
        ros.Publish(gripperOpenTopic, new BoolMsg(true));
        Debug.Log("[MoveItActionClient] Gripper open sent.");
    }

    public void CloseGripper()
    {
        ros.Publish(gripperCloseTopic, new BoolMsg(true));
        Debug.Log("[MoveItActionClient] Gripper close sent.");
    }

    private void OnPlanResponse(PlanToPoseResultMsg msg)
    {
        Debug.Log($"[MoveItActionClient] Plan result: success={msg.success}, planId={msg.plan_id}");

        var waypoints = new Vector3[msg.tcp_waypoints.Length];
        for (int i = 0; i < msg.tcp_waypoints.Length; i++)
        {
            var p = msg.tcp_waypoints[i];
            Vector3 localPos = new Vector3(-(float)p.y, (float)p.z, (float)p.x);
            waypoints[i] = robotBase.TransformPoint(localPos);
        }

        var result = new PlanToPoseResult
        {
            success = msg.success,
            planId = msg.plan_id,
            tcpWaypoints = waypoints,
            errorMessage = msg.message
        };

        currentPlanCallback?.Invoke(result);
        currentPlanCallback = null;
    }

    private void OnExecuteResponse(ExecutePlanResultMsg msg)
    {
        Debug.Log($"[MoveItActionClient] Execute result: success={msg.success}");

        var result = new ExecutePlanResult
        {
            success = msg.success,
            errorMessage = msg.message
        };

        currentExecuteCallback?.Invoke(result);
        currentExecuteCallback = null;
    }

    private void OnGripperResult(BoolMsg msg)
    {
        Debug.Log($"[MoveItActionClient] Gripper result: success={msg.data}");
    }
}