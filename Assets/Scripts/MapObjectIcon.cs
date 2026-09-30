using Unity.VisualScripting;
using UnityEngine;

public class MapObjectIcon : MonoBehaviour
{
    public RectTransform icon;        // UI icon
    public Transform target;          // World object to track
    public RectTransform mapRect;     // SAME rect as player icon
    public Camera mapCamera;          // SAME camera as player icon
    
    // OPTIMIZATION: Cache Renderer to avoid GetComponentInChildren call every LateUpdate
    private Renderer cachedRenderer;

    private void OnEnable()
    {
        // Cache renderer when target is set
        if (target != null && cachedRenderer == null)
            cachedRenderer = target.GetComponentInChildren<Renderer>();
    }

    void LateUpdate()
    {
        if (target == null)
        {
            icon.gameObject.SetActive(false);
            return;
        }

        // If the target exists but is inactive or hidden → hide icon 
        if (!target.gameObject.activeInHierarchy)
        { 
            icon.gameObject.SetActive(false); 
            return; 
        }

        // OPTIMIZATION: Use cached renderer instead of GetComponentInChildren every frame
        if (cachedRenderer == null)
            cachedRenderer = target.GetComponentInChildren<Renderer>();
        
        if (cachedRenderer != null && !cachedRenderer.enabled)
        {
            icon.gameObject.SetActive(false);
            return;
        }
        
        // Get world position
        Vector3 targetPos = target.position;

        // Get camera center
        Vector3 camCenter = mapCamera.transform.position;
        camCenter.y = 0f; // ignore height

        // Offset from center
        Vector3 offset = targetPos - camCenter;

        // Convert world offset to UI pixels
        float pixelsPerUnit = mapRect.rect.width / (mapCamera.orthographicSize * 2f);
        Vector2 anchoredPos = new Vector2(offset.x, offset.z) * pixelsPerUnit;

        // Only update if changed
        if (icon.anchoredPosition != anchoredPos)
            icon.anchoredPosition = anchoredPos;

        // If you don’t want rotation, comment this out
        Vector3 newRot = new Vector3(0, 0, -target.eulerAngles.y);
        if (icon.localEulerAngles != newRot)
            icon.localEulerAngles = newRot;
    }
}
