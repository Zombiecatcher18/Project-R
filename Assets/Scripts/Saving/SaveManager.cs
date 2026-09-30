using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Static utility for serializing and deserializing game state to disk.
/// Manages JSON serialization of SaveData to persistent storage.
/// Used by SaveSlotSelectionUI.cs for save/load UI and GameManager for state persistence.
/// Stores saves in Application.persistentDataPath with slot-based file naming.
/// </summary>
public static class SaveManager
{
    /// <summary>
    /// Constructs file path for a save slot in persistent storage.
    /// Uses Application.persistentDataPath for platform-independent save location.
    /// </summary>
    private static string GetPath(int slot)
    {
        return Application.persistentDataPath + "/SaveSlot_" + slot + ".json";
    }

    /// <summary>
    /// Serializes SaveData to JSON and writes to disk at specified slot.
    /// Creates or overwrites existing save file.
    /// Called by SaveSlotSelectionUI.cs when player confirms save.
    /// </summary>
    public static void Save(int slot, SaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        string path = GetPath(slot);

        File.WriteAllText(path, json);

        Debug.Log($"[Save Manager] Saved slot {slot} to : {path}");
    }

    /// <summary>
    /// Loads SaveData from JSON file at specified slot.
    /// Returns null if save file does not exist.
    /// Called by SaveSlotSelectionUI.cs and GameManager.ApplySaveData().
    /// References SaveData.cs for deserialization target.
    /// </summary>
    public static SaveData Load(int slot)
    {
        string path = GetPath(slot);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveManager] No save file found for slot {slot} at: {path}");
            return null;
        }

        string json = File.ReadAllText(path);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        Debug.Log($"[SaveManager.Load] Slot {slot} loaded. Inventory count = {data.inventoryItems.Count}");
        for (int i = 0; i < data.inventoryItems.Count; i++)
        {
            var s = data.inventoryItems[i];
            Debug.Log($"  [Loaded] [{i}] {s.itemID} x{s.quantity}");
        }
        Debug.Log($"[SaveManager] Loaded slot {slot} from: {path}");
        return data;
    }

    /// <summary>
    /// Deletes save file at specified slot if it exists.
    /// Called by SaveSlotDeleteUI.cs for save deletion.
    /// </summary>
    public static void Delete(int slot)
    {
        string path = GetSlotPath(slot);

        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("[SaveManager] Deleted save slot " + slot);
        }
    }

    /// <summary>
    /// Constructs file path using System.IO.Path for cross-platform compatibility.
    /// </summary>
    private static string GetSlotPath(int slot)
    {
        string folder = Application.persistentDataPath;
        return System.IO.Path.Combine(folder, $"SaveSlot_{slot}.json");
    }

    /// <summary>
    /// Constructs a complete SaveData snapshot of current game state.
    /// Gathers player position, stats, world state, inventory, and quest progress.
    /// Called by SaveSlotSelectionUI.cs before calling Save().
    /// References PlayerStats, GameManager, InventorySystem, and QuestManager for state gathering.
    /// </summary>
    public static SaveData BuildSaveData()
    {
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 pos = player.transform.position;

        SaveData data = new SaveData();
        PlayerStats stats = PlayerStats.Instance;

        data.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        data.playerPosition = new float[] { pos.x, pos.y, pos.z };
        data.timestamp = System.DateTime.Now.ToString();

        // Checkpoint
        if (GameManager.Instance.hasCheckpoint)
        {
            data.hasCheckpoint = true;
            data.checkpointPosition = new float[]
            {
                GameManager.Instance.lastCheckpointPosition.x,
                GameManager.Instance.lastCheckpointPosition.y,
                GameManager.Instance.lastCheckpointPosition.z
            };
        }

        // WORLD STATE
        data.openedDoors = new List<string>(GameManager.Instance.doorsOpened.Keys);
        data.pickedUpItems = new List<string>(GameManager.Instance.pickedUpItemIDs);
        data.defeatedEnemies = new List<string>(GameManager.Instance.defeatedEnemyIDs);

        // INVENTORY
        foreach (var item in InventorySystem.current.consumables)
        {
            data.inventoryItems.Add(new SaveData.InventoryItemSave
            {
                itemID = item.data.itemID,
                quantity = item.quanity,
                pickedUpTime = item.pickedUpTime.ToString("o")
            });
        }

        foreach (var item in InventorySystem.current.keyItems)
        {
            data.inventoryItems.Add(new SaveData.InventoryItemSave
            {
                itemID = item.data.itemID,
                quantity = item.quanity,
                pickedUpTime = item.pickedUpTime.ToString("o")
            });
        }

        foreach (var item in InventorySystem.current.specialItems)
        {
            data.inventoryItems.Add(new SaveData.InventoryItemSave
            {
                itemID = item.data.itemID,
                quantity = item.quanity,
                pickedUpTime = item.pickedUpTime.ToString("o")
            });
        }

        foreach (var item in InventorySystem.current.battleItems)
        {
            data.inventoryItems.Add(new SaveData.InventoryItemSave
            {
                itemID = item.data.itemID,
                quantity = item.quanity,
                pickedUpTime = item.pickedUpTime.ToString("o")
            });
        }

        foreach (var item in InventorySystem.current.gear)
        {
            data.inventoryItems.Add(new SaveData.InventoryItemSave
            {
                itemID = item.data.itemID,
                quantity = item.quanity,
                pickedUpTime = item.pickedUpTime.ToString("o")
            });
        }

        // Save active deck
        foreach (var move in PlayerMoveDeck.Instance.masterPool)
        {
            data.activeDeckMoveIDs.Add(move.moveID);
        }

        // Save unlocked moves
        foreach (var move in PlayerMoveDeck.Instance.unlockedMoves)
        {
            data.unlockedMoveIDs.Add(move.moveID);
        }

        Debug.Log($"[SaveManager.BuildSaveData] Saving {data.inventoryItems.Count} inventory items:"); 
        for (int i = 0; i < data.inventoryItems.Count; i++) 
        { 
            var s = data.inventoryItems[i]; 
            Debug.Log($" [{i}] {s.itemID} x{s.quantity}"); 
        }

        // QUESTS
        data.defeatedQuestEnemies = new List<string>(QuestManager.Instance.defeatedEnemies);
        data.questAccepted = QuestManager.Instance.questAccepted;
        data.rewardGiven = QuestManager.Instance.rewardGiven;

        // PLAYER STATS
        data.playerLevel = stats.level;
        data.playerMaxHealth = stats.MaxHealth;
        data.playerCurrentHealth = stats.currentHealth;
        data.playerAttack = stats.attack;
        data.playerDefense = stats.defense;

        // BOUNTY
        data.playerCurrentBounty = stats.currentBounty;
        data.bountyToNextLevel = stats.bountyToNextLevel;
        data.bountyMultiplier = stats.bountyMultiplier;

        data.hpUpgrades = stats.hpUpgrades;
        data.atkUpgrades = stats.atkUpgrades;
        data.defUpgrades = stats.defUpgrades;

        data.globalBounty = BountyManager.Instance.currentBounty;

        return data;
    }
}
