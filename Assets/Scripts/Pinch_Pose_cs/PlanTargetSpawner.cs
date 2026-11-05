using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.InputSystem;

public class PlanTargetSpawner : MonoBehaviour
{
    [Header("Spawning")]
    public GameObject planTargetPrefab;
    public Transform spawnParent;

    [Header("Inputs")]
    public InputActionProperty confirmAction;
    public InputActionProperty lengthAction;

    [Header("Right Controller Interactor")]
    [Tooltip("Drag the RIGHT hand Near-Far Interactor here")]
    public NearFarInteractor nearFarInteractor;

    [Header("Workspace")]
    public Transform robotBaseLink;
    public Transform endEffector;
    public float maxReach = 0.85f;
    public float minReach = 0.10f;

    [Header("Ray Length")]
    public float minRayLength = 0.2f;
    public float maxRayLength = 1.5f;
    public float rayLengthSpeed = 0.5f;
    public float currentRayLength = 0.5f;

    [Header("Preview Sphere")]
    public float previewRadius = 0.04f;
    public Color insideWorkspaceColor = new Color(0.2f, 1.0f, 0.4f, 0.5f);
    public Color outsideWorkspaceColor = new Color(1.0f, 0.3f, 0.3f, 0.4f);

    [Header("Path Line")]
    public Color pathColor = Color.yellow;
    public float pathWidth = 0.005f;

    private GameObject previewSphere;
    private Material previewMat;
    private GameObject confirmedTarget;
    private LineRenderer pathLine;
    private bool currentInside = false;

    void OnEnable()
    {
        if (confirmAction.action != null)
        {
            confirmAction.action.Enable();
            confirmAction.action.performed += OnConfirmPressed;
        }
        if (lengthAction.action != null) lengthAction.action.Enable();
    }

    void OnDisable()
    {
        if (confirmAction.action != null)
            confirmAction.action.performed -= OnConfirmPressed;
    }

    void Start()
    {
        if (nearFarInteractor == null)
            Debug.LogError("[PlanTargetSpawner] Right Near-Far Interactor not assigned");

        // Preview sphere
        previewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        previewSphere.name = "TargetPreview";
        Destroy(previewSphere.GetComponent<Collider>());
        previewSphere.transform.localScale = Vector3.one * previewRadius * 2f;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            previewMat = new Material(sh);
            if (sh != null && sh.name.Contains("Universal Render Pipeline"))
            {
                previewMat.SetFloat("_Surface", 1);
                previewMat.SetFloat("_Blend", 0);
                previewMat.SetOverrideTag("RenderType", "Transparent");
                previewMat.renderQueue = 3000;
                previewMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                previewMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                previewMat.SetInt("_ZWrite", 0);
            }
        previewSphere.GetComponent<MeshRenderer>().material = previewMat;

        // Path line
        var lineGO = new GameObject("PathToTarget");
        pathLine = lineGO.AddComponent<LineRenderer>();
        pathLine.material = new Material(Shader.Find("Sprites/Default"));
        pathLine.startColor = pathColor;
        pathLine.endColor = pathColor;
        pathLine.startWidth = pathWidth;
        pathLine.endWidth = pathWidth;
        pathLine.positionCount = 0;
    }

    void Update()
    {
        if (nearFarInteractor == null) return;

        if (lengthAction.action != null)
        {
            Vector2 stick = lengthAction.action.ReadValue<Vector2>();
            currentRayLength += stick.y * rayLengthSpeed * Time.deltaTime;
            currentRayLength = Mathf.Clamp(currentRayLength, minRayLength, maxRayLength);
        }

        Transform rayOrigin = nearFarInteractor.transform;
        Vector3 spherePos = rayOrigin.position + rayOrigin.forward * currentRayLength;
        previewSphere.transform.position = spherePos;

        currentInside = false;
        if (robotBaseLink != null)
        {
            float distance = Vector3.Distance(spherePos, robotBaseLink.position);
            currentInside = distance <= maxReach && distance >= minReach;
        }

        previewMat.color = currentInside ? insideWorkspaceColor : outsideWorkspaceColor;

        if (currentInside && endEffector != null)
        {
            pathLine.positionCount = 2;
            pathLine.SetPosition(0, endEffector.position);
            pathLine.SetPosition(1, spherePos);
        }
        else
        {
            pathLine.positionCount = 0;
        }
    }

    private void OnConfirmPressed(InputAction.CallbackContext ctx)
    {
        if (!currentInside)
        {
            Debug.LogWarning("[PlanTargetSpawner] Cannot place — outside workspace");
            return;
        }

        if (confirmedTarget != null) Destroy(confirmedTarget);

        confirmedTarget = Instantiate(planTargetPrefab,
                                      previewSphere.transform.position,
                                      Quaternion.identity,
                                      spawnParent);

        // Face menu toward user
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            var canvas = confirmedTarget.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                Vector3 dir = mainCam.transform.position - canvas.transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                    canvas.transform.rotation = Quaternion.LookRotation(-dir);
            }
        }

        Debug.Log($"[PlanTargetSpawner] Confirmed at {previewSphere.transform.position}");
    }
}