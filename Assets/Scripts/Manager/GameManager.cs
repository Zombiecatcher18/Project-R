using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public class GameManager : MonoBehaviour
{
    /// <summary>Singleton instance for global world state and scene management.</summary>
    public static GameManager Instance;

    [Header("UI Prefabs")]
    /// <summary>Prefab instantiated on battle scenes for inventory access during combat.</summary>
    public GameObject inventoryUIPrefab;

    [Header("Battle Data")]
    /// <summary>Player's position in the overworld before entering battle; restored on return.</summary>
    public Vector3 playerPositionBeforeBattle;
    /// <summary>Name of the scene the player was in before battle started.</summary>
    public string sceneBeforeBattle;
    /// <summary>Y-coordinate baseline for battle scene floor positioning.</summary>
    public float battleFloorY = 0f;
    /// <summary>Flag set when returning to overworld from battle scene.</summary>
    public bool returningFromBattle = false;
    /// <summary>Flag set if player was defeated in battle; triggers respawn logic.</summary>
    public bool diedInBattle = false;
    /// <summary>Timestamp when battle cooldown expires; prevents immediate re-encounter with same enemy.</summary>
    private float battleCooldownEndTime = 0f;

    /// <summary>ID of the last enemy the player battled.</summary>
    public string playerLastBattledID = "";
    /// <summary>Last spawn point ID used for player position restoration.</summary>
    public string lastSpawnID;

    [HideInInspector]
    public bool runAwayTriggered = false;


    [HideInInspector] 
    /// <summary>Pending save data to apply after scene load; set by SaveManager and applied in HandleSceneLoaded().</summary>
    public SaveData pendingLoadData;

    [HideInInspector]
    /// <summary>Current save slot index (-1 if no active save slot).</summary>
    public int currentSlot = -1;

    /// <summary>Flag indicating save data is being loaded from disk.</summary>
    public bool loadingFromSave = false;
    /// <summary>Position of the last activated checkpoint; used for death respawn.</summary>
    public Vector3 lastCheckpointPosition;
    /// <summary>Flag indicating if a checkpoint has been activated in this playthrough.</summary>
    public bool hasCheckpoint = false;

    [Header("World State")]
    /// <summary>Set of enemy IDs currently active in quest systems; managed by QuestManager.</summary>
    public HashSet<string> questActiveEnemyIDs = new HashSet<string>();
    /// <summary>Dictionary of door states (open/closed) for persistent environmental state.</summary>
    public Dictionary<string, bool> doorsOpened = new Dictionary<string, bool>();
    /// <summary>List of item IDs picked up by player; prevents duplicate item spawns.</summary>
    public List<string> pickedUpItemIDs = new List<string>();
    /// <summary>Set of enemy IDs defeated by player; used for quest tracking and achievement systems.</summary>
    public HashSet<string> defeatedEnemyIDs = new HashSet<string>();

    [Header("Persistent Objects")]
    /// <summary>GameObjects marked as persistent across scene loads using DontDestroyOnLoad.</summary>
    public GameObject[] persistentObjects;

    /// <summary>Serializable wrapper for inventory save data; referenced by InventorySaveSystem.cs.</summary>
    [System.Serializable]
    public class SavedInventoryItem
    {
        /// <summary>Unique identifier for the inventory item.</summary>
        public string itemID;
        /// <summary>Quantity of this item in inventory.</summary>
        public int quantity;
    }
    [HideInInspector]
    /// <summary>Inventory items saved from previous session; applied during save load.</summary>
    public List<SavedInventoryItem> savedInventory = new List<SavedInventoryItem>();

    /// <summary>
    /// Initializes singleton instance, registers scene callbacks, and marks persistent objects.
    /// Called automatically by Unity before Start().
    /// </summary>
    private void Awake()
    {
        BattleData.Clear();
        returningFromBattle = false;

        if (Instance != null && Instance != this)
        {
            //Debug.LogWarning("[GameManager] Duplicate found and destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        MarkPersistentObjects();
    }

    public void SaveGame()
    {
        SaveData data = SaveManager.BuildSaveData();
        SaveManager.Save(currentSlot, data);
        //Debug.Log("[GameManager] Game saved to slot " + currentSlot);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Instance = null;
        }
    }

    private void MarkPersistentObjects()
    {
        if (persistentObjects == null) return;

        foreach (GameObject obj in persistentObjects)
        {
            if (obj != null)
                DontDestroyOnLoad(obj);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(HandleSceneLoaded(scene));
    }

    private void OnSceneUnloaded(Scene scene)
    {
        // Light-weight: just auto-save inventory if exists
        //InventorySystem.current?.SaveInventory();
    }

    private IEnumerator HandleSceneLoaded(Scene scene)
    {
        // Wait one frame so scene objects are initialized
        yield return null;
        
        if (pendingLoadData != null && loadingFromSave)
        {
            LoadWorldState(pendingLoadData);
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            yield break;

        if (scene.name == "BattleScene")
            yield break;

        AssignPlayerToMarkedCameras();
        RemoveExtraPlayers();

        // Apply save data once after scene is loaded
        if (pendingLoadData != null && loadingFromSave)
        {
            // Wait until InventorySystem exists
            yield return new WaitUntil(() => InventorySystem.current != null);
            //Debug.Log("[GameManager] About to apply save data...");
            ApplySaveData(player, pendingLoadData);
            //Debug.Log("[GameManager] InventorySystem.current is: " + (InventorySystem.current != null ? InventorySystem.current.gameObject.name : "NULL"));

            pendingLoadData = null;
            loadingFromSave = false;
        }

        // Returning from battle
        if (returningFromBattle && scene.name == sceneBeforeBattle)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            if (diedInBattle && hasCheckpoint)
            {
                player.transform.position = lastCheckpointPosition;
                int halfHP = Mathf.CeilToInt(PlayerStats.Instance.MaxHealth / 2f);
                PlayerStats.Instance.currentHealth = halfHP;
                //Debug.Log($"[GameManager] Player died in battle. Respawning at checkpoint with {halfHP} HP.");
            }
            else
            {
                player.transform.position = playerPositionBeforeBattle;
                //Debug.Log($"[GameManager] Player returned to {playerPositionBeforeBattle}");
            }

            if (cc != null) cc.enabled = true;

            //SaveData data = SaveManager.BuildSaveData(); 
            //data.wasSitting = false; 
            //SaveManager.Save(currentSlot, data);

            returningFromBattle = false;
            diedInBattle = false;
            SaveGame();
        }

        // Apply cooldown to the enemy we ran from
        if (runAwayTriggered)
        {
            var enemy = FindEnemyByID(playerLastBattledID);
         if (enemy != null)
            {
                enemy.StartBattleCooldown(BattleData.enemyCooldownDuration);
                //Debug.Log($"[GameManager] Applied cooldown to enemy {playerLastBattledID}");
            }

            runAwayTriggered = false;
        }

    }

    public void ApplySaveData(GameObject player, SaveData data)
    {
        //Debug.Log("[GameManager] Applying saved data...");
        //Debug.Log("[GameManager] ApplySaveData started.");

        // POSITION
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        if (data.playerPosition != null && data.playerPosition.Length == 3)
        {
            player.transform.position = new Vector3(
                data.playerPosition[0],
                data.playerPosition[1],
                data.playerPosition[2]
            );
            //Debug.Log("[GameManager] Player moved to saved position.");
        }

        if (data.hasCheckpoint)
        {
            hasCheckpoint = true;
            lastCheckpointPosition = new Vector3(
                data.checkpointPosition[0],
                data.checkpointPosition[1],
                data.checkpointPosition[2]
            );
        }

        if (cc != null) cc.enabled = true;

        // WORLD STATE
        LoadWorldState(data);
        //Debug.Log("[GameManager] InventorySystem.current is: " + (InventorySystem.current != null ? InventorySystem.current.gameObject.name : "NULL"));
        //Debug.Log("[GameManager] InventorySystem.current hash: " + InventorySystem.current.GetHashCode());

        // INVENTORY
        if (InventorySystem.current != null)
        {
            //Debug.Log($"[GameManager.ApplySaveData] Loading inventory, count = {data.inventoryItems.Count}");
            InventorySystem.current.LoadInventoryFromSave(data.inventoryItems);
        }

        // QUESTS
        if (QuestManager.Instance != null)
            QuestManager.Instance.LoadQuestState(data);

        // PLAYER STATS
        if (PlayerStats.Instance != null)
        {
            PlayerStats stats = PlayerStats.Instance;

            stats.level = data.playerLevel;
            stats.MaxHealth = data.playerMaxHealth;
            stats.currentHealth = data.playerCurrentHealth;
            stats.attack = data.playerAttack;
            stats.defense = data.playerDefense;

            stats.currentBounty = data.playerCurrentBounty;
            stats.bountyToNextLevel = data.bountyToNextLevel;
            stats.bountyMultiplier = data.bountyMultiplier;

            stats.hpUpgrades = data.hpUpgrades;
            stats.atkUpgrades = data.atkUpgrades;
            stats.defUpgrades = data.defUpgrades;
        }

        // ===============================
        // Restore Player Deck
        // ===============================
        PlayerMoveDeck deck = PlayerMoveDeck.Instance;

        // Clear current deck
        deck.masterPool.Clear();
        deck.unlockedMoves.Clear();

        // Rebuild active deck
        foreach (string id in data.activeDeckMoveIDs)
        {
            AttackMove move = Resources.Load<AttackMove>($"Moves/{id}");
            if (move != null)
                deck.masterPool.Add(move);
        }

        //Rebuild unlocked moves
        foreach (string id in data.unlockedMoveIDs)
        {
            AttackMove move = Resources.Load<AttackMove>($"Moves/{id}");
            if (move != null)
                deck.unlockedMoves.Add(move);
        }

        // BOUNTY MANAGER
        if (BountyManager.Instance != null)
        {
            BountyManager.Instance.currentBounty = data.globalBounty;
        }
    }

    // WORLD STATE HELPERS
    public void LoadWorldState(SaveData data)
    {
        doorsOpened.Clear();
        foreach (string id in data.openedDoors)
            doorsOpened[id] = true;

        pickedUpItemIDs = new List<string>(data.pickedUpItems);
        defeatedEnemyIDs = new HashSet<string>(data.defeatedEnemies);
    }

    public void ClearWorldState()
    {
        doorsOpened.Clear();
        pickedUpItemIDs.Clear();
        defeatedEnemyIDs.Clear();
    }

    // DOOR HELPERS
    public bool IsDoorOpen(string id) => doorsOpened.ContainsKey(id) && doorsOpened[id];
    public void SetDoorOpen(string id) => doorsOpened[id] = true;

    // BATTLE HELPERS
    public void SaveBattleStartPosition(Vector3 playerPos)
    {
        playerPositionBeforeBattle = playerPos;
        battleFloorY = playerPos.y;
        sceneBeforeBattle = SceneManager.GetActiveScene().name;
        //Debug.Log($"[GameManager] Saved battle start position: {playerPositionBeforeBattle}");
    }

    public void RunFromBattle(Vector3 enemyPosition)
    {
        StartCoroutine(RunFromBattleCoroutine(enemyPosition));
    }

    private IEnumerator RunFromBattleCoroutine(Vector3 enemyPosition)
    {
        yield return null;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            //Debug.LogError("Player not found when attempting to run from battle!");
            yield break;
        }

        //Debug.Log($"[RunFromBattle] Teleporting player to position: {playerPositionBeforeBattle}");

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = playerPositionBeforeBattle;

        if (cc != null) cc.enabled = true;

        Collider[] hits = Physics.OverlapSphere(playerPositionBeforeBattle, 5f);
        foreach (var hit in hits)
        {
            EnemyScript enemy = hit.GetComponent<EnemyScript>();
            if (enemy != null)
            {
                enemy.ActiveRunAwayLock();
            }
        }

        yield return new WaitForEndOfFrame();

        returningFromBattle = true;
        SceneManager.LoadScene(sceneBeforeBattle);
        //Debug.Log($"[RunFromBattle] Loading scene: {sceneBeforeBattle}");
    }

    private void AssignPlayerToMarkedCameras()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            //Debug.LogWarning("GameManager: Player not found in scene!");
            return;
        }

        FollowPlayerCameraMarker[] markers = FindObjectsOfType<FollowPlayerCameraMarker>();
        foreach (var marker in markers)
        {
            CinemachineCamera vCam = marker.GetComponent<CinemachineCamera>();
            if (vCam != null)
            {
                vCam.Follow = player.transform;
                vCam.LookAt = player.transform;
            }
        }
    }

    private void RemoveExtraPlayers()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length <= 1) return;

        for (int i = 1; i < players.Length; i++)
        {
            Destroy(players[i]);
        }
    }

    // COOLdowns
    public void SetBattleCooldown(float duration) => battleCooldownEndTime = Time.time + duration;
    public bool CanTriggerBattle() => Time.time >= battleCooldownEndTime;

    public Dictionary<string, float> enemyCooldowns = new Dictionary<string, float>();
    public void SetEnemyCooldown(string enemyID, float duration) => enemyCooldowns[enemyID] = Time.time + duration;
    public bool IsEnemyOnCooldown(string enemyID)
    {
        if (enemyCooldowns.TryGetValue(enemyID, out float endTime))
            return Time.time < endTime;
        return false;
    }
    public void ResetEnemyCooldown(string enemyID)
    {
        if (string.IsNullOrEmpty(enemyID)) return;
        enemyCooldowns[enemyID] = 0f;
        //Debug.Log("[GameManager] Reset cooldown for " + enemyID);
    }

    public EnemyScript FindEnemyByID(string id)
    {
        foreach (var enemy in FindObjectsOfType<EnemyScript>())
        {
            if (enemy.enemyID == id)
                return enemy;
        }
        return null;
    }
}
