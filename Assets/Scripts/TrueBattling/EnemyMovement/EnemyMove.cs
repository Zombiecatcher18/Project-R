using UnityEngine;

/// <summary>
/// Scriptable object defining enemy attack move properties and behavior.
/// Used by EnemyAttackController for turn-based combat and AIComboBuilder for move selection.
/// Stores damage values, QTE requirements, fakeout mechanics, and personality tags.
/// </summary>
[CreateAssetMenu(menuName = "Battle/Enemy Move")]
public class EnemyMove : ScriptableObject
{
    /// <summary>Display name for enemy moves.</summary>
    [Header("Basic Info")]
    public string moveName;
    /// <summary>Combo slot cost; restricts how many moves fit in enemy combo.</summary>
    public int slotCost = 1;

    /// <summary>Base damage when move connects; scaled by enemy attack stat in EnemyAttackController.</summary>
    [Header("Damage Values")]
    public int baseDamage = 10;
    /// <summary>Damage dealt if player fails the QTE (if requiresQTE is true).</summary>
    public int damageOnMiss = 10;
    /// <summary>Damage if player counters move during QTE (special condition).</summary>
    public int counterAttackDamage = 5;

    /// <summary>Pre-delay minimum for enemy button QTE in seconds.</summary>
    [Header("Enemy Button QTE Settings")]
    public float enemyButtonPreDelayMin = 0.3f;
    /// <summary>Pre-delay maximum for enemy button QTE in seconds; random range per QTE.</summary>
    public float enemyButtonPreDelayMax = 0.8f;

    /// <summary>Gets random pre-delay for enemy button QTE within min/max range.</summary>
    public float GetEnemyButtonPreDelay()
    {
        return Random.Range(enemyButtonPreDelayMin, enemyButtonPreDelayMax);
    }

    /// <summary>Whether this move requires player QTE response during damage phase.</summary>
    [Header("QTE Settings")]
    public bool requiresQTE = false;
    /// <summary>Type of QTE if requiresQTE is true (Timing or Button).</summary>
    public QTEType qteType;
    /// <summary>QTE speed multiplier (higher = faster/harder).</summary>
    public float qteSpeed = 1f;
    public float qteTimeLimit = 1.0f;
    /// <summary>Possible button options if qteType is Button.</summary>
    public KeyCode[] possibleButtons;

    /// <summary>Whether this move can use a fakeout (fake start before real attack).</summary>
    [Header("Fakeout Settings")]
    public bool hasFakeout = false;
    /// <summary>Whether fakeout itself requires QTE (adds layer of complexity).</summary>
    public bool fakeoutRequiresQTE = false;
    /// <summary>Probability 0-1 that fakeout will be used if player encounters this move.</summary>
    public float fakeoutChance = 0.1f;

    /// <summary>Array of move tags for categorization and AI decision-making.</summary>
    [Header("Move Tags")]
    public MoveTag[] tags;

    // ===== TAG SYSTEM =====

    /// <summary>
    /// Checks if move has specified tag.
    /// Called by personality systems and AI builders for move categorization.
    /// Safe null-check implementation.
    /// </summary>
    public bool HasTag(MoveTag tag)
    {
        if (tags == null) return false;
        foreach (var t in tags)
            if (t == tag) return true;
        return false;
    }

    /// <summary>True if move has Heavy tag or baseDamage >= 18; used by AI for aggressive decisions.</summary>
    public bool isHeavy => HasTag(MoveTag.Heavy) || baseDamage >= 18;
    /// <summary>True if move has MultiHit tag; used by AI for positioning and combo decisions.</summary>
    public bool isMultiHit => HasTag(MoveTag.MultiHit);
    /// <summary>True if move has Counter tag; used for defensive/reactive positioning.</summary>
    public bool isCounter => HasTag(MoveTag.Counter);

    /// <summary>
    /// Composite value for move strength used by personality combo patterns.
    /// Formula: (baseDamage * 0.5) + (damageOnMiss * 0.3) + (critChance * 20).
    /// Called by Conqueror and other combat patterns for move ranking.
    /// </summary>
    public float scalingValue =>
        (baseDamage * 0.5f) +
        (damageOnMiss * 0.3f) +
        (critChance * 20f);
        
