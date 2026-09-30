/* using UnityEngine;

public class EnemyBattleTrigger : MonoBehaviour
{
    private EnemyScript enemyScript;

    void Start()
    {
        enemyScript = GetComponentInParent<EnemyScript>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log($"[EnemyBattleTrigger] Player touched enemy body — starting battle.");
        enemyScript.StartBattle(other.transform.position);
    }
} */