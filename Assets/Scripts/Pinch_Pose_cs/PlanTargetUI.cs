using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlanTargetUI : MonoBehaviour
{
    [Header("Status")]
    public TextMeshProUGUI statusText;

    [Header("Buttons")]
    public Button planButton;
    public Button executeButton;
    public Button cancelButton;
    public Button openGripperButton;
    public Button closeGripperButton;

    [Header("Colors")]
    public Color planActiveColor = new Color(0.2f, 0.8f, 0.2f);
    public Color executeActiveColor = new Color(0.2f, 0.5f, 1.0f);
    public Color cancelActiveColor = new Color(1.0f, 0.2f, 0.2f);
    public Color gripperActiveColor = new Color(1.0f, 0.6f, 0.0f);
    public Color disabledColor = new Color(0.4f, 0.4f, 0.4f);

    [Header("Gripper Reference")]
    private MoveItActionClient actionClient;

    public enum Phase { Idle, ReadyToPlan, Planning, ReadyToExecute, Executing, Done, Failed }

    void Start()
    {
      // Find MoveItActionClient at runtime instead of Inspector reference
    actionClient = FindFirstObjectByType<MoveItActionClient>();

    if (openGripperButton != null)
        openGripperButton.onClick.AddListener(() => actionClient.OpenGripper());

    if (closeGripperButton != null)
        closeGripperButton.onClick.AddListener(() => actionClient.CloseGripper());

    SetPhase(Phase.ReadyToPlan);
    }

    public void SetPhase(Phase phase)
    {
        bool canPlan = phase == Phase.ReadyToPlan || phase == Phase.Failed;
        bool canExecute = phase == Phase.ReadyToExecute;
        bool canCancel = phase == Phase.Executing;
        bool canGripper = phase != Phase.Planning && phase != Phase.Executing;

        SetButton(planButton, canPlan, planActiveColor);
        SetButton(executeButton, canExecute, executeActiveColor);
        SetButton(cancelButton, canCancel, cancelActiveColor);
        SetButton(openGripperButton, canGripper, gripperActiveColor);
        SetButton(closeGripperButton, canGripper, gripperActiveColor);
    }

    private void SetButton(Button btn, bool active, Color activeCol)
    {
        if (btn == null) return;
        btn.interactable = active;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = active ? activeCol : disabledColor;
    }

    public void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    public void ShowError(string error)
    {
        if (statusText != null) statusText.text = $"<color=red>{error}</color>";
    }
}