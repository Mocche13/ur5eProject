using System.Collections;
using UnityEngine;

public class DoorKeypadController : MonoBehaviour
{
    [Header("Configurazione PIN")]
    public string correctPin = "1234";
    public int maxPinLength = 6;
    [Tooltip("Se true, verifica il PIN appena raggiunge la lunghezza di correctPin")]
    public bool autoSubmitOnFullLength = false;

    [Header("Porta")]
    public SmartDoorController doorController;

    [Header("LED di stato")]
    public Renderer statusLedRenderer;
    public Color idleColor = new Color(1f, 0.25f, 0.05f);
    public Color successColor = Color.green;
    public Color errorColor = Color.red;

    [Header("Audio Tastierino")]
    public AudioSource audioSource;
    public AudioClip pinCorrectSound;
    public AudioClip pinWrongSound;

    private string currentInput = "";
    private Material ledMatInstance;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (statusLedRenderer != null)
        {
            ledMatInstance = statusLedRenderer.material;
            SetLedColor(idleColor);
        }
    }

    public void ReceiveKey(string key)
    {
        if (key == "*")
        {
            currentInput = "";
            SetLedColor(idleColor);
            return;
        }

        if (key == "#")
        {
            VerifyPin();
            return;
        }

        if (currentInput.Length < maxPinLength)
        {
            currentInput += key;

            if (autoSubmitOnFullLength && currentInput.Length == correctPin.Length)
            {
                VerifyPin();
            }
        }
    }

    public void VerifyPin()
    {
        if (currentInput == correctPin)
        {
            currentInput = "";
            SetLedColor(successColor);
            PlayClip(pinCorrectSound);

            if (doorController != null)
            {
                doorController.UnlockAndOpen();
            }
        }
        else
        {
            currentInput = "";
            PlayClip(pinWrongSound);
            StartCoroutine(FlashErrorRoutine());
        }
    }

    private IEnumerator FlashErrorRoutine()
    {
        SetLedColor(errorColor);
        yield return new WaitForSeconds(1.0f);
        SetLedColor(idleColor);
    }

    private void SetLedColor(Color c)
    {
        if (ledMatInstance == null) return;

        if (ledMatInstance.HasProperty("_BaseColor"))
            ledMatInstance.SetColor("_BaseColor", c);
        if (ledMatInstance.HasProperty("_Color"))
            ledMatInstance.SetColor("_Color", c);
        if (ledMatInstance.HasProperty("_EmissionColor"))
            ledMatInstance.SetColor("_EmissionColor", c * 2.5f);
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}