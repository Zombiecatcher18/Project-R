using UnityEngine;

public class MapCameraControls : MonoBehaviour
{
    public float zoomSpeed = 5f;
    public float minZoom = 5f;
    public float maxZoom = 40f;

    public float panSpeed = 0.5f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        // Only active in fullscreen mode
        if (!MapCameraFollow.followPlayer)
        {
            HandleZoom();
            HandlePan();
        }
    }

    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0)
        {
            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }

    void HandlePan()
    {
        // LEFT CLICK DRAG
        if (Input.GetMouseButton(0))
        {
            float moveX = -Input.GetAxis("Mouse X") * panSpeed;
            float moveZ = -Input.GetAxis("Mouse Y") * panSpeed;

            transform.position += new Vector3(moveX, 0, moveZ);
        }
    }
}
