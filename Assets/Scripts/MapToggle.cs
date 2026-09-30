using UnityEngine;

public class MapToggle : MonoBehaviour
{
    public RectTransform mapPanel;       // The map UI panel
    public RectTransform smallMapRect;   // Size for small map
    public RectTransform fullMapRect;    // Size for fullscreen map
    public Camera mapCamera;

    private ExamplePlayerController playerController;

    private MapState state = MapState.Closed;

    private void Awake()
    {
        if (playerController == null)
            playerController = FindObjectOfType<ExamplePlayerController>();

        if (playerController == null)
            Debug.LogWarning("MapToggle: Could not find ExamplePlayerController in the scene.");
    }

    private void FindPlayerController()
    {
        if (playerController != null)
            return;

        playerController = FindObjectOfType<ExamplePlayerController>();

        if (playerController == null)
            Debug.LogWarning("MapToggle: Could not find ExamplePlayerController in the scene.");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            CycleMapState();
        }
    }

    void CycleMapState()
    {
        FindPlayerController(); // auto-locate the player controller
        
        switch (state)
        {
            case MapState.Closed:
                mapPanel.gameObject.SetActive(true);
                ApplyRect(smallMapRect);
                MapCameraFollow.SetFollowMode(true); // follow player
                if (playerController != null) 
                    playerController.movementLocked = false;
                CursorManager.Hide();
                state = MapState.Small;
                break;

            case MapState.Small:
                ApplyRect(fullMapRect);
                MapCameraFollow.SetFollowMode(false); // static overview
                // or reference your map camera directly 
                mapCamera.orthographicSize = 17f;
                if (mapCamera.TryGetComponent<MapCameraFollow>(out var follow) && follow.mapCenter != null) 
                { 
                    var center = follow.mapCenter.position; 
                    mapCamera.transform.position = new Vector3(center.x, mapCamera.transform.position.y, center.z); 
                }
                if (playerController != null) 
                    playerController.movementLocked = true;
                CursorManager.Show();
                state = MapState.Fullscreen;
                break;

            case MapState.Fullscreen:
                mapPanel.gameObject.SetActive(false);
                playerController.movementLocked = false; // allow movement again
                if (playerController != null) 
                    playerController.movementLocked = false;
                CursorManager.Hide();
                state = MapState.Closed;
                break;
        }
    }

    void ApplyRect(RectTransform preset)
    {
        // Copy anchors
        mapPanel.anchorMin = preset.anchorMin;
        mapPanel.anchorMax = preset.anchorMax;

        // Copy pivot
        mapPanel.pivot = preset.pivot;

        // Copy position + size
        mapPanel.anchoredPosition = preset.anchoredPosition;
        mapPanel.sizeDelta = preset.sizeDelta;

        // Copy rotation and scale (important!)
        mapPanel.localRotation = preset.localRotation;
        mapPanel.localScale = preset.localScale;
    }
}


public enum MapState
{
    Closed,
    Small,
    Fullscreen
}
