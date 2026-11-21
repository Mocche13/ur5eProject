using UnityEngine;
using TMPro; // Necessario per usare TextMeshPro

public class NumberSelector : MonoBehaviour
{
    [Header("Riferimenti UI")]
    public TextMeshProUGUI testoNumero;

    [Header("Impostazioni Valore")]
    public int valoreAttuale = 1;
    public int valoreMinimo = 0;
    public int valoreMassimo = 99;

    void Start()
    {
        UpdateText();
    }

    // Funzione da collegare al bottone +
    public void Increment()
    {
        if (valoreAttuale < valoreMassimo)
        {
            valoreAttuale++;
            UpdateText();
        }
    }

    // Funzione da collegare al bottone -
    public void Decrement()
    {
        if (valoreAttuale > valoreMinimo)
        {
            valoreAttuale--;
            UpdateText();
        }
    }

    private void UpdateText()
    {
        if (testoNumero != null)
        {
            testoNumero.text = valoreAttuale.ToString();
        }
    }
}
