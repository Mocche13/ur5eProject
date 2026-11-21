using UnityEngine;
using System.Collections.Generic;

public class RandomSpawner : MonoBehaviour
{
    [Header("Oggetti e Quantità")]
    public GameObject[] prefabsDaSpawnare;
    public int[] quantitaPerOggetto;

    [Header("Genitore Gerarchia")]
    [Tooltip("Trascina qui l'oggetto Batch. Se lasciato vuoto, userà l'oggetto su cui è montato questo script.")]
    public Transform contenitoreBatch;

    [Header("Area e Piano di Appoggio")]
    public Transform superficieTavolo;
    public Vector2 dimensioniTavolo = new Vector2(0.5f, 0.8f);
    public Vector3 offsetSpawn = Vector3.zero;
    public float altezzaPartenzaRaycast = 0.5f;
    public LayerMask layerPiano = ~0;

    [Header("Scala Oggetti")]
    public Vector3 scalaCustom = Vector3.one;

    [Header("Parametri di Spaziatura")]
    public float distanzaMinima = 0.08f;
    public int tentativiMassimi = 30;

    void Start()
    {
        GeneraOggettiRandomici();
    }

    void GeneraOggettiRandomici()
    {
        if (prefabsDaSpawnare.Length != quantitaPerOggetto.Length)
        {
            Debug.LogError("Il numero di prefabs e delle quantità deve essere uguale!");
            return;
        }

        // Se non assegni nulla nell'Inspector, usa questo stesso GameObject come padre
        Transform parentTarget = contenitoreBatch != null ? contenitoreBatch : transform;

        Vector3 centroArea = superficieTavolo
            ? superficieTavolo.position + offsetSpawn
            : transform.position + offsetSpawn;

        List<Vector3> posizioniOccupate = new List<Vector3>();

        for (int i = 0; i < prefabsDaSpawnare.Length; i++)
        {
            int quantita = quantitaPerOggetto[i];
            GameObject prefab = prefabsDaSpawnare[i];

            if (prefab == null) continue;

            for (int q = 0; q < quantita; q++)
            {
                bool trovato = false;
                Vector3 posizioneAppoggio = Vector3.zero;

                for (int tentativo = 0; tentativo < tentativiMassimi; tentativo++)
                {
                    float randomX = Random.Range(-dimensioniTavolo.x / 2f, dimensioniTavolo.x / 2f);
                    float randomZ = Random.Range(-dimensioniTavolo.y / 2f, dimensioniTavolo.y / 2f);

                    Vector3 origineRaggio = new Vector3(
                        centroArea.x + randomX,
                        centroArea.y + altezzaPartenzaRaycast,
                        centroArea.z + randomZ
                    );

                    if (Physics.Raycast(origineRaggio, Vector3.down, out RaycastHit hit, altezzaPartenzaRaycast * 2f, layerPiano))
                    {
                        if (superficieTavolo != null && hit.transform != superficieTavolo && !hit.transform.IsChildOf(superficieTavolo))
                        {
                            continue;
                        }

                        Vector3 candidatoXZ = new Vector3(hit.point.x, 0f, hit.point.z);

                        bool troppoVicino = false;
                        foreach (Vector3 posEsistente in posizioniOccupate)
                        {
                            Vector3 esistenteXZ = new Vector3(posEsistente.x, 0f, posEsistente.z);
                            if (Vector3.Distance(candidatoXZ, esistenteXZ) < distanzaMinima)
                            {
                                troppoVicino = true;
                                break;
                            }
                        }

                        if (!troppoVicino)
                        {
                            posizioneAppoggio = hit.point;
                            trovato = true;
                            break;
                        }
                    }
                }

                if (trovato)
                {
                    posizioniOccupate.Add(posizioneAppoggio);

                    Quaternion rotazioneCasuale = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    GameObject istanza = Instantiate(prefab, posizioneAppoggio, rotazioneCasuale);
                    istanza.transform.localScale = scalaCustom;

                    // Calcola l'altezza del bounding box dopo aver applicato la scala
                    float offsetY = CalcolaMezzaAltezza(istanza);
                    istanza.transform.position = new Vector3(
                        posizioneAppoggio.x,
                        posizioneAppoggio.y + offsetY,
                        posizioneAppoggio.z
                    );

                    // Rende la capsula figlia di Batch mantenendo posizione e scala nel mondo
                    istanza.transform.SetParent(parentTarget, true);
                }
                else
                {
                    Debug.LogWarning($"Nessuna posizione libera trovata per {prefab.name}.");
                }
            }
        }
    }

    float CalcolaMezzaAltezza(GameObject obj)
    {
        Collider col = obj.GetComponentInChildren<Collider>();
        if (col != null)
        {
            return col.bounds.extents.y;
        }

        Renderer rend = obj.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            return rend.bounds.extents.y;
        }

        return 0f;
    }
}