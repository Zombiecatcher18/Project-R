using UnityEngine;

public class MapWaypointIcon : MonoBehaviour
{
    public RectTransform icon;
    public Transform target;          // The world object this icon represents
    public RectTransform mapRect;
    public Camera mapCamera;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 worldPos = target.position;

        Vector3 camCenter = mapCamera.transform.position;
        camCenter.y = 0f;

        Vector3 offset = worldPos - camCenter;

        float ppu = mapRect.rect.width / (mapCamera.orthographicSize * 2f);
        Vector2 anchoredPos = new Vector2(offset.x, offset.z) * ppu;

        if (icon.anchoredPosition != anchoredPos)
            icon.anchoredPosition = anchoredPos;
    }
}
