using System.Collections;
using System.Diagnostics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Manages NPC behavior patterns: patrolling waypoints and transitioning between work/relax states.
/// Uses NavMeshAgent for movement and randomized timers for state transitions.
/// Interacts with DialogueManager.cs when player initiates conversation.
/// </summary>
public class NPCController : MonoBehaviour
{
    /// <summary>Enum representing NPC activity state.</summary>
    public enum NPCState { Working, Relaxing }
    /// <summary>Current state of the NPC (Working or Relaxing).</summary>
    public NPCState currentState;

    [Header("Waypoints")]
    /// <summary>Array of waypoints for work patrol route.</summary>
    public Transform[] workWaypoints;
    /// <summary>Array of waypoints for relaxing patrol route.</summary>
    public Transform[] relaxingWaypoints;

    /// <summary>Currently active waypoint array (either work or relax based on state).</summary>
    private Transform[] activeWaypoints;
    /// <summary>Index of current waypoint in active route.</summary>
    private int currentWaypointIndex = 0;

    [Header("State Timing")]
    /// <summary>Default duration for work state; overridden by randomized currentStateDuration.</summary>
    public float workDuration = 20f;
    /// <summary>Default duration for relax state; overridden by randomized currentStateDuration.</summary>
    public float relaxDuration = 15f;
    /// <summary>Accumulator for state duration tracking.</summary>
    private float stateTimer = 0f;

    /// <summary>Minimum duration for work state (randomized between min/max).</summary>
    public float minWorkDuration = 10f;
    /// <summary>Maximum duration for work state (randomized between min/max).</summary>
    public float maxWorkDuration = 25f;
    /// <summary>Minimum duration for relax state (randomized between min/max).</summary>
    public float minRelaxDuration = 8f;
    /// <summary>Maximum duration for relax state (randomized between min/max).</summary>
    public float maxRelaxDuratin = 16;

    /// <summary>Current state duration for this cycle; randomized on state switch.</summary>
    private float currentStateDuration = 0f;

    [Header("Movement")]
    /// <summary>Duration NPC waits at each waypoint before moving to next.</summary>
    public float waitAtPoint = 2f;
    /// <summary>Flag indicating if NPC is currently waiting at a waypoint.</summary>
    private bool isWaiting = false;
    /// <summary>Flag indicating if NPC has reached the first waypoint since initialization.</summary>
    private bool reachedFirstWaypoint = false;
    /// <summary>Lock flag to freeze NPC movement and dialogue (e.g., during interactions).</summary>
    public bool hardLocked = false;

    [Header("Debug Timers")]
    /// <summary>Debug display of time spent in current state.</summary>
    public float timeInState;
    /// <summary>Debug display of remaining time in current state.</summary>
    public float timeRemaining;
    /// <summary>Debug flag indicating state just transitioned.</summary>
    public bool stateJustFinished;

    /// <summary>NavMeshAgent for pathfinding and movement; cached from GetComponent.</summary>
    private NavMeshAgent agent;
    /// <summary>Reference to DialogueManager for NPC conversation; references DialogueManager.cs.</summary>
    public DialogueManager dialogueManager;

    // ===================================================
    // Initialization
    // ===================================================

    /// <summary>
    /// Initializes agent component and sets initial state.
    /// </summary>
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        SwitchState(currentState);
    }

    // ===================================================
    // Main Update Loop
    // ===================================================

    /// <summary>
    /// Updates NPC state, waypoint navigation, and state transitions each frame.
    /// Hard-locked NPCs are prevented from moving.
    /// </summary>
    void Update()
    {
        if (hardLocked)
        {
            StopMovement();
            RotateTowardsPlayer();
            return;
        }

        HandleMovement();

        if (reachedFirstWaypoint)
        {
            stateTimer += Time.deltaTime;
            HandleStateTimer();
        }

        timeInState = stateTimer;
        timeRemaining = currentStateDuration - stateTimer;
    }

    // ------------------ STATE LOGIC ------------------

    void HandleStateTimer()
    {
        switch (currentState)
        {
            case NPCState.Working:
                if (stateTimer >= currentStateDuration)
                {
                    SwitchState(NPCState.Relaxing);
                }
                break;
            case NPCState.Relaxing:
                if (stateTimer >= currentStateDuration)
                {
                    SwitchState(NPCState.Working);
                }
                break;
        }
        if (stateTimer >= currentStateDuration)
        {
            stateJustFinished = true;
            SwitchState(currentState == NPCState.Working ? NPCState.Relaxing : NPCState.Working);
        }
        else
        {
            stateJustFinished = false;
        }
    }

    public void SwitchState(NPCState newState)
    {
        currentState = newState;
        stateTimer = 0f;
        reachedFirstWaypoint = false;

        if (currentState == NPCState.Working)
        {
            currentStateDuration = Random.Range(minWorkDuration, maxWorkDuration);
            activeWaypoints = workWaypoints;
        }
        else
        {
            currentStateDuration = Random.Range(minRelaxDuration, maxRelaxDuratin);
            activeWaypoints = relaxingWaypoints;
        }

        currentWaypointIndex = 0;
        agent.SetDestination(activeWaypoints[currentWaypointIndex].position);
    }

    // ------------------ MOVEMENT LOGIC ------------------

    void HandleMovement()
    {
        if (activeWaypoints.Length == 0 || isWaiting) return;

        if (!hardLocked && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            StartCoroutine(WaitAndNext());
        }

        IEnumerator WaitAndNext()
        {
            isWaiting = true;
            agent.isStopped = true;

            yield return new WaitForSeconds(waitAtPoint);

            if (!reachedFirstWaypoint)
                reachedFirstWaypoint = true;

            currentWaypointIndex = (currentWaypointIndex + 1) % activeWaypoints.Length;

            agent.isStopped = false;
            agent.SetDestination(activeWaypoints[currentWaypointIndex].position);

            isWaiting = false;
        }
    }

    void RotateTowardsPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        Vector3 direction = (player.transform.position - transform.position);
        direction.y = 0;

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    public void StopMovement()
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }   
    }

    public void ResumeMovement()
    {
        if (agent != null)
        {
            if (activeWaypoints != null && activeWaypoints.Length > 0)
                agent.SetDestination(activeWaypoints[currentWaypointIndex].position);
        }
    }
}

