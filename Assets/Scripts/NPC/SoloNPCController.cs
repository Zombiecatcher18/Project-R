using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class SoloNPCController : MonoBehaviour
{
    public enum NPCState { Working, Relaxing }
    public NPCState currentState;

    [Header("Waypoints")]
    public Transform[] workWaypoints;
    public Transform[] relaxingWaypoints;

    private Transform[] activeWaypoints;
    private int currentWaypointIndex = 0;

    [Header("State Timing")]
    public float minWorkDuration = 10f;
    public float maxWorkDuration = 25f;
    public float minRelaxDuration = 8f;
    public float maxRelaxDuration = 16f;

    private float stateTimer = 0f;
    private float currentStateDuration = 0f;
    private bool reachedFirstWaypoint = false;

    [Header("Movement")]
    public float waitAtPoint = 2f;
    private Coroutine waitCoroutine = null;
    private bool isWaiting = false;

    [Header("Dialogue / Locking")]
    // legacy public flag kept for compatibility (you can set it directly but EnterDialogue/ExitDialogue is preferred)
    public bool hardLocked = false;
    public SoloDialogueManager dialogueManager;

    [Header("Debug Timers")]
    public float timeInState;
    public float timeRemaining;
    public bool stateJustFinished;

    private NavMeshAgent agent;

    // Internal pause flag used by EnterDialogue/ExitDialogue
    private bool pausedForDialogue = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        SwitchState(currentState);
    }

    void Update()
    {
        // If either hardLocked OR explicitly paused for dialogue, freeze movement/timer and rotate to player
        if (hardLocked || pausedForDialogue)
        {
            StopMovement();            // ensure agent is stopped
            RotateTowardsPlayer();
            // do NOT advance timers or waypoints while paused
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

    void HandleStateTimer()
    {
        if (stateTimer >= currentStateDuration)
        {
            stateJustFinished = true;
            SwitchState(currentState == NPCState.Working ? NPCState.Relaxing : NPCState.Working);
        }
        else stateJustFinished = false;
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
            currentStateDuration = Random.Range(minRelaxDuration, maxRelaxDuration);
            activeWaypoints = relaxingWaypoints;
        }

        currentWaypointIndex = 0;
        if (activeWaypoints != null && activeWaypoints.Length > 0 && agent != null)
            agent.SetDestination(activeWaypoints[currentWaypointIndex].position);
    }

    void HandleMovement()
    {
        if (activeWaypoints == null || activeWaypoints.Length == 0 || isWaiting) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            waitCoroutine = StartCoroutine(WaitAndMoveNext());
        }
    }

    IEnumerator WaitAndMoveNext()
    {
        isWaiting = true;
        agent.isStopped = true;

        float timer = 0f;
        // Wait in small increments so we can cancel early if dialogue starts
        while (timer < waitAtPoint)
        {
            if (pausedForDialogue || hardLocked)
            {
                // Cancel the wait if we were paused for dialogue
                isWaiting = false;
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (!reachedFirstWaypoint)
            reachedFirstWaypoint = true;

        currentWaypointIndex = (currentWaypointIndex + 1) % activeWaypoints.Length;

        agent.isStopped = false;
        agent.SetDestination(activeWaypoints[currentWaypointIndex].position);
        isWaiting = false;
        waitCoroutine = null;
    }

    void RotateTowardsPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        Vector3 direction = player.transform.position - transform.position;
        direction.y = 0;
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
        }
    }

    // Public method called by the dialogue manager to pause NPC
    public void EnterDialogue()
    {
        pausedForDialogue = true;
        hardLocked = true; // keep legacy behavior consistent
        // stop agent and cancel wait coroutine if running
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        if (waitCoroutine != null)
        {
            StopCoroutine(waitCoroutine);
            waitCoroutine = null;
            isWaiting = false;
        }
        // do not alter stateTimer so timer remains frozen
    }

    // Public method called by the dialogue manager to resume NPC
    public void ExitDialogue()
    {
        pausedForDialogue = false;
        hardLocked = false;
        // resume navigation to current waypoint (if any)
        if (agent != null && activeWaypoints != null && activeWaypoints.Length > 0)
        {
            agent.isStopped = false;
            agent.SetDestination(activeWaypoints[currentWaypointIndex].position);
        }
    }

    // Kept for legacy/external control
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
            agent.isStopped = false;
            if (activeWaypoints != null && activeWaypoints.Length > 0)
                agent.SetDestination(activeWaypoints[currentWaypointIndex].position);
        }
    }
}
