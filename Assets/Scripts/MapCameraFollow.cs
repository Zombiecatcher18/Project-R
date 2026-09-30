using UnityEngine;

public class MapCameraFollow : MonoBehaviour
{
    private Transform player;

    [Header("Follow Mode")]
    public float followHeight = 50f;

    [Header("Static Overview Mode")]
    public static bool followPlayer = true;
    public static float staticHeight = 50f;

    [Header("Map Center")]
    public Transform mapCenter;   // assign in Inspector if you use it elsewhere

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        // OPTIMIZATION: Cache player on Start instead of searching in LateUpdate
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
                player = p.transform;
        }
    }

    void LateUpdate()
    {
        if (!followPlayer)
        {
            // Fullscreen / static mode:
            // DO NOTHING HERE, MapCameraControls will handle position
            return;
        }

        // FOLLOW MODE
        if (player == null)
        {
            // OPTIMIZATION: Only search if not found in Start (fallback for dynamic player spawn)
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
                player = p.transform;
            else
                return;
        }

        Vector3 followPos = player.position;
        followPos.y += followHeight;
        transform.position = followPos;
    }

    public static void SetFollowMode(bool follow)
    {
        followPlayer = follow;
    }
}
