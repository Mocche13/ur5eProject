using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine; // Pacchetto com.unity.ai.inference (v2.6.1)

// Struttura dati che rappresenta una singola capsula rilevata
[System.Serializable]
public struct DetectedCapsule
{
    public int classId;             // Indice classe YOLO (0..6)
    public string className;        // Nome classe (es. "AMM_PP")
    public float confidence;        // Confidenza del modello (0..1)
    public Rect boxPixels;          // Bounding box in pixel (su immagine 640x640)
    public Vector3 worldPosition;   // Posizione 3D stimata sul piano del tavolo
}

public class CapsuleVisionDetector : MonoBehaviour
{
    [Header("1. Modello AI (ONNX)")]
    [Tooltip("Trascina qui il file best.onnx importato in Unity")]
    public ModelAsset modelAsset;

    [Tooltip("Tipo di esecuzione: GPUCompute (veloce) o CPU (fallback)")]
    public BackendType backendType = BackendType.GPUCompute;

    [Header("2. Telecamera e Input")]
    [Tooltip("La VisionCamera che inquadra il tavolo")]
    public Camera visionCamera;

    [Tooltip("La RenderTexture 640x640 collegata alla VisionCamera")]
    public RenderTexture inputRenderTexture;

    [Header("3. Piano del Tavolo (Metodo A - Geometria)")]
    [Tooltip("Trascina qui il Transform del tavolo (useremo la sua altezza Y)")]
    public Transform tableSurface;

    [Tooltip("Offset verticale in metri rispetto al piano del tavolo (es. metà altezza capsula)")]
    public float graspHeightOffset = 0.0f;

    [Header("4. Parametri YOLO")]
    [Range(0.1f, 0.99f)]
    public float confidenceThreshold = 0.5f;

    [Range(0.1f, 0.99f)]
    public float iouThreshold = 0.45f; // Soglia per eliminare box sovrapposte (NMS)

    // Ordine ESATTO delle classi del nostro data.yaml (0..6)
    private readonly string[] classNames = new string[]
    {
        "AMM_PP",     // 0
        "BLUE_Comp",  // 1
        "DG_Paper",   // 2
        "DG_PP",      // 3
        "DIVA_PP",    // 4
        "N_Alu",      // 5
        "N_PLA"       // 6
    };

    private const int InputSize = 640; // Dimensione fissa del modello (640x640)

    // Risorse Inference Engine
    private Model runtimeModel;
    private Worker worker;
    private Tensor<float> inputTensor;

    // Lista pubblica in memoria con l'ultimo risultato della scansione
    [Header("5. Risultati Ultima Scansione (Solo Lettura)")]
    public List<DetectedCapsule> lastDetections = new List<DetectedCapsule>();

    void Start()
    {
        if (modelAsset == null)
        {
            Debug.LogError("[CapsuleVision] Manca il file best.onnx nell'Inspector!");
            return;
        }

        // 1. Carica il modello ONNX in memoria
        runtimeModel = ModelLoader.Load(modelAsset);

        // 2. Crea il Worker che eseguirà la rete neurale
        worker = new Worker(runtimeModel, backendType);

        // 3. Pre-alloca il tensore di input (Batch=1, Canali=3 RGB, H=640, W=640)
        inputTensor = new Tensor<float>(new TensorShape(1, 3, InputSize, InputSize));

        Debug.Log("[CapsuleVision] Modello ONNX caricato correttamente. Premi SPAZIO per testare una scansione.");
    }

    void Update()
    {
        // Test manuale rapido: premi la barra spaziatrice in Play Mode
        if (Input.GetKeyDown(KeyCode.Space))
        {
            DetectCapsules();
        }
    }

    /// <summary>
    /// Esegue l'inferenza sul frame attuale della VisionCamera e aggiorna lastDetections.
    /// </summary>
    public List<DetectedCapsule> DetectCapsules()
    {
        lastDetections.Clear();

        if (worker == null || visionCamera == null || inputRenderTexture == null)
        {
            Debug.LogWarning("[CapsuleVision] Controlla che ModelAsset, VisionCamera e RenderTexture siano assegnati!");
            return lastDetections;
        }

        // 1. Forza il rendering aggiornato della VisionCamera sulla RenderTexture
        visionCamera.Render();

        // 2. Converte la RenderTexture 640x640 nel Tensore di input (valori normalizzati 0..1)
        TextureConverter.ToTensor(inputRenderTexture, inputTensor, new TextureTransform().SetCoordOrigin(CoordOrigin.TopLeft));

        // 3. Esegue la rete neurale
        worker.Schedule(inputTensor);

        // 4. Legge l'output dalla GPU alla CPU: shape (1, 11, 8400)
        using Tensor<float> gpuOutput = worker.PeekOutput() as Tensor<float>;
        using Tensor<float> cpuOutput = gpuOutput.ReadbackAndClone();

        // 5. Decodifica le 8400 predizioni e applica NMS
        List<DetectedCapsule> candidates = ParseYoloOutput(cpuOutput);
        lastDetections = ApplyNMS(candidates, iouThreshold);

        // 6. Calcola la posizione 3D sul piano del tavolo per ogni capsula trovata
        for (int i = 0; i < lastDetections.Count; i++)
        {
            DetectedCapsule cap = lastDetections[i];
            cap.worldPosition = ProjectPixelToTablePlane(cap.boxPixels.center);
            lastDetections[i] = cap;
        }

        // Log di riepilogo in Console
        Debug.Log($"[CapsuleVision] Scansione completata: trovate {lastDetections.Count} capsule.");
        foreach (var cap in lastDetections)
        {
            Debug.Log($"  -> {cap.className} | Conf: {cap.confidence:P1} | Pos 3D: {cap.worldPosition:F3}");
        }

        return lastDetections;
    }

