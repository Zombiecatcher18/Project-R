using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance;

    // Keep track of all defeated enemies by their unique IDs
    private HashSet<string> defeatedEnemies = new HashSet<string>();
    private Dictionary<string, float> enemyRespawnTimers = new Dictionary<string, float>();

    public float respawnDelay = 120f; // default respawn delay in seconds

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Do NOT clear defeatedEnemies here! That resets your progress.
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log("[EnemyManager] Initialized and set to DontDestroyOnLoad.");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[EnemyManager] Scene loaded: {scene.name}");

        // Skip checks in BattleScene
        if (scene.name == "BattleScene")
            return;

        // Find all enemies currently in the loaded scene
        EnemyScript[] allEnemies = FindObjectsOfType<EnemyScript>(true);

        foreach (EnemyScript enemy in allEnemies)
        {
            if (string.IsNullOrEmpty(enemy.enemyID))
                continue;

            // If enemy is part of active quest -> active it
            if (GameManager.Instance.questActiveEnemyIDs.Contains(enemy.enemyID))
            {
                enemy.gameObject.SetActive(true);
                Debug.Log($"[EnemyManager] Activated quest enemy '{enemy.enemyID}'");
                continue;
            }

            // NOT defeated → enemy should be active
            if (!defeatedEnemies.Contains(enemy.enemyID))
            { 
                enemy.gameObject.SetActive(false); 
                continue; 
            }

            // Look up timer
            if (enemyRespawnTimers.TryGetValue(enemy.enemyID, out float respawnTime))
            {
                // If it's still too early → keep the enemy disabled
                if (Time.realtimeSinceStartup < respawnTime)
                {
                    enemy.gameObject.SetActive(false);
                    Debug.Log($"[EnemyManager] '{enemy.enemyID}' still dead. Respawn in {respawnTime - Time.time:0.0}s.");
                    continue;
                }
            }

            // If this is a quest enemy → NEVER respawn it
            if (QuestManager.Instance != null &&
                QuestManager.Instance.requiredEnemyIDs != null &&
                System.Array.Exists(QuestManager.Instance.requiredEnemyIDs, x => x == enemy.enemyID))
            {
                enemy.gameObject.SetActive(false);
                Debug.Log($"[EnemyManager] Quest enemy '{enemy.enemyID}' is defeated. Keeping disabled permanently.");
                continue;
            }

            // TIMER EXPIRED → respawn enemy
            Debug.Log($"[EnemyManager] Respawning enemy '{enemy.enemyID}'");

            defeatedEnemies.Remove(enemy.enemyID);
            enemyRespawnTimers.Remove(enemy.enemyID);

            enemy.gameObject.SetActive(true);
        }

    }

    // Public method to check if an enemy is defeated
    public bool IsDefeated(string id)
    {
        return defeatedEnemies.Contains(id);
    }

    // Public method to mark an enemy as defeated
    public void MarkDefeated(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[EnemyManager] Tried to mark an enemy with no ID!");
            return;
        }

        defeatedEnemies.Add(id);

        // Check if this enemy is part of the ative Quest
        if (QuestManager.Instance != null && 
        QuestManager.Instance.requiredEnemyIDs != null && 
        System.Array.Exists(QuestManager.Instance.requiredEnemyIDs, x => x == id))
        {
            Debug.Log($"[EnemyManager] Enemy '{id}' is part of the active quest. Not setting respawn timer.");
            return; // STOP HERE - do not set respawn timer
        }

        // Normal `enemy - set respawn timer
        enemyRespawnTimers[id] = Time.realtimeSinceStartup + respawnDelay;
        Debug.Log($"[EnemyManager] Enemy '{id}' marked as defeated.");
    }

    // Optional: allow reviving enemies (for debugging or respawn systems)
    public void UnmarkDefeated(string id)
    {
        if (defeatedEnemies.Remove(id))
            Debug.Log($"[EnemyManager] Enemy '{id}' revived (unmarked).");
    }

    // Optional: reset all defeated enemies (for debugging or new game)
    public void ResetAll()
    {
        defeatedEnemies.Clear();
        Debug.Log("[EnemyManager] All defeated enemies cleared.");
    }

    public void ClearDefeatedEnemies()
    {
        defeatedEnemies.Clear();
        Debug.Log("[EnemyManager] Cleared defeated enemies list.");
    }
}
