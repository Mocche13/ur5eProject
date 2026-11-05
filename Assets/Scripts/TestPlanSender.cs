using UnityEngine;
using UnityEngine.InputSystem;

public class TestPlanSender : MonoBehaviour
{
    private MoveItActionClient client;

    void Start()
    {
        client = FindFirstObjectByType<MoveItActionClient>();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            Debug.Log("[Test] Sending plan goal");
            client.PlanToPose(
                new Vector3(0.3f, 0.5f, 0.0f),
                Quaternion.identity,
                (result) => Debug.Log($"[Test] Plan result: success={result.success}, planId={result.planId}")
            );
        }
    }
}