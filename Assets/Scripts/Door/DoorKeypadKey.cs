using System.Collections;
using UnityEngine;

public class DoorKeypadKey : MonoBehaviour
{
    [Tooltip("Valore associato al tasto: '0'-'9', '*' per reset, '#' per conferma")]
    public string keyValue = "1";

    [Tooltip("Distanza di rientro quando il tasto viene premuto (in metri)")]
    public float pressDepth = 0.003f;
    public float pressSpeed = 20f;

    [Header("Audio pressione tasto")]
    [Tooltip("Se vuoto, usa l'AudioSource del DoorKeypadController padre")]
    public AudioSource keyAudioSource;
    public AudioClip pressSound;

    private DoorKeypadController controller;
    private Vector3 restLocalPos;
    private Coroutine animRoutine;

    private void Awake()
    {
        restLocalPos = transform.localPosition;
        controller = GetComponentInParent<DoorKeypadController>();

        if (keyAudioSource == null && controller != null)
        {
            keyAudioSource = controller.audioSource;
        }

        if (string.IsNullOrEmpty(keyValue))
        {
            if (gameObject.name.StartsWith("Key_"))
            {
                string suffix = gameObject.name.Replace("Key_", "");
                if (suffix == "Star") keyValue = "*";
                else if (suffix == "Hash") keyValue = "#";
                else keyValue = suffix;
            }
        }
    }

    public void Press()
    {
        PlayKeySound();

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(PressAnimation());

        if (controller != null)
        {
            controller.ReceiveKey(keyValue);
        }
    }

    private void PlayKeySound()
    {
        if (keyAudioSource != null && pressSound != null)
        {
            keyAudioSource.PlayOneShot(pressSound);
        }
    }

    private IEnumerator PressAnimation()
    {
        Vector3 pressedPos = restLocalPos + new Vector3(0f, pressDepth, 0f);

        while (Vector3.Distance(transform.localPosition, pressedPos) > 0.0001f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, pressedPos, pressSpeed * Time.deltaTime);
            yield return null;
        }

        while (Vector3.Distance(transform.localPosition, restLocalPos) > 0.0001f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, restLocalPos, pressSpeed * Time.deltaTime);
            yield return null;
        }

        transform.localPosition = restLocalPos;
    }
}