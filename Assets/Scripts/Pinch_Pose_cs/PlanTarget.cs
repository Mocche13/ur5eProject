using UnityEngine;

public class PlanTarget : MonoBehaviour
{
    public enum State { PLACED, PLANNING, PLANNED, EXECUTING, DONE, FAILED }

    private State currentState = State.PLACED;
    private PlanTargetUI ui;
    private TrajectoryPreview trajectoryPreview;
    private MoveItActionClient moveItActionClient;

    private string currentPlanId;
    private MoveItActionClient actionClient;


    void Start()
    {
        actionClient = FindFirstObjectByType<MoveItActionClient>();

        ui = GetComponent<PlanTargetUI>();
        trajectoryPreview = GetComponent<TrajectoryPreview>();
        moveItActionClient = FindFirstObjectByType<MoveItActionClient>();

        if (ui == null) { Debug.LogError("[PlanTarget] PlanTargetUI missing"); return; }
        if (moveItActionClient == null) { Debug.LogError("[PlanTarget] MoveItActionClient missing"); return; }

        SetState(State.PLACED);
    }

    public void Plan()
    {
        Debug.Log("[PlanTarget] Plan() called");

        if (currentState != State.PLACED && currentState != State.FAILED)
        {
            Debug.LogWarning($"[PlanTarget] Cannot plan from state {currentState}");
            return;
        }

        SetState(State.PLANNING);

        Debug.Log($"[PlanTarget] Sending plan to ROS, target pos={transform.position}");

// TEMP: hardcode tool-pointing-down orientation for testing
            Quaternion testRot = Quaternion.Euler(180, 0, 0);
            moveItActionClient.PlanToPose(transform.position, testRot, (result) =>        {
            Debug.Log($"[PlanTarget] Got plan result: success={result.success}");
            if (result.success)
            {
                currentPlanId = result.planId;
                if (trajectoryPreview != null)
                    trajectoryPreview.DrawWaypoints(result.tcpWaypoints);
                SetState(State.PLANNED);
                ui.SetStatus($"Planned ({result.tcpWaypoints.Length} waypoints)");
            }
            else
            {
                SetState(State.FAILED);
                ui.ShowError("Plan failed: " + result.errorMessage);
            }
        });
    }

    public void Execute()
    {
        Debug.Log("[PlanTarget] Execute() called");

        if (currentState != State.PLANNED) return;
        if (string.IsNullOrEmpty(currentPlanId))
        {
            ui.ShowError("No plan ID");
            return;
        }

        SetState(State.EXECUTING);

        moveItActionClient.ExecutePlan(currentPlanId, (result) =>
        {
            if (result.success)
                SetState(State.DONE);
            else
            {
                SetState(State.FAILED);
                ui.ShowError("Execute failed: " + result.errorMessage);
            }
        });
    }

    public void Cancel()
    {
        if (currentState != State.EXECUTING) return;
        Debug.Log("[PlanTarget] Cancel requested (not yet wired)");
        SetState(State.FAILED);
        ui.ShowError("Execution cancelled");
    }

    private void SetState(State newState)
    {
        currentState = newState;
        switch (newState)
        {
            case State.PLACED:
                ui.SetPhase(PlanTargetUI.Phase.ReadyToPlan);
                ui.SetStatus("Ready to plan");
                break;
            case State.PLANNING:
                ui.SetPhase(PlanTargetUI.Phase.Planning);
                ui.SetStatus("Planning...");
                break;
            case State.PLANNED:
                ui.SetPhase(PlanTargetUI.Phase.ReadyToExecute);
                ui.SetStatus("Plan Ready");
                break;
            case State.EXECUTING:
                ui.SetPhase(PlanTargetUI.Phase.Executing);
                ui.SetStatus("Executing...");
                break;
            case State.DONE:
                ui.SetPhase(PlanTargetUI.Phase.Done);
                ui.SetStatus("Completed");
                break;
            case State.FAILED:
                ui.SetPhase(PlanTargetUI.Phase.Failed);
                ui.SetStatus("Failed");
                break;
        }
    }
    public void OnOpenGripperPressed()
        {
            actionClient.OpenGripper();
        }

        public void OnCloseGripperPressed()
        {
            actionClient.CloseGripper();
}
}