using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Positions player on scene load at correct spawn point.
/// Listens to scene load events and matches GameManager.lastSpawnID against SpawnPoint components.
/// Enables seamless transitions between scenes with consistent player positioning.
/// Skips placement when returning from battle (BattleManager handles positioning).
/// </summary>
public class PlayerSpawn : MonoBehaviour
{
    /// <summary>
    /// Registers scene load listener on component enable.
    /// Called when PlayerSpawn gameObject becomes active.
    /// </summary>
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Unregisters scene load listener on component disable.
    /// Called when PlayerSpawn gameObject becomes inactive.
    /// </summary>
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Handles scene load event by initiating spawn position coroutine.
    /// Called by SceneManager when new scene finishes loading.
    /// </summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(SetSpawnPosition());
    }

    /// <summary>
    /// Positions player at matching spawn point in new scene.
    /// Waits one frame for scene initialization, then finds SpawnPoint matching lastSpawnID.
    /// Skips positioning if returning from battle (BattleManager handles post-battle positioning).
    /// References GameManager for spawn ID tracking and SpawnPoint for position data.
    /// </summary>
    IEnumerator SetSpawnPosition()
    {
        // Wait one frame for scene objects to fully initialize before positioning
        yield return null;

        // Skip positioning if returning from battle (BattleManager.FinishBattleReturnToOverworld() handles this)
        if (GameManager.Instance.returningFromBattle)
        {
            Debug.Log("[PlayerSpawn] Skipping spawn placement - returning from battle");
            yield break;
        }

        // Get target spawn point ID from GameManager (set by Transport)
        string targetID = GameManager.Instance.lastSpawnID;
        if (string.IsNullOrEmpty(targetID)) yield break;

        // Find player and all spawn points in scene
        GameObject player = GameObject.FindWithTag("Player");
        GameObject[] respawns = GameObject.FindGameObjectsWithTag("Respawn");

        // Search for matching spawn point
        foreach (GameObject obj in respawns)
        {
            SpawnPoint p = obj.GetComponent<SpawnPoint>();
            if (p != null && p.SpawnID == targetID)
            {
                // Position player at matching spawn point
                player.transform.position = p.transform.position;
                Debug.Log("[PlayerSpawn] Player spawned at: " + targetID);

                // Clear spawn ID so it's not reused
                GameManager.Instance.lastSpawnID = null;
                yield break;
            }
        }

        Debug.LogWarning("[PlayerSpawn] ⚠ Spawn point not found: " + targetID);
    }
}
