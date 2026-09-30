using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton quest state management system.
/// Tracks quest progression and required enemy defeats.
/// Works with DialogueManager.cs for quest dialogue branching and rewards.
/// Persists quest state across scenes via DontDestroyOnLoad.
/// </summary>
public class QuestManager : MonoBehaviour
{
    /// <summary>Singleton instance for global quest access.</summary>
    public static QuestManager Instance;

    /// <summary>Flag indicating if player has accepted the current quest.</summary>
    public bool questAccepted = false;
    /// <summary>Flag indicating if player has received the quest reward; prevents duplicate rewards.</summary>
    public bool rewardGiven = false;

    /// <summary>Array of enemy IDs required to defeat to complete the quest; set via inspector.</summary>
    public string[] requiredEnemyIDs;

    /// <summary>Set of enemy IDs already defeated; used for quest progress tracking.</summary>
    public HashSet<string> defeatedEnemies = new HashSet<string>();

    /// <summary>
    /// Initializes singleton instance and marks as persistent.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[QuestManager] Duplicate detected, destroying this one in scene: " + gameObject.scene.name);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Records an enemy as defeated for quest progress.
    /// Called by EnemyScript.OnDeath() when a quest enemy is defeated.
    /// Updates local tracking and GameManager.defeatedEnemyIDs for persistence.
    /// </summary>
    public void MarkEnemyDefeated(string id)
    {
        Debug.Log($"[QuestManager] MarkEnemyDefeated called with ID: '{requiredEnemyIDs}'");

        if (!defeatedEnemies.Contains(id))
            defeatedEnemies.Add(id);
        
        Debug.Log("[QuestManager] List now: " + string.Join(",", defeatedEnemies));
    }

    /// <summary>
    /// Checks if a specific enemy has been defeated in this quest.
    /// </summary>
    public bool IsEnemyDefeated(string id)
    {
        return defeatedEnemies.Contains(id);
    }

    /// <summary>
    /// Checks if all required enemies have been defeated.
    /// Queries GameManager.defeatedEnemyIDs for persistent tracking.
    /// Returns false if no required enemies are configured.
    /// </summary>
    public bool AreAllEnemiesDefeated()
    {
        if (requiredEnemyIDs == null || requiredEnemyIDs.Length == 0)
        {
            Debug.Log("[QuestManager] No required enemies set.");
            return false;
        }

        Debug.Log("[QuestManager] Checking required enemies:");

        foreach (string id in requiredEnemyIDs)
        {
            bool defeated = GameManager.Instance != null &&
                            GameManager.Instance.defeatedEnemyIDs.Contains(id);

            Debug.Log($" → {id}: defeated={defeated}");

            if (!defeated)
                return false;
        }

        return true;
    }

    public void LoadQuestState(SaveData data)
    {
        defeatedEnemies = new HashSet<string>(data.defeatedQuestEnemies);
        questAccepted = data.questAccepted;
        rewardGiven = data.rewardGiven;
    }

    public void ClearQuestState()
    {
        defeatedEnemies.Clear();
        questAccepted = false;
        rewardGiven = false;
    }
}
