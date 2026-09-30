using System;
using System.Collections.Generic;

/// <summary>
/// Serializable data container for complete game state persistence.
/// Managed by SaveManager.cs for saving/loading; deserialized by GameManager.ApplySaveData().
/// Contains player stats, world state, inventory, and quest progress.
/// </summary>
[Serializable]
public class SaveData
{
    // ===================================================
    // Position & Checkpoint
    // ===================================================
    
    /// <summary>Name of the scene where player was saved.</summary>
    public string sceneName;
    /// <summary>Player position as float array [x, y, z]; restored by GameManager.ApplySaveData().</summary>
    public float[] playerPosition;
    /// <summary>Checkpoint position as float array [x, y, z]; used for death respawns.</summary>
    public float[] checkpointPosition;
    /// <summary>Flag indicating if player has activated a checkpoint in this playthrough.</summary>
    public bool hasCheckpoint;
    /// <summary>Human-readable timestamp of when save was created.</summary>
    public string timestamp;
    /// <summary>Flag indicating if player was sitting on a chair when saved; affects animation state on load.</summary>
    public bool wasSitting;

    // ===================================================
    // World State
    // ===================================================
    
    /// <summary>List of door/barrier IDs that were opened; prevents duplicate door opening on load.</summary>
    public List<string> openedDoors = new List<string>();
    /// <summary>List of item IDs picked up; prevents respawning collected items.</summary>
    public List<string> pickedUpItems = new List<string>();
    /// <summary>List of enemy IDs defeated in overworld; used for achievement tracking.</summary>
    public List<string> defeatedEnemies = new List<string>();

    // ===================================================
    // Inventory
    // ===================================================
    
    /// <summary>Serializable wrapper for inventory items to preserve quantity and ID.</summary>
    [Serializable]
    public class InventoryItemSave
    {
        /// <summary>Unique identifier of the item; matches ItemObject.cs itemID.</summary>
        public string itemID;
        /// <summary>Quantity of this item in inventory.</summary>
        public int quantity;
        public string pickedUpTime;
    }
    /// <summary>List of items and quantities from InventorySystem; restored by InventorySaveSystem.cs.</summary>
    public List<InventoryItemSave> inventoryItems = new List<InventoryItemSave>();

    // ===================================================
    // Quests
    // ===================================================
    
    /// <summary>List of quest-specific enemy IDs defeated; used by QuestManager for completion tracking.</summary>
    public List<string> defeatedQuestEnemies = new List<string>();
    /// <summary>Flag indicating if quest was accepted by player.</summary>
    public bool questAccepted;
    /// <summary>Flag indicating if quest reward was already given (prevents duplicate rewards).</summary>
    public bool rewardGiven;

    // ===================================================
    // Player Stats
    // ===================================================
    
    /// <summary>Player level; restored to PlayerStats.level by ApplySaveData().</summary>
    public int playerLevel;
    /// <summary>Maximum health capacity; restored to PlayerStats.MaxHealth.</summary>
    public int playerMaxHealth;
    /// <summary>Current health; restored to PlayerStats.currentHealth.</summary>
    public int playerCurrentHealth;
    /// <summary>Attack stat; restored to PlayerStats.attack.</summary>
    public int playerAttack;
    /// <summary>Defense stat; restored to PlayerStats.defense.</summary>
    public int playerDefense;

    /// <summary>Current bounty progress; restored to PlayerStats.currentBounty.</summary>
    public int playerCurrentBounty;
    /// <summary>Bounty threshold for next level; restored to PlayerStats.bountyToNextLevel.</summary>
    public int bountyToNextLevel;
    /// <summary>Multiplier for bounty scaling per level; restored to PlayerStats.bountyMultiplier.</summary>
    public float bountyMultiplier;

    // ===================================================
    // Upgrade Tracking
    // ===================================================
    
    /// <summary>Number of HP upgrades applied; for stat progression tracking.</summary>
    public int hpUpgrades;
    /// <summary>Number of attack upgrades applied; for stat progression tracking.</summary>
    public int atkUpgrades;
    /// <summary>Number of defense upgrades applied; for stat progression tracking.</summary>
    public int defUpgrades;

    /// <summary>Global bounty currency from BountyManager.cs; separate from player level bounty.</summary>
    public int globalBounty;

    // ===================================================
    // Player Deck
    // ===================================================
    public List<string> activeDeckMoveIDs = new List<string>();
    public List<string> unlockedMoveIDs = new List<string>();
}
