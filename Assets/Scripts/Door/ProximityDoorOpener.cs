using UnityEngine;

public class ProximityDoorOpener : MonoBehaviour
{
    public enum AsseRotazione { X, Y, Z }

    [Header("Riferimenti")]
    public Transform portaPivot;
    public string tagGiocatore = "Player";

    [Header("Movimento Porta")]
    public AsseRotazione asseCardine = AsseRotazione.Z; // Cambia in X, Y o Z in base all'orientamento del pivot
    public float angoloApertura = 90f;
    public float velocitaRotazione = 3f;
    public bool chiudiQuandoTiAllontani = true;

    private Quaternion rotazioneChiusa;
    private Quaternion rotazioneAperta;
    private bool portaAperta = false;

    void Start()
    {
        if (portaPivot == null) portaPivot = transform;

        rotazioneChiusa = portaPivot.localRotation;
        Vector3 vettoreRotazione = GetVettoreAsse(asseCardine, angoloApertura);
        rotazioneAperta = rotazioneChiusa * Quaternion.Euler(vettoreRotazione);
    }

    void Update()
    {
        Quaternion targetRotazione = portaAperta ? rotazioneAperta : rotazioneChiusa;
        portaPivot.localRotation = Quaternion.Slerp(
            portaPivot.localRotation,
            targetRotazione,
            Time.deltaTime * velocitaRotazione
        );
    }

    private Vector3 GetVettoreAsse(AsseRotazione asse, float gradi)
    {
        switch (asse)
        {
            case AsseRotazione.X: return new Vector3(gradi, 0f, 0f);
            case AsseRotazione.Y: return new Vector3(0f, gradi, 0f);
            case AsseRotazione.Z: return new Vector3(0f, 0f, gradi);
            default: return new Vector3(0f, gradi, 0f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (ÈGiocatoreValido(other))
        {
            portaAperta = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (chiudiQuandoTiAllontani && ÈGiocatoreValido(other))
        {
            portaAperta = false;
        }
    }

    private bool ÈGiocatoreValido(Collider other)
    {
        if (string.IsNullOrEmpty(tagGiocatore)) return true;

        return other.CompareTag(tagGiocatore) ||
               (other.transform.root != null && other.transform.root.CompareTag(tagGiocatore));
    }
}