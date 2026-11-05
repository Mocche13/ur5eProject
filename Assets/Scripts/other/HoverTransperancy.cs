using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class HoverTransparency : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private float normalAlpha = 0.1f;
    [SerializeField] private float hoverAlpha = 0.95f;
    [SerializeField] private float fadeSpeed = .5f;

    private Material material;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
    private float targetAlpha;
    private Color baseColor;

    void Start()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        material = targetRenderer.material;
        baseColor = material.GetColor("_Color");
        targetAlpha = normalAlpha;

        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        interactable.hoverEntered.AddListener(OnHoverEnter);
        interactable.hoverExited.AddListener(OnHoverExit);
    }

        void Update()
        {
            Color currentColor = material.GetColor("_Color");
            currentColor.a = Mathf.Lerp(currentColor.a, targetAlpha, Time.deltaTime * fadeSpeed);
            material.SetColor("_Color", currentColor);
            
            Debug.Log($"Current Alpha: {currentColor.a}, Target: {targetAlpha}"); // Add this
        }

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        targetAlpha = hoverAlpha;
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        targetAlpha = normalAlpha;
    }

    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEnter);
            interactable.hoverExited.RemoveListener(OnHoverExit);
        }
    }
}