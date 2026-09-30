using UnityEngine;

public class EnemyWanderDetection : MonoBehaviour
{
    public EnemyWandering enemy;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            enemy.PlayerDetected(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            enemy.PlayerLost(other.transform.position);
        }
    }
}
