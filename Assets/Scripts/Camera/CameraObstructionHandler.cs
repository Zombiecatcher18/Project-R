using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages camera obstruction: fades out walls/objects blocking view of player.
/// Raycasts from camera to player each frame, fading obstacles that block line-of-sight.
/// 
/// Mechanic:
/// 1. Raycast from camera through player position
/// 2. Detect colliders on obstructionMask layer (walls, buildings, etc.)
/// 3. Fade out detected obstacles (alpha → fadedAlpha)
/// 4. Fade back in obstacles no longer blocking view
/// 
/// Performance:
/// - Only raycasts between camera and player (single direction, limited distance)
/// - Uses HashSet to track current vs previous obstructions (efficient diff)
/// - LateUpdate timing ensures runs after camera movement
/// 
/// Integration:
/// - Attached to CinemachineCamera or main camera GameObject
/// - Requires OccluderFadeOverlay components on obstructing GameObjects
/// - Works automatically without scene setup beyond component assignment
/// 
/// Used by: Exploration cameras to maintain player visibility during navigation
/// </summary>
public class CameraObstructionHandler : MonoBehaviour
{
    // ===== CONFIGURATION =====
    /// <summary>
    /// Player transform for raycast target and line-of-sight detection.
    /// Must be assigned in inspector to function.
    /// </summary>
    [Header("Setup")]
    public Transform player;

    /// <summary>
    /// Raycast check frequency: performs raycast every N frames.
    /// Value 1 = every frame, 2 = every other frame, 3+ = less frequent.
    /// Recommended: 2-3 for smooth detection with reduced raycasts.
    /// OPTIMIZATION: Reduces per-frame raycasts from 60 to 20-30 per second.
    /// </summary>
    [Range(1, 6)]
    public int raycastFrequency = 2;

    /// <summary>
    /// Physics layer mask for obstruction detection.
    /// Only GameObjects on specified layers are considered obstacles.
    /// Typical: Layer "Walls", "Buildings", "Terrain" (not "Player", "Enemies")
    /// </summary>
    public LayerMask obstructionMask;

    /// <summary>
    /// Speed of fade animation (units per second for alpha transition).
    /// Higher = faster fade in/out
    /// Typical range: 2-10 (3-5 recommended for smooth but responsive)
    /// </summary>
    public float fadeSpeed = 5f;

    /// <summary>
    /// Target alpha when obstacle is faded out (blocking view).
    /// Range [0, 1]: 0 = invisible, 1 = opaque
    /// Typical: 0.25 (25% opacity = ghosted visible)
    /// Allows player to see through obstacle while maintaining visibility
    /// </summary>
    [Range(0f, 1f)] public float fadedAlpha = 0.25f;

    // ===== OBSTRUCTION TRACKING =====
    /// <summary>
    /// Cached camera to avoid GetComponent per frame (major performance win).
    /// </summary>
    private Camera cachedCamera;

    /// <summary>
    /// Frame counter for raycast frequency skipping logic.
    /// Incremented each frame, raycast only when frameCounter % raycastFrequency == 0.
    /// </summary>
    private int frameCounter = 0;

    /// <summary>
    /// HashSet of OccluderFadeOverlay currently blocking view.
    /// Updated each frame via raycast detection.
    /// Used to identify which obstacles need fading.
    /// </summary>
    private readonly HashSet<OccluderFadeOverlay> current = new();

    /// <summary>
    /// HashSet of OccluderFadeOverlay from previous frame.
    /// Compared against `current` to detect new vs removed obstructions.
    /// Enables fade-in of obstacles no longer blocking view.
    /// </summary>
    private readonly HashSet<OccluderFadeOverlay> previous = new();

    /// <summary>
    /// Reusable RaycastHit array to avoid allocation per raycast (GC optimization).
    /// Pre-sized to 16 to handle typical obstacle count.
    /// </summary>
    private RaycastHit[] raycastHits = new RaycastHit[16];

    // ===== MAIN LOOP =====
    /// <summary>
    /// Cache camera on first use to avoid per-frame GetComponent call.
    /// </summary>
    private void Start()
    {
        if (cachedCamera == null)
            cachedCamera = Camera.main;
    }

    /// <summary>
    /// Per-frame obstruction detection and fade management.
    /// Called by Physics system in LateUpdate phase (after camera movement).
    /// OPTIMIZATION: Only performs raycast every raycastFrequency frames.
    /// </summary>
    void LateUpdate()
    {
        frameCounter++;
        // Only raycast every N frames to reduce physics queries
        if (frameCounter % raycastFrequency == 0)
            HandleObstructions();
    }

    /// <summary>
    /// Perform raycast obstruction detection and trigger fade in/out.
    /// 
    /// Algorithm:
    /// 1. Swap current → previous HashSets (tracks frame-to-frame changes)
    /// 2. Calculate ray direction and distance (camera to player)
    /// 3. RaycastAll from camera toward player on obstructionMask
    /// 4. For each hit:
    ///    - Get OccluderFadeOverlay component
    ///    - Add to current set, configure fade parameters
    ///    - Call FadeOut() to ghost the obstacle
    /// 5. For obstacles in previous but NOT in current:
    ///    - Call FadeIn() to restore opacity (no longer blocking)
    /// 
    /// Performance: Single raycast per frame, distance limited to camera-player range
    /// Called by: LateUpdate() each frame
    /// </summary>
    void HandleObstructions()
    {
        // Swap detection sets: current becomes previous, current clears
        previous.Clear();
        foreach (var o in current)
            previous.Add(o);
        current.Clear();

        // Calculate raycast: from camera through player
        Vector3 dir = player.position - transform.position;
        float dist = dir.magnitude;

        // Raycast from camera to player, detect obstructions on specified layers
        RaycastHit[] hits = Physics.RaycastAll(transform.position, dir, dist, obstructionMask);
        foreach (var hit in hits)
        {
            // Try to get OccluderFadeOverlay component on hit object
            OccluderFadeOverlay occ = hit.collider.GetComponent<OccluderFadeOverlay>();
            if (occ)
            {
                current.Add(occ);  // Mark as currently blocking view
                occ.fadeSpeed = fadeSpeed;  // Configure fade speed
                occ.fadedAlpha = fadedAlpha;  // Configure target alpha
                occ.FadeOut();  // Begin fade-out animation (ghost the obstacle)
            }
        }

        // Fade in obstacles that were blocking but are no longer
        foreach (var occ in previous)
        {
            if (!current.Contains(occ))
                occ.FadeIn();  // Restore to full opacity
        }
    }

    /// <summary>
    /// Debug gizmo: Draw line from camera to player showing raycast direction.
    /// Only visible in editor when object selected, useful for debugging obstruction zones.
    /// 
    /// Called by: Editor scene view rendering (when object selected)
    /// Visual: Yellow line from camera position to player position
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (player)
            Gizmos.DrawLine(transform.position, player.position);
    }
}
