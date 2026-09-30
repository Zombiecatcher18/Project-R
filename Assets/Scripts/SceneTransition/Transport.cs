using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene transition trigger for level portals/doors.
/// Detects player proximity and loads specified scene with spawn point configuration.
/// Coordinates with GameManager and SpawnPoint system for player positioning on load.
/// </summary>
public class Transport : MonoBehaviour
{
    /// <summary>Target scene index in build settings to load on trigger.</summary>
    public int sceneBuildingIndex;
    /// <summary>Spawn point identifier in target scene; used by SpawnPoint matching to position player correctly.</summary>
    public string spawnIDOnNextScene;

    /// <summary>
    /// Triggers scene load when player enters zone.
    /// Called by physics system on collider trigger entry.
    /// Sets GameManager.lastSpawnID for next scene's SpawnPoint to use.
    /// Loads scene via SceneManager at configured build index.
    /// References GameManager for cross-scene state and SpawnPoint for positioning.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Set spawn point for next scene's initialization (picked up by SpawnPoint.Start())
            GameManager.Instance.lastSpawnID = spawnIDOnNextScene;
            // Load target scene
            SceneManager.LoadScene(sceneBuildingIndex);
        }
    }
}
