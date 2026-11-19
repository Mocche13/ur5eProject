using UnityEngine;
using TMPro; // Se usi TextMeshPro per il display del pad

public class KeypadController : MonoBehaviour
{
    [Header("Configurazione PIN")]
    public string pinCorretto = "1234";
    public int lunghezzaMassima = 4;

    [Header("Collegamenti")]
    public DoorOpener portaCollegata;
    public TMP_Text displayTesto;        // Opzionale: schermo/testo sopra il pad
    public float ritardoResetErrore = 1.2f;

    private string pinInserito = "";
    private bool bloccatoTemporaneamente = false;

    void Start()
    {
        AggiornaDisplay("PIN:");
    }

    // Chiama questa funzione passando "0", "1", ..., "9" dal pulsante
    public void PremiCifra(string cifra)
    {
        if (bloccatoTemporaneamente) return;
        if (pinInserito.Length >= lunghezzaMassima) return;

        pinInserito += cifra;
        AggiornaDisplay(pinInserito);

        // Controllo automatico non appena si raggiunge la lunghezza del PIN
        if (pinInserito.Length == pinCorretto.Length)
        {
            VerificaPin();
        }
    }

    public void Cancella()
    {
        if (bloccatoTemporaneamente) return;
        pinInserito = "";
        AggiornaDisplay("PIN:");
    }

    private void VerificaPin()
    {
        if (pinInserito == pinCorretto)
        {
            AggiornaDisplay("OK - APERTURA");
            bloccatoTemporaneamente = true;

            if (portaCollegata != null)
            {
                portaCollegata.ApriPorta();
            }
        }
        else
        {
            AggiornaDisplay("PIN ERRATO");
            bloccatoTemporaneamente = true;
            Invoke(nameof(ResetDopoErrore), ritardoResetErrore);
        }
    }

    private void ResetDopoErrore()
    {
        pinInserito = "";
        bloccatoTemporaneamente = false;
        AggiornaDisplay("PIN:");
    }

    private void AggiornaDisplay(string messaggio)
    {
        if (displayTesto != null)
        {
            displayTesto.text = messaggio;
        }
    }
}