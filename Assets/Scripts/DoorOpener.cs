using UnityEngine;

public class DoorOpener : MonoBehaviour
{
    [Header("Impostazioni Rotazione")]
    public Transform portaPivot;          // Cardine della porta (se vuoto, usa questo transform)
    public float angoloApertura = 90f;    // Gradi di apertura (es. 90 o -90)
    public float velocitaApertura = 3f;   // Velocità di rotazione
    public bool apriAutomaticamente = false;

    private Quaternion rotazioneChiusa;
    private Quaternion rotazioneAperta;
    private bool staAprendo = false;

    void Start()
    {
        if (portaPivot == null) portaPivot = transform;

        rotazioneChiusa = portaPivot.localRotation;
        rotazioneAperta = rotazioneChiusa * Quaternion.Euler(0f, angoloApertura, 0f);

        if (apriAutomaticamente)
        {
            ApriPorta();
        }
    }

    void Update()
    {
        if (staAprendo)
        {
            portaPivot.localRotation = Quaternion.Slerp(
                portaPivot.localRotation,
                rotazioneAperta,
                Time.deltaTime * velocitaApertura
            );
        }
    }

    public void ApriPorta()
    {
        staAprendo = true;
    }

    public void ChiudiPorta()
    {
        staAprendo = false;
        portaPivot.localRotation = rotazioneChiusa;
    }
}