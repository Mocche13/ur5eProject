using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BatchPickAndPlace : MonoBehaviour
{
    [Header("Riferimenti")]
    [Tooltip("Trascina qui il robot che ha lo script RobotProgram.")]
    public RobotProgram robotProgram;

    [Header("Impostazioni Sequenza")]
    [Tooltip("Pausa in secondi tra un ciclo di Pick & Place e il successivo.")]
    public float delayBetweenItems = 0.5f;

    private bool isBatchRunning = false;
    public bool IsBatchRunning => isBatchRunning;

    /// <summary>
    /// Collega questo metodo all'OnClick() del tuo bottone UI per avviare
    /// il Pick & Place automatico su tutti i figli di questo oggetto.
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

        StartCoroutine(BatchRoutine());
    }

    private IEnumerator BatchRoutine()
    {
        isBatchRunning = true;

        // 1. Salva tutti i figli in una lista fissa prima di iniziare
        // (fondamentale perché durante il Grab l'oggetto cambia genitore!)
        List<Transform> itemsToPick = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.activeInHierarchy)
            {
                itemsToPick.Add(child);
            }
        }

        // 2. Esegue il ciclo Pick & Place per ogni oggetto in coda
        for (int i = 0; i < itemsToPick.Count; i++)
        {
            Transform currentItem = itemsToPick[i];
            if (currentItem == null) continue;

            // Assegna l'oggetto corrente come target del robot
            robotProgram.liveTarget = currentItem;

            // Avvia la sequenza singola (MoveJ -> Grab -> Home -> Release)
            robotProgram.RunPickAndPlace();

            // Aspetta un frame per assicurarsi che robotProgram.IsRunning sia diventato true
            yield return null;

            // Attende finché il robot non ha completato l'intero ciclo per questo oggetto
            while (robotProgram.IsRunning)
            {
                yield return null;
            }

            // Pausa prima di passare all'oggetto successivo
            if (delayBetweenItems > 0f && i < itemsToPick.Count - 1)
            {
                yield return new WaitForSeconds(delayBetweenItems);
            }
        }

        isBatchRunning = false;
    }
}
