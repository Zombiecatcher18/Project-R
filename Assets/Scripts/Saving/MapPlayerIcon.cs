using UnityEngine;

public class MapPlayerIcon : MonoBehaviour
{
    public RectTransform icon;        // UI icon
    public Transform player;          // Player transform
    public RectTransform mapRect;     // RawImage rect
    public Camera mapCamera;          // MapCamera

    void LateUpdate()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
            return;
        }

        // Get world position
        Vector3 playerPos = player.position;

        // Get camera center
        Vector3 camCenter = mapCamera.transform.position;
        camCenter.y = 0f; // ignore height

        // Offset from center
        Vector3 offset = playerPos - camCenter;

        // Convert world offset to UI pixels
        float pixelsPerUnit = mapRect.rect.width / (mapCamera.orthographicSize * 2f);
        Vector2 anchoredPos = new Vector2(offset.x, offset.z) * pixelsPerUnit;

        // Only update if changed
        if (icon.anchoredPosition != anchoredPos)
            icon.anchoredPosition = anchoredPos;

        // Rotate icon to match player
        Vector3 newRot = new Vector3(0, 0, -player.eulerAngles.y);
        if (icon.localEulerAngles != newRot)
            icon.localEulerAngles = newRot;
    }
}
