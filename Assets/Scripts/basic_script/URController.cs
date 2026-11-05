using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Standalone robot controller. No URDF Importer dependency.
/// Attach to the root robot GameObject.
/// Automatically finds and initializes URJointControl on each non-fixed ArticulationBody.
/// </summary>
public class URController : MonoBehaviour
{
    [Header("Drive Settings")]
    public float stiffness   = 10000f;
    public float damping     = 100f;
    public float forceLimit  = 1000f;
    public float speed       = 30f;   // deg/s for revolute
    public float torque      = 100f;
    public float acceleration = 5f;

    [Header("Selection (Read Only in Play)")]
    [SerializeField] private string selectedJointName;
    [SerializeField] private int    selectedIndex = 0;

    [Header("Highlight Color")]
    public Color highlightColor = new Color(1f, 0f, 0f, 1f);

    // Internal
    internal URJointControl[] Joints { get; private set; }

    void Start()
    {
        var bodies   = GetComponentsInChildren<ArticulationBody>();
        var jointList = new System.Collections.Generic.List<URJointControl>();

        foreach (var body in bodies)
        {
            if (body.jointType == ArticulationJointType.FixedJoint) continue;

            URJointControl jc = body.GetComponent<URJointControl>();
            if (jc == null) jc = body.gameObject.AddComponent<URJointControl>();

            // Configure drive
            body.jointFriction  = 10f;
            body.angularDamping = 10f;
            var drive        = body.xDrive;
            drive.stiffness  = stiffness;
            drive.damping    = damping;
            drive.forceLimit = forceLimit;
            body.xDrive      = drive;

            jc.Initialize(this, body);
            jointList.Add(jc);
        }

        Joints        = jointList.ToArray();
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, Joints.Length - 1));

        // Ensure URFK is present
        if (GetComponent<URFK>() == null) gameObject.AddComponent<URFK>();

        RefreshLabel();
        Debug.Log($"[URController] Initialized with {Joints.Length} joints.");
    }

    void Update()
    {
        if (Joints == null || Joints.Length == 0) return;

#if ENABLE_INPUT_SYSTEM
        bool selectNext = Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame;
        bool selectPrev = Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame;
        float move = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.isPressed)   move =  1f;
            if (Keyboard.current.downArrowKey.isPressed) move = -1f;
        }
#else
        bool selectNext = Input.GetKeyDown("right");
        bool selectPrev = Input.GetKeyDown("left");
        float move = Input.GetAxis("Vertical");
#endif

        if (selectNext) ChangeSelection(selectedIndex + 1);
        if (selectPrev) ChangeSelection(selectedIndex - 1);

        for (int i = 0; i < Joints.Length; i++)
            Joints[i].direction = (i == selectedIndex) ? (move > 0f ? 1 : move < 0f ? -1 : 0) : 0;
    }

    private void ChangeSelection(int index)
    {
        selectedIndex = (index + Joints.Length) % Joints.Length;
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (Joints == null || Joints.Length == 0) return;
        selectedJointName = Joints[selectedIndex].name + " (" + selectedIndex + ")";
    }

    void OnGUI()
    {
        var s = GUI.skin.GetStyle("Label");
        s.alignment = TextAnchor.UpperCenter;
        GUI.Label(new Rect(Screen.width / 2 - 200, 10, 400, 20), "Left / Right arrows → select joint", s);
        GUI.Label(new Rect(Screen.width / 2 - 200, 30, 400, 20), "Up / Down arrows → move: " + selectedJointName, s);
    }
}
