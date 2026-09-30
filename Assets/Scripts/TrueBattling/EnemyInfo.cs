using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Scriptable object storing enemy configuration: stats, moves, and personality.
/// Used by EnemyScript for encounter setup and EnemyRuntimeData for battle initialization.
/// Defines both base stats and level-scaling parameters for dynamic difficulty.
/// Cloned at runtime to prevent persistent modifications to the asset.
/// </summary>
[CreateAssetMenu(menuName = "Battle/Enemy Data")]
public class EnemyInfo : ScriptableObject
{
    /// <summary>Unique identifier for this enemy type; used for save file matching and quest tracking.</summary>
    [Header("Identity")]
    public string enemyID;
    /// <summary>Display name shown in UI and logs.</summary>
    public string enemyName;
    /// <summary>Enemy category (e.g., "Goblin", "Troll") for organization and special case handling.</summary>
    public string enemyType;

    /// <summary>Base enemy level at spawn; determines stat calculations and party difficulty.</summary>
    [Header("Enemy Level")]
    public int baseLevel = 1;
    /// <summary>Maximum level scaling cap; controls upper bound of level-based stat growth.</summary>
    public int maxScalingLevel = 6;

    /// <summary>HP increase per level beyond baseLevel.</summary>
    public int hpPerLevel = 5;
    /// <summary>ATK increase per level beyond baseLevel.</summary>
    public int atkPerLevel = 1;
    /// <summary>DEF increase per level beyond baseLevel.</summary>
    public int defPerLevel = 1;

    public int counteratkPerLevel = 1; // scaling per level

    /// <summary>Combo slots added per level; scales turn complexity with difficulty.</summary>
    public float comboSlotsPerLevel = 1f;

    /// <summary>Minimum stat value from random roll (added to base stats).</summary>
    public int statRollMin = 1;
    /// <summary>Maximum stat value from random roll (added to base stats).</summary>
    public int statRollMax = 10;

    [Header("AI Hand Settings")]
    public int handSizeOverride = -1; // -1 means use default (decksize + comboSlots)
    // Any positive number means “force this enemy to draw exactly this many cards”
    
    /// <summary>
    /// Additional cards added to handSizeOverride per level.
    /// Example: handSizeOverride = 4, handSizePerLevel = 1 → L1=4, L2=5, L3=6...
    /// </summary>
    public int handSizePerLevel = 0;

    /// <summary>Base health at baseLevel before level scaling.</summary>
    [Header("Stats")]
    [Tooltip("WHAT: Base health points at baseLevel. HOW: Scaled by level during enemy spawn. AFFECTS: How many hits enemy can take.")]
    public int maxHP = 50;
    
    /// <summary>Base attack power; used in damage calculation formula (ATK * 0.04f multiplier in BattleManager.cs).</summary>
    [Tooltip("WHAT: Base attack stat before personality scaling. HOW: Multiplied by personality damageMultiplier then ×0.04 in damage formula. AFFECTS: Enemy damage output and combat threat.")]
    public int attack = 10;
    
    /// <summary>Base defense value; used in damage reduction formula (100/(100+defense) in PlayerStats.TakeDamage()).</summary>
    [Tooltip("WHAT: Base defense stat for damage reduction. HOW: Used in formula 100/(100+defense) to reduce incoming damage. AFFECTS: Enemy survivability and player advantage.")]
    public int defense = 5;
    public int counteratk = 1; // base counter attack value
    public int baseDeckSize = 4;
    public int deckSizePerLevel = 1;

    /// <summary>Currency reward for defeating this enemy; added to PlayerStats.bounty.</summary>
    [Header("Rewards")]
    [Tooltip("WHAT: Currency awarded to player for defeating this enemy. HOW: Added to player bounty on victory. AFFECTS: Progression pacing and resource gain.")]
    public int bountyReward = 10;

    /// <summary>Visual color for enemy capsule representation in overworld.</summary>
    [Header("Appearance")]
    public Color capsuleColor = Color.red;

    /// <summary>Available moves for this enemy; selected randomly for combo building by AI systems.</summary>
    [Header("Moves (Deck)")]
    public List<EnemyMove> moves;
    /// <summary>Maximum combo slots available for this enemy; dynamically increased with levels.</summary>
    public int maxComboSlots = 3;

    /// <summary>Probability of using fakeout attacks; range 0-1 for EnemyAttackController fakeout bias.</summary>
    [Header("AI Behavior")]
    [Tooltip("WHAT: Base chance enemy uses fakeout deceptions. HOW: Multiplied by personality fakeoutChanceMultiplier. AFFECTS: How often enemy tricks player with fake attacks.")]
    [Range(0f, 1f)]
    public float fakeoutBias = 0.25f;

    /// <summary>AI difficulty multiplier; affects decision-making sophistication in personality systems.</summary>
    public float tricksterFactor = 1f;

    /// <summary>Personality profile scriptable object defining behavior patterns (combat style, move preferences).</summary>
    [Header("Personality")]
    public EnemyPersonalityProfile personality;

    // ===== SAFE HELPERS =====

    /// <summary>
    /// Checks if this enemy has at least one move available.
    /// Safe null check without LINQ; used by AI systems to validate move deck before selection.
    /// </summary>
    public bool HasMoves => moves != null && moves.Count > 0;

    /// <summary>
    /// Calculates combo slots for enemy at given level.
    /// Formula: maxComboSlots + (level - 1) * comboSlotsPerLevel, clamped to 1-10 range.
    /// Called by EnemyRuntimeData during initialization to determine turn complexity.
    /// </summary>
    public int GetComboSlotsForLevel(int level)
    {
        if (level <= 1)
            return maxComboSlots;

        float extra = (level - 1) * comboSlotsPerLevel;
        return Mathf.Clamp(Mathf.RoundToInt(maxComboSlots + extra), 1, 10);
    }

    /// <summary>
    /// Validates and initializes move list if null.
    /// Called during initialization to ensure safe move access throughout battle.
    /// </summary>
    public void ValidateMoves()
    {
        if (moves == null)
            moves = new List<EnemyMove>();
    }

    public int GetDeckSizeForLevel(int level)
    {
        return baseDeckSize + deckSizePerLevel * (level - 1);
    }
}