    /// <summary>Probability 0-1 for critical hit on successful attack.</summary>
    [Header("Crit Settings")]
    public float critChance = 0.05f;
    /// <summary>Multiplier applied to baseDamage on critical hit; typical range 1.2-2.0.</summary>
    public float critMultiplier = 1.5f;

    /// <summary>If true, damage bypasses player defense calculation formula.</summary>
    [Header("Special Flags")]
    public bool ignoresDefense = false;

    /// <summary>Personality/style indicator for move selection bias; affects aggression weighting.</summary>
    [Header("AI Utility")]
    public float trickiness = 0f;

    /// <summary>
    /// Converts tags into actionable gameplay stats.
    /// </summary>
    public TagModifiers GetTagModifiers()
    {
        // Default values (Neutral)
        TagModifiers mods = new TagModifiers
        {
            damageMult = 1f,
            qteSpeedMult = 1f,
            fakeoutChanceMult = 1f,
            hitCount = 1, 
            // We use the Inspector value as the baseline.
            ignoreDefense = this.ignoresDefense, 
            isExecute = false,
            isAntiTank = false,
            isCounter = false
        };

        if (tags == null) return mods;

        foreach (var tag in tags)
        {
            switch (tag)
            {
                // --- COMBAT STYLE ---
                case MoveTag.Heavy:
                    mods.damageMult *= 1.4f;     // Heavy hits 40% harder
                    mods.qteSpeedMult *= 1.25f;  // QTE is slower/heavier
                    break;

                case MoveTag.Fast:
                    mods.damageMult *= 0.75f;    // Fast hits weaker
                    mods.qteSpeedMult *= 0.8f;   // QTE is 20% faster
                    break;

                case MoveTag.MultiHit:
                    mods.hitCount = 3;           // Default to 3 hits for MultiHit tags
                    mods.damageMult *= 0.4f;     // Each hit does 40% damage (Total 120%)
                    break;

                case MoveTag.Counter:
                    mods.isCounter = true;       // Flags this for the Counter System
                    break;

                // --- ARMOR / DEFENSE ---
                case MoveTag.ArmorPiercing:
                    mods.ignoreDefense = true;   // Bypasses defense formula
                    break;

                case MoveTag.AntiTank:
                    mods.isAntiTank = true;      // Flags logic to check Player DEF
                    break;

                // --- MIND GAMES ---
                case MoveTag.FakeoutHeavy:
                    mods.fakeoutChanceMult *= 2.0f; // Double chance to fakeout
                    mods.damageMult *= 1.2f;        // Slightly stronger
                    break;

                case MoveTag.Execute:
                    mods.isExecute = true;       // Flags logic to check Player Low HP
                    break;
            }
        }
        return mods;
    }
}

// ---------------------------------------------------------
// SUPPORTING DATA STRUCTURES
// ---------------------------------------------------------

/// <summary>
/// Category tags for moves used by personality systems and combo builders.
/// Enables flexible move categorization for tactical decision-making.
/// </summary>
public enum MoveTag
{
    None,

    // Damage / Combat Style
    Heavy,              // High damage moves; triggers aggressive personality response
    MultiHit,           // Multiple hits in succession; affects positioning
    Counter,            // Defensive/reactive moves; triggers defensive patterns
    Fast,               // Low slot cost; enables quick combos

    // Defense / Armor
    ArmorPiercing,      // Ignores defense; triggers armor-piercing bias
    AntiTank,           // Specialized vs high-defense opponents

    // Mind Games / Tactical
    FakeoutHeavy,       // Heavy move with high fakeout chance
    Execute             // Damage increases with target low health; finishing move
}

/// <summary>
/// A container for all the stat changes a tag applies.
/// </summary>
public struct TagModifiers
{
    public float damageMult;      // For Heavy, Fast
    public float qteSpeedMult;    // For Heavy, Fast
    public float fakeoutChanceMult; // For FakeoutHeavy
    public int hitCount;          // For MultiHit
    public bool ignoreDefense;    // For ArmorPiercing
    public bool isExecute;        // For Execute logic
    public bool isAntiTank;       // For AntiTank logic
    public bool isCounter;        // For Counter logic
}