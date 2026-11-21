using UnityEngine;
using UnityEngine.InputSystem; // Se usi il nuovo Input System

public class KeypadMouseClicker : MonoBehaviour
{
    public Camera targetCamera;
    public float maxDistance = 10f;
    public LayerMask interactableLayers = ~0;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (targetCamera == null) return;

        bool clicked = false;
        Vector2 screenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            clicked = true;
            screenPos = Mouse.current.position.ReadValue();
        }
#endif

        // Fallback se il progetto ha attivo anche/solo l'Input Manager classico
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!clicked && Input.GetMouseButtonDown(0))
        {
            clicked = true;
            screenPos = Input.mousePosition;
        }
#endif

        if (!clicked) return;

        Ray ray = targetCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableLayers))
        {
            DoorKeypadKey key = hit.collider.GetComponentInParent<DoorKeypadKey>();
            if (key != null)
            {
                key.Press();
            }
        }
    }
}