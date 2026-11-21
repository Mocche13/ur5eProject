using System.Collections;
using UnityEngine;

public class SmartDoorController : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("Riferimenti Porta e Player")]
    [Tooltip("Oggetto che ruota sui cardini (se vuoto, usa questo stesso Transform)")]
    public Transform doorPivot;
    [Tooltip("Transform del giocatore (es. Main Camera o XR Origin). Se vuoto, cerca tag 'Player' o Camera.main")]
    public Transform playerTransform;
    [Tooltip("Punto da cui misurare la distanza (se vuoto, usa la posizione del pivot/porta)")]
    public Transform referencePoint;

    [Header("Asse, Angolo e Velocità")]
    [Tooltip("Asse locale attorno cui ruota la porta")]
    public RotationAxis openAxis = RotationAxis.Y;
    [Tooltip("Gradi di apertura (usa valore positivo o negativo per invertire il verso)")]
    public float openAngle = 90f;
    public float openSpeed = 2.5f;
    public float closeSpeed = 2.5f;

    [Header("Avvicinamento per Uscita e Allontanamento per Chiusura")]
    public bool autoOpenOnApproach = true;
    public float approachOpenDistance = 2.0f;
    public bool autoCloseWhenFar = true;
    public float autoCloseDistance = 3.5f;

    [Header("Audio Porta")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;

    private Quaternion closedRot;
    private Quaternion openRot;
    private bool isOpen = false;
    private bool isMoving = false;
    private Coroutine moveRoutine;

    private void Awake()
    {
        if (doorPivot == null) doorPivot = transform;
        if (referencePoint == null) referencePoint = doorPivot;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
            else if (Camera.main != null) playerTransform = Camera.main.transform;
        }

        closedRot = doorPivot.localRotation;
        openRot = closedRot * Quaternion.Euler(GetEulerForAxis(openAxis, openAngle));
    }

    private Vector3 GetEulerForAxis(RotationAxis axis, float angle)
    {
        switch (axis)
        {
            case RotationAxis.X:
                return new Vector3(angle, 0f, 0f);
            case RotationAxis.Z:
                return new Vector3(0f, 0f, angle);
            case RotationAxis.Y:
            default:
                return new Vector3(0f, angle, 0f);
        }
    }

    private void Update()
    {
        if (playerTransform == null || referencePoint == null) return;

        float dist = Vector3.Distance(playerTransform.position, referencePoint.position);

        if (autoOpenOnApproach && !isOpen && !isMoving && dist <= approachOpenDistance)
        {
            OpenDoor();
            return;
        }

        if (autoCloseWhenFar && isOpen && !isMoving && dist >= autoCloseDistance)
        {
            CloseDoor();
        }
    }

    public void UnlockAndOpen()
    {
        OpenDoor();
    }

    public void OpenDoor()
    {
        if (isOpen && !isMoving) return;

        isOpen = true;
        PlayClip(openSound);
        StartMove(openRot, openSpeed);
    }

    public void CloseDoor()
    {
        if (!isOpen && !isMoving) return;

        isOpen = false;
        PlayClip(closeSound);
        StartMove(closedRot, closeSpeed);
    }

    private void StartMove(Quaternion targetRot, float speed)
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(RotateDoorRoutine(targetRot, speed));
    }

    private IEnumerator RotateDoorRoutine(Quaternion targetRot, float speed)
    {
        isMoving = true;

        while (Quaternion.Angle(doorPivot.localRotation, targetRot) > 0.1f)
        {
            doorPivot.localRotation = Quaternion.Slerp(
                doorPivot.localRotation,
                targetRot,
                speed * Time.deltaTime
            );
            yield return null;
        }

        doorPivot.localRotation = targetRot;
        isMoving = false;
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}