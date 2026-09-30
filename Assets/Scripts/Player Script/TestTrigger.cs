using UnityEngine;

public class TestTrigger : MonoBehaviour
{
    private EnemyScript enemyScript;

    void Start()
    {
        enemyScript = GetComponentInParent<EnemyScript>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (enemyScript != null)
        {
            Debug.Log("[BattleTrigger] Player touched battle trigger — starting battle");
            enemyScript.EngageBattle(other.transform.position);
        }
    }
}

