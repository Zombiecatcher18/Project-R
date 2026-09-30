using UnityEngine;

/// <summary>
/// Scene spawn point marker for player placement on scene load.
/// Matched against GameManager.lastSpawnID by SceneSpawnManager to position player correctly.
/// Enables dynamic level transitions with persistent player positioning across scenes.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    /// <summary>Unique identifier for this spawn point; must match Transport.spawnIDOnNextScene to be used.</summary>
    public string SpawnID;
}
