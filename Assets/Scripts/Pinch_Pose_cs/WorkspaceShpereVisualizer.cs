using UnityEngine;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class WorkspaceSphereVisualizer : MonoBehaviour
{
    [Header("Workspace")]
    public Transform robotBaseLink;
    public float maxReach = 0.85f;

    [Header("Appearance")]
    public Color sphereColor = new Color(0.2f, 0.7f, 1.0f, 0.15f);

    void Start()
    {
        if (robotBaseLink == null)
        {
            Debug.LogError("[WorkspaceSphere] robotBaseLink not assigned");
            return;
        }

        // Use sphere primitive mesh
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        GetComponent<MeshFilter>().sharedMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
        Destroy(sphere);

        // Disable collider so raycasts pass through
        if (TryGetComponent(out Collider col)) Destroy(col);

        // Transparent material with shader fallback
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        var mat = new Material(sh);
        if (sh != null && sh.name.Contains("Universal Render Pipeline"))
        {
            // Enable transparency in URP
            mat.SetFloat("_Surface", 1);                     // 0 opaque, 1 transparent
            mat.SetFloat("_Blend", 0);                       // 0 alpha, 1 premultiply, 2 additive
            mat.SetFloat("_AlphaClip", 0);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor("_BaseColor", sphereColor);
        }
        mat.color = sphereColor;
        GetComponent<MeshRenderer>().material = mat;

        transform.position = robotBaseLink.position;
        transform.localScale = Vector3.one * (maxReach * 2f);
    }

    void Update()
    {
        if (robotBaseLink != null)
            transform.position = robotBaseLink.position;
    }
}