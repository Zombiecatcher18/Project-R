using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class EnemyWandering : MonoBehaviour
{
    [Header("NavMesh & Wandering")]
    public NavMeshAgent agent;
    public float wanderRadius = 10f;
    public float waitAtPoint = 2f;
    private bool isWaiting = false;

    [Header("Burst Settings")]
    public float burstSpeed = 7f;
    public float burstDuration = 1.5f;
    private bool isBursting = false;
    private float baseSpeed;

    [Header("Memory")]
    public float memoryDuration = 5f;
    private List<Vector3> playerMemory = new List<Vector3>();
    private List<float> memoryTimers = new List<float>();

    private Transform player;
    private Rigidbody rb;
    public float stoppingDistance = 0.5f;
    
    private enum State { Wandering, Chasing, Investigating }
    private State currentState = State.Wandering;

    // -------------------- START --------------------

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        baseSpeed = agent.speed;

        agent.Warp(transform.position);
        agent.stoppingDistance = stoppingDistance;
    }

    void Update()
    {
        UpdateMemory();

        switch (currentState)
        {
            case State.Wandering:
                if (!isWaiting)
                    Wander();
                break;
            case State.Chasing:
                Chasing();
                break;
            case State.Investigating:
                if (!isWaiting)
                    Investigating();
                break;
        }
    }

    void FixedUpdate()
    {
        rb.AddForce(Physics.gravity, ForceMode.Acceleration);

        agent.nextPosition = rb.position;

        Vector3 desiredVelocity = agent.desiredVelocity;
        desiredVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = desiredVelocity;

        if (new Vector3(desiredVelocity.x, 0, desiredVelocity.z).sqrMagnitude > 0.01f)
            rb.MoveRotation(Quaternion.LookRotation(new Vector3(desiredVelocity.x, 0, desiredVelocity.z).normalized));
    }

    // -------------------- DETECTION --------------------

    public void PlayerDetected(Transform detectedPlayer)
    {
        player = detectedPlayer;
        currentState = State.Chasing;
    }

    public void PlayerLost(Vector3 lastPosition)
    {
        RememberPlayer(lastPosition);
        player = null;
        currentState = State.Investigating;
    }

    // -------------------- WANDER --------------------

    void Wander()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance && !isWaiting)
        {
            Vector3 target = RandomNavMeshPos(transform.position, wanderRadius);

            agent.SetDestination(target);

            StartCoroutine(WaitAtDestination());
        }
    }
    
    IEnumerator WaitAtDestination()
    {
        isWaiting = true;

        yield return new WaitForSeconds(waitAtPoint);

        isWaiting = false;
    }

    Vector3 RandomNavMeshPos(Vector3 origin, float distance)
    {
        Vector3 ranDir = Random.insideUnitSphere * distance + origin;
        NavMeshHit hit;
        NavMesh.SamplePosition(ranDir, out hit, distance, NavMesh.AllAreas);
        return hit.position;

    }

    // -------------------- CHASE --------------------

    void Chasing()
    {
        if (player != null)
        {
            agent.SetDestination(player.position);

            if (!isBursting)
                StartCoroutine(BurstOfSpeed());
        }
        else
        {
            currentState = State.Investigating;
        }
    }

    IEnumerator BurstOfSpeed()
    {
        isBursting = true;
        float originalSpeed = agent.speed;
        agent.speed = burstSpeed;

        yield return new WaitForSeconds(burstDuration);

        agent.speed = originalSpeed;
        isBursting = false;
    }

    // -------------------- INVESTIGATING --------------------

    void Investigating()
    {
        if (playerMemory.Count == 0)
        {
            currentState = State.Wandering;
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance && !isWaiting)
        {
            int index = Random.Range(0, playerMemory.Count);
            Vector3 target = playerMemory[index];
            agent.SetDestination(target);

            StartCoroutine(WaitAtDestination());
        }
    }

    // -------------------- MEMORY --------------------

    void UpdateMemory()
    {
        for (int i = memoryTimers.Count - 1; i >= 0; i--)
        {
            memoryTimers[i] -= Time.deltaTime;
            if (memoryTimers[i] <= 0)
            {
                memoryTimers.RemoveAt(i);
                playerMemory.RemoveAt(i);
            }
        }
    }
    
    public void RememberPlayer(Vector3 pos)
    {
        playerMemory.Add(pos);
        memoryTimers.Add(memoryDuration);
    }
}
