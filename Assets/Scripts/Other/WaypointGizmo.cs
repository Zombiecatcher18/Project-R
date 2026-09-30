using UnityEngine;

public class WaypointGizmo : MonoBehaviour
{
    public Color gizmoColor = Color.yellow;
    public float gizmoRadius = 0.3f;

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
    }
}
