using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy patrol and chase movement controller using NavMeshAgent with Rigidbody physics integration.
/// Implements four-state behavior machine: Patrol → Alert → Chase → Return to Patrol.
/// 
/// State Flow:
/// - Patrol: Cycles through waypoints, waits at each
/// - Alert: Focuses on player for alertDuration (1.5s), ready to chase if detection confirmed
/// - Chase: Pursues player with speed multiplier and burst mechanics
/// - Return: Returns to last waypoint when player lost
/// 
/// Physics Integration:
/// - NavMeshAgent for pathfinding and obstacle avoidance
/// - Rigidbody for gravity and physics-based velocity
/// - Manual velocity application for fine-tuned control
/// 
/// Called by: EnemyDetection for state transitions (OnPlayerDetected, OnPlayerLost)
/// References: EnemyScript for cooldown management, waypoint system for patrol points
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    // ===== MOVEMENT SETTINGS =====
    /// <summary>
    /// Base patrol speed (units per second, typically 3-5 for realistic walking pace).
    /// </summary>
    [Header("General Settings")]
    public float speed = 3f;

    /// <summary>
    /// Burst sprint speed for chase phase (typically 1.5-2.5x base speed).
    /// </summary>
    public float burstSpeed = 7f;

    /// <summary>
    /// Wait duration at each patrol waypoint before moving to next (in seconds).
    /// </summary>
    public float waitTimeAtPoint = 2f;

    // ===== PATROL CONFIGURATION =====
    /// <summary>
    /// Array of waypoint transforms for patrol route.
    /// Enemy cycles through waypoints in order: [0] → [1] → ... → [n] → [0] → ...
    /// Can be empty (no patrol), in which case enemy only alerts/chases.
    /// </summary>
    [Header("Patrol Settings")]
    public Transform[] waypoints;

    /// <summary>
    /// Current waypoint index in patrol cycle (0 to waypoints.Length-1).
    /// Incremented after reaching each waypoint (with wraparound).
    /// </summary>
    private int currentWaypointIndex = 0;

    // ===== COMPONENT REFERENCES =====
    /// <summary>
    /// Current player transform (set by EnemyDetection.OnTriggerEnter, cleared by OnTriggerExit).
    /// Used for alert/chase state to track player position.
    /// </summary>
    [Header("References")]
    public Transform player;

    /// <summary>
    /// NavMeshAgent component for pathfinding and obstacle avoidance.
    /// Configured in SetupAgent() with high-quality settings.
    /// </summary>
    private NavMeshAgent agent;

    /// <summary>
    /// Parent EnemyScript component for cooldown management and enemy identity.
    /// </summary>
    private EnemyScript enemyScript;

    /// <summary>
    /// Rigidbody component for physics-based velocity and gravity application.
    /// Manually updated via desiredVelocity from agent.
    /// </summary>
    private Rigidbody rb;

    // ===== MOVEMENT CONSTRAINTS =====
    /// <summary>
    /// Distance threshold for NavMeshAgent.remainingDistance (stopping point proximity).
    /// Triggers waypoint arrival or patrol point wait when within this distance.
    /// </summary>
    [Header("Return Settings")]
    public float stoppingDistance = 0.5f;

    // ===== PLAYER DETECTION STATE =====
    /// <summary>
    /// Flag set by EnemyDetection.OnTriggerEnter (true) and OnTriggerExit (false).
    /// Indicates whether player is currently in detection trigger zone.
    /// Controls state transitions during alert phase.
    /// </summary>
    [Header("Alert Settings")]
    [HideInInspector] public bool isPlayerDetected = false;

    /// <summary>
    /// Duration of alert phase before returning to patrol (if no chase triggered).
    /// Typical value: 1.5 seconds (gives time for player to be detected during alert).
    /// </summary>
    public float alertDuration = 1.5f;

    // ===== CHASE SETTINGS =====
    /// <summary>
    /// Speed multiplier during chase phase (applied to burstSpeed).
    /// Example: burstSpeed=7, multiplier=1.8 → chase speed = 12.6.
    /// Typically 1.3-2.0 for challenging but fair difficulty.
    /// </summary>
    [Header("Chase Settings")]
    public float chaseSpeedMultiplier = 1.8f;

    /// <summary>
    /// Duration of burst speed phase during chase (in seconds).
    /// After expiration, returns to normal chase speed (not burst).
    /// Creates dynamic speed variation (sprint then jog pattern).
    /// </summary>
    public float burstDuration = 1.5f;

    // ===== DEBUG =====
    /// <summary>
    /// Debug display: remaining time on battle cooldown (updated in Update).
    /// Shows in editor for debugging cooldown behavior.
    /// Automatically calculated from enemyScript.battleCooldownEndTime.
    /// </summary>
    [Header("Debug")]
    public float debugCooldownTimeLeft = 0f;

    // ===== STATE TRACKING =====
    /// <summary>
    /// Flag: currently waiting at patrol waypoint.
    /// Set true by WaitAtPatrolPoint(), prevents patrol behavior during wait.
    /// </summary>
    private bool isWaiting = false;

    /// <summary>
    /// Flag: currently chasing player (moving toward player.position).
    /// Set during Chase state, used for velocity application.
    /// </summary>
    private bool isChasing = false;

    /// <summary>
    /// Flag: currently in burst speed phase (higher speed movement).
    /// Set by BurstOfSpeed() coroutine, controls agent.speed setting.
    /// </summary>
    private bool isBursting = false;

    /// <summary>
    /// Cached base speed for speed resets (when burst ends, returns to baseSpeed).
    /// Set in Start() from speed field.
    /// </summary>
    private float baseSpeed;

    /// <summary>
    /// Reference to active alert phase coroutine (for stopping and cleanup).
    /// Null when no alert phase active.
    /// </summary>
    private Coroutine alertRoutine;

    /// <summary>
    /// Reference to active burst speed coroutine (for stopping and cleanup).
    /// Null when no burst active.
    /// </summary>
    private Coroutine burstRoutine;

    // ===== BEHAVIOR STATE MACHINE =====
    /// <summary>
    /// Four-state behavior machine:
    /// - Patrol: Follow waypoint route
    /// - Alert: Look toward player, prepare to chase
    /// - Chase: Pursue player with variable speed
    /// - Return: Return to last patrol waypoint
    /// </summary>
    private enum State { Patrol, Alert, Chase, Return }

    /// <summary>
    /// Current behavior state (default: Patrol at start).
    /// Transitions controlled by OnPlayerDetected(), OnPlayerLost(), and state logic.
    /// </summary>
    private State currentState = State.Patrol;

    // ===== IDENTITY =====
    /// <summary>
    /// Unique enemy identifier (set by EnemyScript, used for debug logging).
    /// </summary>
    public string enemyID;

    /// <summary>
    /// Deprecated: use EnemyScript.IsBattleCooldownActive() instead.
    /// Kept for reference compatibility.
    /// </summary>
    private bool battleCooldownActive = false;

    // ===== INITIALIZATION =====
    /// <summary>
    /// Unity lifecycle: Initialize agent, get components, set initial patrol waypoint.
    /// 
    /// Actions:
    /// 1. Get Rigidbody, NavMeshAgent, EnemyScript components via GetComponent()
    /// 2. Cache baseSpeed from speed field
    /// 3. Validate EnemyScript exists (log error if missing)
    /// 4. Call SetupAgent() to configure NavMeshAgent parameters
    /// 5. If waypoints exist, set initial destination to waypoints[0]
    /// </summary>
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        enemyScript = GetComponent<EnemyScript>();

        baseSpeed = speed;

        if (enemyScript == null)
            Debug.LogError($"{gameObject.name} is missing EnemyScript!");

        SetupAgent();

        // If GameManager has a stored cooldown for this enemy, we don't need to do anything here,
        // because EnemyScript will restore it in its Awake/Start.
        // But keep base behavior
        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[currentWaypointIndex].position);
    }

    // ===== BEHAVIOR LOOP =====
    /// <summary>
    /// Per-frame behavior routing and state management.
    /// 
    /// Actions:
    /// 1. Update debug cooldown display if EnemyScript exists
    /// 2. Switch to appropriate behavior method based on currentState
    /// 3. Dispatch to PatrolBehavior(), AlertBehavior(), ChaseBehavior(), or ReturnBehavior()
    /// 
    /// Called by: Physics system each frame
    /// </summary>
    void Update()
    {
        // Update debug cooldown time left if enemyScript exists
        if (enemyScript != null)
        {
            debugCooldownTimeLeft = Mathf.Max(0f, enemyScript.battleCooldownEndTime - Time.time);
        }

        switch (currentState)
        {
            case State.Patrol: PatrolBehavior(); break;
            case State.Chase: ChaseBehavior(); break;
            case State.Alert: AlertBehavior(); break;
            case State.Return: ReturnBehavior(); break;
        }
    }

    // ===== PHYSICS INTEGRATION =====
    /// <summary>
    /// Per-frame physics update: apply gravity and sync Rigidbody with NavMeshAgent.
    /// 
    /// Process:
    /// 1. Apply gravity manually (NavMeshAgent doesn't apply gravity)
    /// 2. Sync agent.nextPosition = rb.position (agent reads Rigidbody position)
    /// 3. Get desiredVelocity from agent (pathfinding destination velocity)
    /// 4. Preserve Y velocity (gravity, jumping, falling)
    /// 5. Apply velocity to Rigidbody with state-specific logic:
    ///    - Patrol (not waiting): Use only horizontal components from agent
    ///    - All other states: Use full desiredVelocity (including Y)
    /// 6. Rotate toward movement direction based on horizontal velocity
    /// 
    /// Physics Rationale: Manual velocity control avoids NavMeshAgent movement stuttering.
    /// </summary>
    void FixedUpdate()
    {
        if (rb == null || agent == null) return;

        // Apply gravity manually
        rb.AddForce(Physics.gravity, ForceMode.Acceleration);
        agent.nextPosition = rb.position;

        Vector3 desiredVelocity = agent.desiredVelocity;
        desiredVelocity.y = rb.linearVelocity.y;  // Preserve vertical velocity (gravity/jump)

        // Apply velocity based on state
        if (currentState == State.Patrol && !isWaiting)
            rb.linearVelocity = new Vector3(desiredVelocity.x, rb.linearVelocity.y, desiredVelocity.z);
        else
            rb.linearVelocity = desiredVelocity;

        // Rotate toward movement direction
        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (flatVelocity.sqrMagnitude > 0.01f)
            rb.MoveRotation(Quaternion.LookRotation(flatVelocity.normalized));
    }

    // ===== NAVMESH AGENT SETUP =====
    /// <summary>
    /// Configures NavMeshAgent parameters for smooth movement and avoidance.
    /// Called once at Start().
    /// 
    /// Configuration:
    /// - speed: Set to this.speed value
    /// - angularSpeed: 900° per second (fast, snappy turning)
    /// - acceleration: 60 units per sec² (high responsiveness)
    /// - radius: 0.45 units (matches typical humanoid collider)
    /// - stoppingDistance: this.stoppingDistance (waypoint arrival threshold)
    /// - autoBraking: true (auto-decelerate when near destination)
    /// - updateRotation: true (agent-controlled facing)
    /// - updateUpAxis: false (gravity determines up)
    /// - updatePosition: false (Rigidbody controls position, not agent)
    /// - obstacleAvoidance: HighQuality (avoids other agents smoothly)
    /// </summary>
    private void SetupAgent()
    {
        agent.speed = speed;
        agent.angularSpeed = 900f;
        agent.acceleration = 60f;
        agent.radius = 0.45f;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;
        agent.updateRotation = true;
        agent.updateUpAxis = false;
        agent.updatePosition = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    // ===== PATROL STATE =====
    /// <summary>
    /// Patrol behavior: follow waypoint route with waits at each point.
    /// 
    /// Logic:
    /// 1. Skip if waiting at current waypoint or no waypoints defined
    /// 2. Check if agent reached waypoint (pathPending=false AND remainingDistance <= stoppingDistance)
    /// 3. If reached, start WaitAtPatrolPoint() coroutine
    /// 
    /// Called by: Update() when currentState == Patrol
    /// </summary>
    void PatrolBehavior()
    {
        if (isWaiting || waypoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            StartCoroutine(WaitAtPatrolPoint());
    }

    /// <summary>
    /// Wait coroutine at patrol waypoint.
    /// 
    /// Process:
    /// 1. Set isWaiting = true (prevents PatrolBehavior from starting another wait)
    /// 2. Stop agent (agent.isStopped = true)
    /// 3. Wait waitTimeAtPoint seconds
    /// 4. Increment waypoint index with wraparound: index = (index + 1) % length
    /// 5. Set destination to new waypoint
    /// 6. Resume agent movement
    /// 7. Set isWaiting = false
    /// 
    /// Called by: PatrolBehavior() when waypoint reached
    /// </summary>
    private IEnumerator WaitAtPatrolPoint()
    {
        isWaiting = true;
        agent.isStopped = true;

        yield return new WaitForSeconds(waitTimeAtPoint);

        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        agent.SetDestination(waypoints[currentWaypointIndex].position);
        agent.isStopped = false;

        isWaiting = false;
    }

    // ===== ALERT STATE =====
    /// <summary>
    /// Alert behavior: look toward player while waiting.
    /// Called during Alert state to smoothly face player.
    /// 
    /// Logic:
    /// 1. If player null, transition to Return state
    /// 2. Calculate look direction from transform to player (ignore Y axis)
    /// 3. Smoothly rotate toward look direction (5° per second with Slerp)
    /// 
    /// Called by: Update() when currentState == Alert
    /// </summary>
    void AlertBehavior()
    {
        if (player == null)
        {
            currentState = State.Return;
            return;
        }

        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized), Time.deltaTime * 5f);
    }

    /// <summary>
    /// Alert phase coroutine: wait and watch for confirmed player detection.
    /// 
    /// Process:
    /// 1. Stop agent (agent.isStopped = true)
    /// 2. Loop for alertDuration seconds:
    ///    - Face toward player with smooth rotation
    ///    - Check if isPlayerDetected flag remains true
    ///    - If still detected, transition to Chase immediately
    /// 3. If full duration elapses without detection, return to Patrol
    /// 4. Resume agent movement before exiting
    /// 5. Clear alertRoutine reference
    /// 
    /// Rationale: Allows player brief window to escape before chase begins.
    /// Called by: OnPlayerDetected() when player enters trigger
    /// </summary>
    IEnumerator AlertPhase()
    {
        agent.isStopped = true;
        float alertTimer = 0f;

        while (alertTimer < alertDuration)
        {
            alertTimer += Time.deltaTime;

            if (player != null)
            {
                Vector3 lookDir = player.position - transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 5f);
            }

            // If during alert we get a detection that survives cooldown checks, start chase
            if (isPlayerDetected)
            {
                agent.isStopped = false;
                currentState = State.Chase;
                if (!isBursting)
                    burstRoutine = StartCoroutine(BurstOfSpeed());

                alertRoutine = null;
                yield break;
            }

            yield return null;
        }

        currentState = State.Patrol;
        agent.isStopped = false;
        alertRoutine = null;
    }

    // ===== CHASE STATE =====
    /// <summary>
    /// Chase behavior: pursue player toward their current position.
    /// 
    /// Logic:
    /// 1. If player null, transition to Return state
    /// 2. Set agent destination to player.position (real-time update)
    /// 
    /// Called by: Update() when currentState == Chase
    /// </summary>
    void ChaseBehavior()
    {
        if (player == null)
        {
            currentState = State.Return;
            return;
        }

        agent.SetDestination(player.position);
    }

    // ===== RETURN STATE =====
    /// <summary>
    /// Return behavior: navigate back to last patrol waypoint.
    /// 
    /// Logic:
    /// 1. If no waypoints, stay in Return (or error)
    /// 2. Set destination to currentWaypoint
    /// 3. Check if waypoint reached (pathPending=false AND remainingDistance <= stoppingDistance)
    /// 4. If reached, transition back to Patrol
    /// 
    /// Called by: Update() when currentState == Return
    /// </summary>
    void ReturnBehavior()
    {
        if (waypoints.Length == 0) return;

        agent.SetDestination(waypoints[currentWaypointIndex].position);

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            currentState = State.Patrol;
    }

    // ===== PLAYER DETECTION CALLBACKS =====
    /// <summary>
    /// Called by EnemyDetection.OnTriggerEnter() when player enters detection zone.
    /// Initiates alert phase with option to transition to chase if detection confirmed.
    /// 
    /// Logic:
    /// 1. Check if enemy is in battle cooldown (prevent re-engagement)
    /// 2. If already chasing, don't restart alert
    /// 3. Transition to Alert state
    /// 4. Stop any existing alert routine, start new AlertPhase()
    /// 
    /// Called by: EnemyDetection component
    /// Result: Begins alert phase (facing player, waiting for sustained detection)
    /// </summary>
    public void OnPlayerDetected()
    {
        if (enemyScript != null && enemyScript.IsBattleCooldownActive())
        {
            isPlayerDetected = false;
            return;
        }

        if (currentState != State.Chase)
        {
            currentState = State.Alert;
            if (alertRoutine != null) StopCoroutine(alertRoutine);
            alertRoutine = StartCoroutine(AlertPhase());
        }
    }

    /// <summary>
    /// Called by EnemyDetection.OnTriggerExit() when player leaves detection zone.
    /// Stops chase/burst and returns to alert or patrol.
    /// 
    /// Logic:
    /// 1. If burst active, stop burst coroutine and reset speed
    /// 2. If chasing, return to Alert for brief moment before patrol
    /// 3. Set isBursting = false, isPlayerDetected = false
    /// 
    /// Called by: EnemyDetection component
    /// Result: Halts pursuit and begins return-to-patrol sequence
    /// </summary>
    public void OnPlayerLost()
    {
        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
            burstRoutine = null;
            isBursting = false;
            agent.speed = baseSpeed;

            if (currentState == State.Chase)
            {
                currentState = State.Alert;
                if (alertRoutine != null) StopCoroutine(alertRoutine);
                alertRoutine = StartCoroutine(AlertPhase());
            }
        }
    }

    /// <summary>
    /// Called by BattleManager when player flees battle.
    /// Forces immediate stop to chase and initiates battle cooldown.
    /// 
    /// Actions:
    /// 1. Call StopChaseImmediate() to halt all pursuit coroutines
    /// 2. Clear isPlayerDetected flag
    /// 3. Return to Patrol state
    /// 4. Trigger battle cooldown via enemyScript.StartBattleCooldown()
    /// 5. Resume patrol to nearest waypoint
    /// 
    /// Called by: BattleManager.OnPlayerFlee() or similar
    /// Result: Enemy returns to patrol with cooldown preventing immediate re-engagement
    /// </summary>
    public void OnPlayerRunFromBattle()
    {
        StopChaseImmediate();
        isPlayerDetected = false;
        currentState = State.Patrol;

        if (enemyScript != null)
            enemyScript.StartBattleCooldown(enemyScript.battleCooldownDuration);

        if (waypoints.Length > 0)
        {
            agent.isStopped = false;
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }

        Debug.Log($"[EnemyMovement] {enemyID} returned to patrol and cooldown started.");
    }

    // ===== UTILITY METHODS =====
    /// <summary>
    /// Immediately terminates all active chase coroutines and resets chase state.
    /// 
    /// Actions:
    /// 1. Clear isChasing, player references
    /// 2. Stop alert and burst coroutines
    /// 3. Reset isBursting flag and speed
    /// 
    /// Called by: OnPlayerRunFromBattle(), manual interrupts
    /// Rationale: Ensures clean transition without lingering coroutine state
    /// </summary>
    private void StopChaseImmediate()
    {
        isChasing = false;
        player = null;

        if (alertRoutine != null) { StopCoroutine(alertRoutine); alertRoutine = null; }
        if (burstRoutine != null) { StopCoroutine(burstRoutine); burstRoutine = null; }

        isBursting = false;
        agent.speed = baseSpeed;
    }

    /// <summary>
    /// Burst speed coroutine: temporarily increases chase speed.
    /// 
    /// Process:
    /// 1. Set isBursting = true
    /// 2. Increase agent.speed to burstSpeed
    /// 3. Wait burstDuration seconds
    /// 4. Reset agent.speed to baseSpeed
    /// 5. Set isBursting = false
    /// 
    /// Called by: OnPlayerDetected() (if not already bursting)
    /// Effect: Creates sprint-then-jog movement pattern during chase
    /// </summary>
    IEnumerator BurstOfSpeed()
    {
        isBursting = true;
        agent.speed = burstSpeed;
        yield return new WaitForSeconds(burstDuration);
        isBursting = false;
        agent.speed = baseSpeed;
    }
}
