using System.Collections.Generic;
using UnityEngine;

public class VisionBatchManager : MonoBehaviour
{
    [Header("1. Riferimenti Sistema Visione")]
    [Tooltip("Trascina qui l'oggetto che contiene lo script CapsuleVisionDetector")]
    public CapsuleVisionDetector visionDetector;

    [Header("2. Riferimenti Robot e Batch")]
    [Tooltip("Trascina qui l'oggetto vuoto 'batch' (in posizione globale 0, 0, 0)")]
    public Transform batchTransform;

    [Tooltip("Trascina qui la base del robot UR5e per ordinare le capsule dalla più vicina alla più lontana")]
    public Transform robotBase;

    [Header("3. Stato Attuale (Solo Lettura)")]
    [Tooltip("Ultima classe di capsula selezionata dall'utente tramite UI")]
    public string selectedCapsuleClass = "";

    /// <summary>
    /// Metodo pubblico chiamato dai bottoni della UI.
    /// Riceve il nome della capsula scelta (es. "DG_PP"), avvia la scansione
    /// e popola l'oggetto 'batch' con gli Empty Object ordinati per distanza.
    /// </summary>
    public void SelectAndScanCapsule(string targetClassName)
    {
        // Rimuove eventuali spazi vuoti scritti per sbaglio nell'Inspector del bottone
        selectedCapsuleClass = targetClassName.Trim();

        // Controllo di sicurezza sui riferimenti obbligatori
        if (visionDetector == null || batchTransform == null)
        {
            Debug.LogError("[VisionBatch] Mancano i riferimenti a CapsuleVisionDetector o a batch nell'Inspector!");
            return;
        }

        // 1. Svuota 'batch' da eventuali vecchi Empty Object rimasti da una scansione precedente
        ClearBatch();

        // 2. Avvia la visione artificiale per rilevare tutte le capsule presenti sul tavolo
        List<DetectedCapsule> allDetections = visionDetector.DetectCapsules();

        // 3. Tiene solo le capsule che corrispondono alla classe scelta dall'utente
        List<DetectedCapsule> matchingCapsules = new List<DetectedCapsule>();
        foreach (DetectedCapsule cap in allDetections)
        {
            if (cap.className == selectedCapsuleClass)
            {
                matchingCapsules.Add(cap);
            }
        }

        // Se non trova nessuna capsula di quel tipo, avvisa in Console e termina
        if (matchingCapsules.Count == 0)
        {
            Debug.LogWarning($"[VisionBatch] Nessuna capsula di tipo '{selectedCapsuleClass}' trovata sul tavolo.");
            return;
        }

        // 4. Ordina le capsule trovate dalla più vicina alla più lontana rispetto alla base del robot
        Vector3 referencePos = (robotBase != null) ? robotBase.position : Vector3.zero;
        matchingCapsules.Sort((a, b) =>
        {
            float distA = Vector3.Distance(referencePos, a.worldPosition);
            float distB = Vector3.Distance(referencePos, b.worldPosition);
            return distA.CompareTo(distB);
        });

        // 5. Crea un Empty Object per ogni capsula trovata e lo mette figlio di 'batch'
        for (int i = 0; i < matchingCapsules.Count; i++)
        {
            // Crea un GameObject vuoto (es. "Target_DG_PP_0" sarà la più vicina, "Target_DG_PP_1" la seconda, ecc.)
            GameObject targetObj = new GameObject($"Target_{selectedCapsuleClass}_{i}");

            // Assegna la posizione 3D calcolata dalla visione
            targetObj.transform.position = matchingCapsules[i].worldPosition;

            // Lo rende figlio di 'batch' mantenendo invariata la sua posizione globale nel mondo (true)
            targetObj.transform.SetParent(batchTransform, true);
        }

        Debug.Log($"[VisionBatch] Creati {matchingCapsules.Count} target in '{batchTransform.name}' per '{selectedCapsuleClass}' (ordinati dal più vicino). Pronto per l'avvio!");
    }

    /// <summary>
    /// Rimuove tutti i figli presenti dentro 'batch'.
    /// </summary>
    public void ClearBatch()
    {
        if (batchTransform == null) return;

        // Scorre i figli all'indietro per rimuoverli tutti senza saltare indici
        for (int i = batchTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = batchTransform.GetChild(i);
            // Scollega subito il figlio da 'batch' così childCount si azzera immediatamente nello stesso frame
            child.SetParent(null);
            // Elimina l'oggetto dalla scena
            Destroy(child.gameObject);
        }
    }
}
