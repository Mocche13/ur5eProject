using UnityEngine;
using System.Collections.Generic;

public class RandomSpawner : MonoBehaviour
{
    [Header("Oggetti e Quantità")]
    public GameObject[] prefabsDaSpawnare;
    public int[] quantitaPerOggetto;

    [Header("Area e Piano di Appoggio")]
    public Transform superficieTavolo;
    public Vector2 dimensioniTavolo = new Vector2(0.5f, 0.8f);
    public Vector3 offsetSpawn = Vector3.zero;
    public float altezzaPartenzaRaycast = 0.5f; // Altezza sopra il piano da cui parte il raggio
    public LayerMask layerPiano = ~0;           // Layer del tavolo (lascia Everything o imposta il layer del piano)

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

                    // Lancia un raggio verso il basso per trovare la superficie reale del tavolo
                    if (Physics.Raycast(origineRaggio, Vector3.down, out RaycastHit hit, altezzaPartenzaRaycast * 2f, layerPiano))
                    {
                        // Se è assegnato superficieTavolo, verifica che il colpo sia su quel tavolo o sui suoi figli
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