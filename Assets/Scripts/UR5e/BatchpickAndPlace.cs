using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BatchPickAndPlace : MonoBehaviour
{
    [Header("Riferimenti")]
    [Tooltip("Trascina qui il robot che ha lo script RobotProgram.")]
    public RobotProgram robotProgram;

    [Tooltip("Trascina qui il testo TextMeshPro che contiene il numero di operazioni da eseguire.")]
    public TMP_Text quantityText;

    [Header("Impostazioni Sequenza")]
    [Tooltip("Pausa in secondi tra un ciclo di Pick & Place e il successivo.")]
    public float delayBetweenItems = 0.5f;

    private bool isBatchRunning = false;
    public bool IsBatchRunning => isBatchRunning;

    /// <summary>
    /// Collega questo metodo all'OnClick() del tuo bottone UI per avviare
    /// il Pick & Place automatico sul numero di capsule indicato nel TextMeshPro.
    /// </summary>
    public void StartBatchPickAndPlace()
    {
        if (robotProgram == null)
        {
            Debug.LogError("[BatchPickAndPlace] RobotProgram non assegnato nell'Inspector!", this);
            return;
        }

        if (isBatchRunning || robotProgram.IsRunning)
        {
            Debug.LogWarning("[BatchPickAndPlace] Un ciclo è già in esecuzione.", this);
            return;
        }

        if (transform.childCount == 0)
        {
            Debug.LogWarning("[BatchPickAndPlace] Nessun oggetto figlio trovato dentro " + gameObject.name, this);
            return;
        }

        // Legge il numero dal componente TextMeshPro
        int requestedCount = 0;
        if (quantityText != null)
        {
            int.TryParse(quantityText.text.Trim(), out requestedCount);
        }

        // Se il valore letto è <= 0, non avvia nulla
        if (requestedCount <= 0) return;

        StartCoroutine(BatchRoutine(requestedCount));
    }

    private IEnumerator BatchRoutine(int maxItemsToProcess)
    {
        isBatchRunning = true;

        // 1. Raccoglie i figli attivi fino al massimo richiesto dall'utente
        // (se requestedCount supera i figli disponibili, si ferma silenziosamente ai figli presenti)
        List<Transform> itemsToPick = new List<Transform>();
        for (int i = 0; i < transform.childCount && itemsToPick.Count < maxItemsToProcess; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.activeInHierarchy)
            {
                itemsToPick.Add(child);
            }
        }

        // 2. Esegue il ciclo Pick & Place per ogni oggetto selezionato
        for (int i = 0; i < itemsToPick.Count; i++)
        {
            Transform currentItem = itemsToPick[i];
            if (currentItem == null) continue;

            robotProgram.liveTarget = currentItem;
            robotProgram.RunPickAndPlace();

            yield return null;

            while (robotProgram.IsRunning)
            {
                yield return null;
            }

            if (delayBetweenItems > 0f && i < itemsToPick.Count - 1)
            {
                yield return new WaitForSeconds(delayBetweenItems);
            }
        }

        isBatchRunning = false;
    }
}
