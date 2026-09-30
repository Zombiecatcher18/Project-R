using UnityEngine;

/// <summary>
/// Detects player proximity and triggers enemy pursuit/battle initiation.
/// Attached to trigger collider child of enemy patrol GameObject.
/// Implements continuous detection with overlap sphere refinement for robust player tracking.
/// 
/// Detection System:
/// 1. OnTriggerEnter/Exit: Broad detection via collider overlap (low-cost)
/// 2. Update: Continuous overlap sphere check for movement-during-trigger accuracy
/// 3. ForceDetect(): Triggers pursuit mode when player confirmed in detection zone
/// 
/// Safety Features:
/// - Respects EnemyScript battle cooldown (prevents spam battles)
/// - Respects runAwayLock (prevents re-engagement during retreat)
/// - Null-safe component checks throughout
/// - Tag comparison for player validation
/// 
/// Integration:
/// Called by: Physics system (OnTrigger) and internal Update()
/// Updates: EnemyMovement.player, EnemyMovement.isPlayerDetected
/// Triggers: EnemyMovement.OnPlayerDetected() and OnPlayerLost()
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyDetection : MonoBehaviour
{
    // ===== COMPONENT REFERENCES =====
    /// <summary>
    /// Parent enemy script component (provides cooldown checks and battle logic).
    /// Set via GetComponentInParent in Start().
    /// </summary>
    private EnemyScript enemyScript;

    /// <summary>
    /// Parent enemy movement component (receives detection updates and player reference).
    /// Set via GetComponentInParent in Start().
    /// </summary>
    private EnemyMovement enemyMovement;
    private EnemyWandering enemyWandering;

    /// <summary>
    /// Current player transform (set by OnTriggerEnter, cleared by OnTriggerExit).
    /// Used for overlap sphere position and movement component updates.
    /// </summary>
    private Transform player;

    // ===== DETECTION STATE =====
    /// <summary>
    /// Flag indicating player is currently in trigger collider zone.
    /// Set true by OnTriggerEnter, false by OnTriggerExit.
    /// Controls whether Update() performs continuous detection checks.
    /// </summary>
    private bool playerInsideTrigger = false;

    /// <summary>
    /// Flag preventing duplicate ForceDetect() calls within same trigger stay.
    /// Set true immediately after ForceDetect(), reset on OnTriggerExit.
    /// Prevents multiple simultaneous detections and redundant callbacks.
    /// </summary>
    private bool forcedDetectedThisStay = false;

    // ===== DETECTION PARAMETERS =====
    /// <summary>
    /// Overlap sphere radius for continuous player detection during trigger stay.
    /// Default 0.6f matches typical player collider size.
    /// Larger radius = more forgiving detection, smaller = more precise.
    /// </summary>
    [SerializeField] private float overlapRadius = 0.6f;

    /// <summary>
    /// Physics layer mask for overlap sphere queries (optional optimization).
    /// If set to 0 (LayerMask.Nothing), queries all layers (default behavior).
    /// If configured, only checks specified layers for performance on large scenes.
    /// </summary>
    [SerializeField] private LayerMask playerLayer;

    // ===== PERFORMANCE OPTIMIZATION =====
    /// <summary>
    /// Frame counter for skipping overlap sphere queries (performance optimization).
    /// Reduces expensive physics queries while maintaining detection accuracy.
    /// OnTriggerEnter/Exit still trigger immediately for responsiveness.
    /// </summary>
    private int detectionFrameCounter = 0;

    /// <summary>
    /// How many frames to skip between overlap sphere checks.
    /// Default 3 = check every 3rd frame (66% reduction in physics queries).
    /// Increase for better performance on slow devices (e.g., 5-6 on mobile).
    /// Decrease for more responsive detection (1 = every frame, 2 = every other frame).
    /// </summary>
    [SerializeField][Range(1, 10)] private int detectionFrameSkip = 3;

    // ===== INITIALIZATION =====
    /// <summary>
    /// Unity lifecycle: Cache component references and validate trigger setup.
    /// 
    /// Actions:
    /// 1. GetComponentInParent() for EnemyMovement and EnemyScript
    /// 2. Validate collider is set to isTrigger (required for OnTrigger callbacks)
    /// 3. Log warning if collider is solid (collision-based, not trigger)
    /// 
    /// Called by: Physics system at scene initialization
    /// </summary>
    void Start()
    {
        enemyMovement = GetComponentInParent<EnemyMovement>();
        enemyWandering = GetComponentInParent<EnemyWandering>();
        enemyScript = GetComponentInParent<EnemyScript>();

        Collider c = GetComponent<Collider>();
        if (c != null && !c.isTrigger)
            Debug.LogWarning("[EnemyDetection] Collider should be set as Trigger.");
    }

    // ===== CONTINUOUS DETECTION =====
    /// <summary>
    /// Per-frame detection check using overlap sphere during trigger stay.
    /// Provides continuous position updates without relying solely on trigger enter/exit.
    /// Handles cases where player might be moving/teleporting within trigger zone.
    /// 
    /// Logic:
    /// 1. Skip if not in trigger or already detected this frame
    /// 2. Skip if missing critical components (player, enemyScript)
    /// 3. Skip if enemy is in cooldown or run-away lock state
    /// 4. Query overlap sphere at player position with configurable radius (FRAME-SKIPPED)
    /// 5. Check each overlapping collider for "Player" tag
    /// 6. Call ForceDetect() if player collider found
    /// 
    /// Performance: Only runs if playerInsideTrigger=true (very selective).
    /// OPTIMIZATION: Frame-skipping reduces overlap sphere queries by ~66% (every 3rd frame).
    /// OnTriggerEnter/Exit still respond immediately for responsiveness.
    /// Continuous sphere queries are O(n) where n = nearby colliders (usually 1-3).
    /// </summary>
    void Update()
    {
        // Skip if conditions don't support detection
        if (!playerInsideTrigger || forcedDetectedThisStay) return;
        if (player == null || enemyScript == null) return;

        // Respect state locks: cooldown prevents spam battles, runAwayLock prevents re-engagement
        if (enemyScript.IsBattleCooldownActive() || enemyScript.runAwayLockActive) return;

        // OPTIMIZATION: Frame-skip expensive overlap sphere query
        detectionFrameCounter++;
        if (detectionFrameCounter % detectionFrameSkip != 0)
            return;

        // Perform overlap sphere query at player position
        // If playerLayer configured, only query that layer; otherwise query all layers
        Collider[] hits = (playerLayer != 0)
            ? Physics.OverlapSphere(player.position, overlapRadius, playerLayer)
            : Physics.OverlapSphere(player.position, overlapRadius);

        // Check if any overlapping collider is player (by tag)
        foreach (var h in hits)
        {
            if (h.CompareTag("Player"))
            {
                ForceDetect();
                break;  // Found player, can exit loop
            }
        }
    }

    // ===== DETECTION TRIGGER =====
    /// <summary>
    /// Forces immediate player detection and pursuit mode activation.
    /// Called by both OnTriggerEnter and Update() when conditions align.
    /// 
    /// Actions:
    /// 1. Validate all required components and state exist
    /// 2. Respect cooldown and runAwayLock (prevent redundant engagement)
    /// 3. Set enemyMovement.player reference to enable following
    /// 4. Set enemyMovement.isPlayerDetected = true (pursuit mode flag)
    /// 5. Trigger enemyMovement.OnPlayerDetected() callback (enables pathfinding, etc.)
    /// 6. Set forcedDetectedThisStay to prevent duplicate calls
    /// 
    /// Called by: OnTriggerEnter (initial detection) and Update() (continuous verification)
    /// Impact: Initiates enemy pursuit behavior and can trigger BattleManager if configured
    /// </summary>
    private void ForceDetect()
    {
        if (enemyScript == null || player == null) return;
        if (enemyScript.IsBattleCooldownActive() || enemyScript.runAwayLockActive) return;

        // Patrol AI
        if (enemyMovement != null)
        {
            enemyMovement.player = player;
            enemyMovement.isPlayerDetected = true;
            enemyMovement.OnPlayerDetected();
        }

        // Wandering AI
        if (enemyWandering != null)
        {
            enemyWandering.PlayerDetected(player);
            enemyScript.EngageBattle(player.position);
        }

        forcedDetectedThisStay = true;
        Debug.Log($"[EnemyDetection] {enemyScript.enemyID} detected player!");
    }

    // ===== TRIGGER ENTER =====
    /// <summary>
    /// Physics callback: Player entered detection trigger zone.
    /// Caches player transform and initiates detection if state permits.
    /// 
    /// Actions:
    /// 1. Filter by "Player" tag (ignore other colliders)
    /// 2. Set playerInsideTrigger = true (enables Update() checks)
    /// 3. Cache player transform reference
    /// 4. Reset forcedDetectedThisStay flag (allows detection this trigger session)
    /// 5. Respect cooldown/runAwayLock before calling ForceDetect()
    /// 6. Call ForceDetect() if conditions permit
    /// 
    /// Called by: Physics system on trigger enter event
    /// Result: Begins continuous detection phase in Update()
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("[TEST] Trigger fired on " + gameObject.name + " by " + other.name);
        if (!other.CompareTag("Player")) return;

        // Player entered detection zone
        playerInsideTrigger = true;
        player = other.transform;
        forcedDetectedThisStay = false;

        // Check state locks before triggering detection
        if (enemyScript != null && (enemyScript.IsBattleCooldownActive() || enemyScript.runAwayLockActive))
            return;

        ForceDetect();
    }

    // ===== TRIGGER EXIT =====
    /// <summary>
    /// Physics callback: Player exited detection trigger zone.
    /// Clears detection state and triggers OnPlayerLost behavior callback.
    /// 
    /// Actions:
    /// 1. Filter by "Player" tag (ignore other colliders)
    /// 2. Set playerInsideTrigger = false (disables Update() checks)
    /// 3. Clear player transform reference
    /// 4. Reset forcedDetectedThisStay flag (allows re-detection if player re-enters)
    /// 5. Call enemyMovement.OnPlayerLost() to halt pursuit (pathfinding stop, etc.)
    /// 6. Clear pursuit mode flag (isPlayerDetected = false)
    /// 
    /// Called by: Physics system on trigger exit event
    /// Result: Enemy stops pursuing, returns to idle/patrol behavior
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Player left detection zone
        playerInsideTrigger = false;
        player = null;
        forcedDetectedThisStay = false;
        detectionFrameCounter = 0;  // OPTIMIZATION: Reset frame counter when leaving trigger

        // Notify movement component that player is lost
        if (enemyMovement != null)
        {
            enemyMovement.isPlayerDetected = false;
            enemyMovement.OnPlayerLost();
        }

        if (enemyWandering != null)
        {
            enemyWandering.PlayerLost(transform.position);
        }
    }
}
