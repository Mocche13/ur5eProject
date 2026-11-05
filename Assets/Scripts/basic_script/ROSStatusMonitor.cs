using UnityEngine;
using TMPro;
using System.Collections;
using Unity.Robotics.ROSTCPConnector;

public class ROSStatusMonitor : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusText;

    private void Start()
    {
        StartCoroutine(CheckROSConnection());
    }

    private IEnumerator CheckROSConnection()
    {
        ROSConnection ros = ROSConnection.GetOrCreateInstance();
        UpdateText("ROS: <color=yellow>Connecting...</color>");

        while (true)
        {
            if (ros.HasConnectionError)
            {
                UpdateText("ROS: <color=red>Disconnected</color>");
            }
            else if (ros.HasConnectionThread)
            {
                UpdateText($"ROS: <color=green>Connected ({ros.RosIPAddress})</color>");
            }
            else
            {
                UpdateText("ROS: <color=yellow>Connecting...</color>");
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private void UpdateText(string msg)
    {
        if (statusText) statusText.text = msg;
    }
}