using UnityEngine;

public class UIFollowPlayer : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float distance = 2f;
    [SerializeField] private float angleTrigger = 45f; // reposition if beyond this angle
    [SerializeField] private float moveSpeed = 3f;

    private Vector3 targetPosition;

    void Start()
    {
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
        SnapToCamera();
    }

    void Update()
    {
        float angle = Vector3.Angle(
            cameraTransform.forward,
            transform.position - cameraTransform.position);

        if (angle > angleTrigger)
            SnapToCamera();

        transform.position = Vector3.Lerp(
            transform.position, targetPosition, Time.deltaTime * moveSpeed);

        transform.LookAt(transform.position 
                       + (transform.position - cameraTransform.position));
    }

    void SnapToCamera()
    {
        targetPosition = cameraTransform.position 
                       + cameraTransform.forward * distance;
    }
}