using UnityEngine;
using Unity.Robotics.ROSTCPConnector;

public class ROSConfigurator : MonoBehaviour
{
    private void Awake()
    {
        ApplySettings();
    }

    public void ApplySettings()
    {
        ROSConnection ros = ROSConnection.GetOrCreateInstance();

        if (AppManager.Instance == null)
        {
            Debug.LogWarning("[ROSConfigurator] AppManager not found – using Inspector defaults.");
            return;
        }

        string targetIP   = AppManager.Instance.RobotIP;
        int    targetPort = AppManager.Instance.RobotPort;

        // Always disconnect cleanly before changing settings to avoid stale state
        try { ros.Disconnect(); } catch { /* safe to ignore if not connected */ }

        ros.RosIPAddress = targetIP;
        ros.RosPort      = targetPort;

        Debug.Log($"[ROSConfigurator] Applied: {targetIP}:{targetPort}");

        // Reconnect with the correct settings
        ros.Connect();
    }
}
