using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Checkpoint system trigger that auto-saves game state when player enters.
/// Includes cooldown to prevent spam-saving on every frame.
/// Updates GameManager checkpoint position and saves via SaveManager.
/// Coordinates with SaveManager.cs for save persistence and GameManager for state tracking.
/// </summary>
public class Checkpoint : MonoBehaviour
{
    /// <summary>Minimum time in seconds between consecutive checkpoint saves; prevents spam saves.</summary>
    private float saveCooldown = 1f;
    /// <summary>Timestamp of last save; used with saveCooldown to gate save frequency.</summary>
    private float lastSaveTime = -999f;

    /// <summary>
    /// Monitors player presence in checkpoint trigger with cooldown.
    /// Calls SaveCheckpoint() when cooldown expires.
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // Prevent spam saves with cooldown check
        if (Time.time - lastSaveTime < saveCooldown)
            return;

        lastSaveTime = Time.time;

        SaveCheckpoint();
    }

    /// <summary>
    /// Saves complete game state at checkpoint location.
    /// Updates GameManager checkpoint position and saves to current slot via SaveManager.
    /// References GameManager, SaveManager, and SaveData.
    /// </summary>
    private void SaveCheckpoint()
    {
        // Update GameManager checkpoint location (called by SaveManager.BuildSaveData())
        GameManager.Instance.lastCheckpointPosition = transform.position;
        GameManager.Instance.hasCheckpoint = true;

        // Gather all current game state (player stats, inventory, quest progress, etc.)
        SaveData data = SaveManager.BuildSaveData();

        // Override checkpoint metadata in save data structure
        Vector3 pos = GameManager.Instance.lastCheckpointPosition;
        data.hasCheckpoint = true;
        data.checkpointPosition = new float[] { pos.x, pos.y, pos.z };

        // Persist complete save to disk via SaveManager for current slot
        SaveManager.Save(GameManager.Instance.currentSlot, data);

        Debug.Log("[Checkpoint] Game saved at position " + transform.position);
    }
}