    /// <summary>
    /// Metodo pubblico per il robot: trova la capsula del tipo richiesto più vicina a un punto di riferimento.
    /// </summary>
    public bool TryGetClosestCapsule(string targetClassName, Vector3 referencePosition, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        float minDist = float.MaxValue;
        bool found = false;

        foreach (var cap in lastDetections)
        {
            if (cap.className == targetClassName)
            {
                float d = Vector3.Distance(referencePosition, cap.worldPosition);
                if (d < minDist)
                {
                    minDist = d;
                    worldPos = cap.worldPosition;
                    found = true;
                }
            }
        }
        return found;
    }

    /// <summary>
    /// Legge il tensore (1, 11, 8400) di YOLOv8 e filtra per confidenceThreshold.
    /// </summary>
    private List<DetectedCapsule> ParseYoloOutput(Tensor<float> output)
    {
        List<DetectedCapsule> results = new List<DetectedCapsule>();
        int numBoxes = output.shape[2]; // 8400
        int numClasses = classNames.Length; // 7

        for (int i = 0; i < numBoxes; i++)
        {
            // Trova la classe con lo score più alto tra gli indici 4..10
            int bestClass = -1;
            float bestScore = 0f;

            for (int c = 0; c < numClasses; c++)
            {
                float score = output[0, 4 + c, i];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestScore < confidenceThreshold)
                continue;

            // Coordinate YOLOv8 in pixel (0..640): centro X, centro Y (dall'alto), larghezza, altezza
            float cx = output[0, 0, i];
            float cy = output[0, 1, i];
            float w  = output[0, 2, i];
            float h  = output[0, 3, i];

            Rect box = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);

            results.Add(new DetectedCapsule
            {
                classId = bestClass,
                className = classNames[bestClass],
                confidence = bestScore,
                boxPixels = box,
                worldPosition = Vector3.zero
            });
        }

        return results;
    }

    /// <summary>
    /// Non-Maximum Suppression: elimina box duplicate sullo stesso oggetto.
    /// </summary>
    private List<DetectedCapsule> ApplyNMS(List<DetectedCapsule> detections, float iouThresh)
    {
        detections.Sort((a, b) => b.confidence.CompareTo(a.confidence));
        List<DetectedCapsule> finalResults = new List<DetectedCapsule>();

        while (detections.Count > 0)
        {
            DetectedCapsule best = detections[0];
            finalResults.Add(best);
            detections.RemoveAt(0);

            for (int i = detections.Count - 1; i >= 0; i--)
            {
                if (CalculateIoU(best.boxPixels, detections[i].boxPixels) > iouThresh)
                {
                    detections.RemoveAt(i);
                }
            }
        }
        return finalResults;
    }

    private float CalculateIoU(Rect a, Rect b)
    {
        float x1 = Mathf.Max(a.xMin, b.xMin);
        float y1 = Mathf.Max(a.yMin, b.yMin);
        float x2 = Mathf.Min(a.xMax, b.xMax);
        float y2 = Mathf.Min(a.yMax, b.yMax);

        float interArea = Mathf.Max(0, x2 - x1) * Mathf.Max(0, y2 - y1);
        float unionArea = (a.width * a.height) + (b.width * b.height) - interArea;

        return unionArea > 0 ? interArea / unionArea : 0f;
    }

    /// <summary>
    /// Metodo A: proietta il pixel 2D (0..640) sul piano orizzontale del tavolo in coordinate 3D.
    /// </summary>
    private Vector3 ProjectPixelToTablePlane(Vector2 pixelCenter)
    {
        // Normalizza in coordinate Viewport di Unity (0..1)
        // Attenzione: in YOLO Y=0 è in ALTO, in Unity Viewport Y=0 è in BASSO -> invertiamo Y!
        float vx = pixelCenter.x / InputSize;
        float vy = 1.0f - (pixelCenter.y / InputSize);

        Ray ray = visionCamera.ViewportPointToRay(new Vector3(vx, vy, 0f));

        // Definisce il piano orizzontale all'altezza del tavolo (+ eventuale offset)
        float planeY = (tableSurface != null ? tableSurface.position.y : 0f) + graspHeightOffset;
        Plane tablePlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));

        if (tablePlane.Raycast(ray, out float enterDistance))
        {
            return ray.GetPoint(enterDistance);
        }

        return Vector3.zero;
    }

    // Disegna sfere verdi nella Scene View sui punti 3D calcolati (utilissimo per verificare la precisione!)
    void OnDrawGizmos()
    {
        if (lastDetections == null) return;
        Gizmos.color = Color.green;
        foreach (var cap in lastDetections)
        {
            Gizmos.DrawSphere(cap.worldPosition, 0.3f); // raggio 2 cm (regolabile se la scala è diversa)
        }
    }

    void OnDestroy()
    {
        // Libera sempre la memoria GPU quando si ferma il Play Mode!
        inputTensor?.Dispose();
        worker?.Dispose();
    }
}