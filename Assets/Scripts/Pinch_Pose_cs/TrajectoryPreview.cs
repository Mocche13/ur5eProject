using UnityEngine;

public class TrajectoryPreview : MonoBehaviour
{
    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.cyan;
        lineRenderer.endColor = Color.cyan;
        lineRenderer.startWidth = 0.005f;
        lineRenderer.endWidth = 0.005f;
    }

    public void DrawWaypoints(Vector3[] waypoints)
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }
        lineRenderer.positionCount = waypoints.Length;
        lineRenderer.SetPositions(waypoints);
    }

    public void Clear()
    {
        lineRenderer.positionCount = 0;
    }
}