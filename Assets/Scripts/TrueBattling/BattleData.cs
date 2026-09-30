using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Static container for battle context data passed from overworld to battle scene.
/// Populated by EnemyBattleTrigger.cs before scene load, cleared on battle end by BattleManager.cs.
/// Accessed by BattleManager.cs for enemy setup and LevelUpUI.cs for victory display.
/// </summary>
public static class BattleData
{
    /// <summary>Reference to the EnemyParty data structure (all party members); set by EnemyBattleTrigger.cs.</summary>
    public static EnemyParty enemyParty;
    /// <summary>Arena color tint for battle scene; used by camera/environment systems.</summary>
    public static Color arenaColor;
    /// <summary>Position in overworld where player returns after battle exits.</summary>
    public static Vector3 playerReturnPosition;
    /// <summary>Prefab of the battle arena environment; instantiated by BattleManager.Start().</summary>
    public static GameObject battleMapPrefab;

    /// <summary>Unique ID of the enemy being fought; used for defeat tracking and cooldown application.</summary>
    public static string enemyID;
    /// <summary>Display name of the enemy; shown in battle UI and messages.</summary>
    public static string enemyName;

    /// <summary>Maximum health of the enemy party; set by EnemyBattleTrigger.cs and used in BattleManager.Start().</summary>
    public static int enemyMaxHealth;
    /// <summary>Current health of the enemy (may be different from max if continuing a battle).</summary>
    public static int enemyCurrentHealth;
    /// <summary>Flag to disable run option for certain boss/scripted battles.</summary>
    public static bool disableRunOption = false;
    /// <summary>Duration in seconds before the fled-from enemy can be fought again; set by BattleData and applied by GameManager.SetEnemyCooldown().</summary>
    public static float enemyCooldownDuration = 10f;
    /// <summary>List of runtime data for each enemy in the party; includes stats, memory, personality.</summary>
    public static List<EnemyRuntimeData> enemyPartyRuntime = new List<EnemyRuntimeData>();
    
    public static List<BattleManager.Turn> turnPattern = new List<BattleManager.Turn>()
    {
        BattleManager.Turn.Player,
        BattleManager.Turn.Enemy
    };

    /// <summary>
    /// Clears all battle data to reset between encounters.
    /// Called by BattleManager.Awake() and GameManager state transitions.
    /// </summary>
    public static void Clear()
    {
        enemyPartyRuntime.Clear();
        enemyID = "";
        enemyName = "";
        enemyMaxHealth = 0;
        enemyCurrentHealth = 0;
        enemyParty = null;
        battleMapPrefab = null;
        disableRunOption = false;
        turnPattern = new List<BattleManager.Turn>()
        {
            BattleManager.Turn.Player,
            BattleManager.Turn.Enemy
        };
    }
}
