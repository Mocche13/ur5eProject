using UnityEngine;

public class RandomSpawner : MonoBehaviour
{
    [Header("Oggetti e Quantità")]
    public GameObject[] prefabsDaSpawnare; // Array dei tuoi oggetti (es. Capsula A, B, C...)
    public int[] quantitaPerOggetto;      // Quantità N corrispondente per ciascun oggetto

    [Header("Area del Tavolo")]
    public Transform superficieTavolo;    // Il piano del tavolo in Unity
    public Vector2 dimensioniTavolo = new Vector2(1.0f, 1.0f); // Dimensioni (larghezza, profondità) dell'area di spawn
    public Vector3 offsetSpawn = Vector3.zero; // Spostamento del centro dell'area rispetto al tavolo
    public float altezzaSpawn = 0.05f;    // Altezza sopra il tavolo per lo spawn

    [Header("Scala Oggetti")]
    public Vector3 scalaCustom = Vector3.one; // Eventuale scala forzata per le capsule

    [Header("Parametri di Spaziatura")]
    public float distanzaMinima = 0.1f;    // Distanza minima tra un oggetto e l'altro
    public int tentativiMassimi = 30;     // Tentativi massimi per trovare una posizione libera

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

        // Calcola il centro effettivo considerando l'offset impostato nell'Inspector
        Vector3 centroTavolo = superficieTavolo ? superficieTavolo.position + offsetSpawn : transform.position + offsetSpawn;

        // Lista per tenere traccia delle posizioni già occupate
        System.Collections.Generic.List<Vector3> posizioniGenerate = new System.Collections.Generic.List<Vector3>();

        for (int i = 0; i < prefabsDaSpawnare.Length; i++)
        {
            int quantita = quantitaPerOggetto[i];
            GameObject prefab = prefabsDaSpawnare[i];

            for (int q = 0; q < quantita; q++)
            {
                Vector3 posizioneFinale = Vector3.zero;
                bool posizioneTrovata = false;

                // Tenta di trovare una posizione casuale libera
                for (int tentativo = 0; tentativo < tentativiMassimi; tentativo++)
                {
                    // Genera coordinate casuali entro i limiti dell'area definita
                    float randomX = Random.Range(-dimensioniTavolo.x / 2f, dimensioniTavolo.x / 2f);
                    float randomZ = Random.Range(-dimensioniTavolo.y / 2f, dimensioniTavolo.y / 2f);

                    Vector3 candidato = centroTavolo + new Vector3(randomX, altezzaSpawn, randomZ);

                    // Controlla la distanza dagli altri oggetti già piazzati
                    bool troppoVicino = false;
                    foreach (Vector3 posEsistente in posizioniGenerate)
                    {
                        if (Vector3.Distance(new Vector3(candidato.x, 0, candidato.z), new Vector3(posEsistente.x, 0, posEsistente.z)) < distanzaMinima)
                        {
                            troppoVicino = true;
                            break;
                        }
                    }

                    if (!troppoVicino)
                    {
                        posizioneFinale = candidato;
                        posizioneTrovata = true;
                        break;
                    }
                }

                // Se ha trovato una posizione valida, istanzia l'oggetto
                if (posizioneTrovata)
                {
                    posizioniGenerate.Add(posizioneFinale);

                    // Rotazione casuale sull'asse Y per maggiore realismo
                    Quaternion rotazioneCasuale = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                    GameObject istanza = Instantiate(prefab, posizioneFinale, rotazioneCasuale);

                    // Applica la scala custom configurata nell'Inspector
                    istanza.transform.localScale = scalaCustom;
                }
                else
                {
                    Debug.LogWarning($"Impossibile trovare una posizione libera per {prefab.name} (limite tentativi raggiunto).");
                }
            }
        }
    }
}