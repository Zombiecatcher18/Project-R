using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Scriptable Object defining an enemy personality type's combat behavior and stat preferences.
/// 
/// Purpose: Centralized configuration for how an enemy personality should scale stats, choose moves,
/// and behave in combat. Decouples personality-specific values from battle logic.
/// 
/// Architecture:
/// - Created via Unity Inspector: "Battle/Enemy Personality Profile"
/// - Referenced by EnemyRuntimeData for scaling decisions
/// - Referenced by PersonalityMoveAdapter for move selection bias
/// - Referenced by PersonalityComboPatterns for combo strategy
/// - Referenced by EnemyPartySpawner for stat distribution during spawning
/// 
/// Key Systems:
/// 1. IDENTITY: personalityType and personalityName for debugging/UI
/// 2. STAT WEIGHTING: statWeights list for allocating bonus stats during level-up
/// 3. MOVE STAT MODIFIERS: damageMultiplier, qteSpeedMultiplier, etc. for personality-specific move scaling
/// 4. BEHAVIOR FLAGS: Boolean toggles for move selection preferences (prefersMultiHit, forbidFakeouts, etc.)
/// 5. BASELINE BIAS: Fine-tuning move selection biases (extraAggressiveBias, extraFakeoutBias, etc.)
/// 6. COMBO PATTERN: Strategy enum (Balanced/Aggressive/Defensive/Unpredictable) for combo sequencing
/// 
/// Cross-references:
/// - Used by EnemyPartySpawner.cs for stat scaling during spawn
/// - Used by PersonalityMoveAdapter.cs for move selection
/// - Used by PersonalityTurnLogic.cs for turn decision making
/// - Used by EnemyRuntimeData.cs as configuration storage
/// </summary>
[CreateAssetMenu(menuName = "Battle/Enemy Personality Profile")]
public class EnemyPersonalityProfile : ScriptableObject
{
    // ---------------------------------------------------------
    // IDENTITY
    // ---------------------------------------------------------
    /// <summary>
    /// Enum type of this personality (BountyHunter, Explorer, MilitaryAdmiral, etc.).
    /// Used to identify personality in logs, synergy calculations, and move selection.
    /// Each type has unique stat weights and behavior flags defined below.
    /// 
    /// Referenced by: EnemySynergyMatrix for affinity lookups, PersonalityMoveAdapter for behavior
    /// </summary>
    [Header("Identity")]
    public EnemyPersonalityType personalityType;

    /// <summary>
    /// Human-readable name for debugging and UI (e.g., "The Explorer", "Noble Warrior").
    /// Displayed in logs when personality is referenced.
    /// </summary>
    [Tooltip("Display name for debugging and UI.")]
    public string personalityName;

    /// <summary>
    /// List of stat weight preferences for level-up allocation.
    /// Example: [ATK:2.0, DEF:1.0, HP:0.5, SLOTS:0.8]
    /// Stat names: "ATK", "DEF", "HP", "SLOTS"
    /// 
    /// Used by EnemyPartySpawner.ApplyPersonalityRolls() to distribute bonus stats.
    /// Higher weight = more points allocated to that stat per level.
    /// </summary>
    [Tooltip("Stat weighting preferences for scaling logic.")]
    public List<StatWeight> statWeights;

    // ---------------------------------------------------------
    // COMBO PATTERN
    // ---------------------------------------------------------
    /// <summary>
    /// Overall combo strategy for this personality (Balanced/Aggressive/Defensive/Unpredictable).
    /// Controls move sequencing tendency during turn-based combat.
    /// 
    /// Balanced: Mixes move types evenly
    /// Aggressive: Favors high-damage heavy/multi-hit moves
    /// Defensive: Favors defensive/counter moves
    /// Unpredictable: Random move selection with high diversityBias
    /// 
    /// Used by PersonalityComboPatterns.SelectNextMove() for sequencing decisions.
    /// </summary>
    [Header("Combo Pattern")]
    public ComboPattern comboPattern = ComboPattern.Balanced;

    [Header("Turn Meter Influence")]
    [Tooltip("WHAT: Multiplier to turn meter gain on perfect QTE blocks. HOW: Multiplies meter gain in BattleManager damage calculation. AFFECTS: How fast enemies build turn meter on successful attacks.")]
    [Range(0.1f, 3f)] public float meterGainMultiplier = 1f;     // Perfect
    
    [Tooltip("WHAT: Multiplier to turn meter gain on good QTE blocks. HOW: Scales meter gain result. AFFECTS: Turn pacing and enemy action frequency.")]
    [Range(0.1f, 3f)] public float meterNeutralMultiplier = 1f;  // Good
    
    [Tooltip("WHAT: Multiplier to turn meter gain on missed/ok QTE blocks. HOW: Penalizes successful attacks with meter reduction. AFFECTS: Risk-reward balance of landing moves.")]
    [Range(0.1f, 3f)] public float meterLossMultiplier = 1f;     // Ok / Miss

    [Header("Pattern Learning")]
    [Range(0.1f, 3f)] public float awarenessGainMultiplier = 1f;
    [Range(0.1f, 3f)] public float predictionGainMultiplier = 1f;
    [Range(0.1f, 3f)] public float predictionDecayMultiplier = 1f;

    [Header("Counter Settings")]
    [Range(0f, 1f)] public float baseCounterChance = 0.2f; // 20% base chance to counter on miss
    public float counterDamageMultiplier = 1.2f;         // Counter moves do 20% more damage    
    [Range(1f, 200f)]
    public float awarenessThreshold = 20f;

    [Header("QTE Damage Multipliers")]
    [Tooltip("Multiplier applied when the player gets a GOOD dodge.")]
    public float qteGoodDamageMultiplier = 0.5f;

    [Tooltip("Multiplier applied when the player gets an OK dodge.")]
    public float qteOkDamageMultiplier = 0.75f;

    [Tooltip("Multiplier applied when the player MISSES the QTE.")]
    public float qteMissDamageMultiplier = 1f;

    /// <summary>
    /// Controls how strictly the personality adheres to its combo pattern.
    /// Range: 0 = strict pattern adherence, 1 = completely random within pattern
    /// 
    /// Example:
    /// - 0.0: Always follow exact pattern sequence (Heavy→MultiHit→Fakeout...)
    /// - 0.5: Mix pattern moves with occasional deviations
    /// - 1.0: Pattern only defines move type weights, not actual sequence
    /// 
    /// Used by PersonalityComboPatterns.SelectNextMove() to add variety to combos.
    /// </summary>
    [Range(0f, 1f)]
    public float diversityBias = 0.2f; // 0 = strict, 1 = very diverse

    // ---------------------------------------------------------
    // MOVE STAT MULTIPLIERS
    // ---------------------------------------------------------
    /// <summary>
    /// Multiplies all damage values for this personality's moves.
    /// Range: 0.5 (half damage) to 1.5 (50% bonus damage)
    /// 
    /// Example: damageMultiplier = 1.2 means all moves do 20% more damage
    /// 
    /// Applied by PersonalityMoveAdapter when selecting/scaling moves.
    /// Affects BattleManager damage calculations.
    /// </summary>
    [Header("Move Stat Multipliers")]
    [Tooltip("WHAT: Global damage scaling for all this personality's moves. HOW: Multiplies baseDamage of every move in move selection. AFFECTS: Overall combat difficulty and engagement challenge.")]
    [Range(0.5f, 1.5f)] public float damageMultiplier = 1f;

    /// <summary>
    /// Multiplies QTE timing window for this personality's moves.
    /// Range: 0.5 (half time) to 1.5 (50% more time)
    /// 
    /// Higher = easier QTE to block (more time for player)
    /// Lower = harder QTE to block (less time for player)
    /// 
    /// Applied during QTEManager move setup.
    /// </summary>
    [Tooltip("WHAT: Multiplier to QTE timing window difficulty for this personality. HOW: 1.0=normal, 0.5=easier (more time), 1.5=harder (less time). AFFECTS: How difficult it is to block/dodge this personality's attacks.")]
    [Range(0.5f, 1.5f)] public float qteSpeedMultiplier = 1f;

    /// <summary>
    /// Multiplies fakeout occurrence chance for this personality.
    /// Range: 0 (never fakeout) to 2 (2x more fakeouts)
    /// 
    /// Used by PersonalityMoveAdapter.FakeoutInfluence() to bias fakeout selection.
    /// Can be overridden by forceFakeouts/forbidFakeouts flags.
    /// </summary>
    [Tooltip("WHAT: Multiplier to fakeout chance for this personality's moves. HOW: Scales individual move fakeoutChance values. AFFECTS: How often fake-outs trick the player.")]
    [Range(0f,   2f)]   public float fakeoutChanceMultiplier = 1f;

    /// <summary>
    /// Multiplies critical hit chance for this personality's moves.
    /// Range: 0.5 to 1.5
    /// </summary>
    [Range(0.5f, 1.5f)] public float critChanceMultiplier = 1f;

    /// <summary>
    /// Multiplies stat scaling bonus multiplier (for scaling moves like Heavy).
    /// Range: 0.5 to 1.5
    /// 
    /// Example: scalingMultiplier = 1.3 means (ATK/100) scaling becomes (ATK/100)*1.3
    /// </summary>
    [Range(0.5f, 1.5f)] public float scalingMultiplier = 1f;

    // ---------------------------------------------------------
    // BEHAVIOR FLAGS
    // ---------------------------------------------------------
    /// <summary>
    /// Section: Hard constraints on move selection behavior.
    /// These override normal move selection logic when true.
    /// </summary>
    [Header("Behavior Flags")]

    /// <summary>
    /// If true: This personality ALWAYS selects fakeout moves regardless of situation.
    /// Overrides fakeoutChanceMultiplier. Useful for personalities that rely on deception.
    /// </summary>
    public bool forceFakeouts = false;

    /// <summary>
    /// If true: This personality NEVER selects fakeout moves.
    /// Overrides fakeoutChanceMultiplier. Useful for straightforward aggressive types.
    /// </summary>
    public bool forbidFakeouts = false;

    /// <summary>
    /// If true: This personality prefers counter-type moves in move selection.
    /// Biases PersonalityMoveAdapter toward MoveTags.Counter when selecting.
    /// </summary>
    public bool prefersCounters = false;

    /// <summary>
    /// If true: This personality prefers heavy-damage moves (High Power tag).
    /// Biases PersonalityMoveAdapter toward MoveTags.HighPower when selecting.
    /// </summary>
    public bool prefersHeavy = false;

    /// <summary>
    /// If true: This personality prefers multi-hit moves (MultiHit tag).
    /// Biases PersonalityMoveAdapter toward MoveTags.MultiHit when selecting.
    /// </summary>
    public bool prefersMultiHit = false;
    public bool prefersFast  = false;
    public bool prefersExecute = false;
    public bool prefersArmorPierce = false;

    // ---------------------------------------------------------
    // BASELINE BIAS ADJUSTMENTS
    // ---------------------------------------------------------
    /// <summary>
    /// Section: Fine-tuning adjustments for move selection probabilities.
    /// Range: -1.0 to +1.0 (adds to base selection weight)
    /// 
    /// These adjustments influence PersonalityMoveAdapter.CalculateWeight() for each move tag.
    /// Positive = prefer that move type, Negative = avoid that move type.
    /// </summary>
    [Header("Baseline Bias Adjustments")]

    /// <summary>
    /// Adds to weight of Aggressive-tagged moves.
    /// Range: -1 (forbid aggressive) to +1 (strongly prefer aggressive)
    /// 
    /// Used by PersonalityMoveAdapter.CalculateWeight() to bias move selection toward aggressive play.
    /// </summary>
    [Range(-1f, 1f)] public float extraAggressiveBias = 0f;

    /// <summary>
    /// Adds to weight of ArmorPiercing-tagged moves.
    /// Range: -1 to +1
    /// 
    /// Useful for personalities that specialize in ignoring defense (e.g., CheapShot).
    /// </summary>
    [Range(-1f, 1f)] public float extraArmorPiercingBias = 0f;

    /// <summary>
    /// Adds to weight of Execute-tagged moves (high crit, instant kills).
    /// Range: -1 to +1
    /// 
    /// Useful for finisher personalities (e.g., Conqueror).
    /// </summary>
    [Range(-1f, 1f)] public float extraExecuteBias = 0f;

    /// <summary>
    /// Adds to weight of Fakeout-tagged moves.
    /// Range: -1 (forbid fakeout) to +1 (strongly prefer fakeout)
    /// 
    /// Can be overridden by forceFakeouts/forbidFakeouts flags.
    /// </summary>
    [Range(-1f, 1f)] public float extraFakeoutBias = 0f;

    // ---------------------------------------------------------
    // SAFE HELPERS (no LINQ, no allocations)
    // ---------------------------------------------------------

    // ---------------------------------------------------------
    // SAFE HELPERS (no LINQ, no allocations)
    // ---------------------------------------------------------
    /// <summary>
    /// Section: Convenience properties for behavior flag access.
    /// These avoid string lookups and provide type-safe behavior checking.
    /// Used by PersonalityMoveAdapter and combat systems for move selection.
    /// </summary>

    /// <summary>
    /// Returns true if this personality prefers multi-hit moves.
    /// Cached property access: returns prefersMultiHit field value.
    /// </summary>
    public bool PrefersMultiHit => prefersMultiHit;

    /// <summary>
    /// Returns true if this personality prefers heavy moves.
    /// Cached property access: returns prefersHeavy field value.
    /// </summary>
    public bool PrefersHeavy => prefersHeavy;

    /// <summary>
    /// Returns true if this personality prefers counter-type moves.
    /// Cached property access: returns prefersCounters field value.
    /// </summary>
    public bool PrefersCounters => prefersCounters;

    /// <summary>
    /// Returns true if this personality forbids fakeouts.
    /// Cached property access: returns forbidFakeouts field value.
    /// </summary>
    public bool ForbidsFakeouts => forbidFakeouts;

    /// <summary>
    /// Returns true if this personality forces fakeouts.
    /// Cached property access: returns forceFakeouts field value.
    /// </summary>
    public bool ForcesFakeouts => forceFakeouts;
}

// ===== SUPPORT CLASSES =====
/// <summary>
/// Serializable container for individual stat weight definition.
/// Associates a stat name with its weight multiplier for personality-based scaling.
/// 
/// Example: StatWeight { statName = "ATK", weight = 2.0f }
/// Meaning: This personality allocates double the bonus points to ATK compared to neutral (1.0)
/// 
/// Used by PersonalityProfile.statWeights list during EnemyPartySpawner.ApplyPersonalityRolls()
/// </summary>
[System.Serializable]
public class StatWeight
{
    /// <summary>
    /// Stat type identifier: "ATK", "DEF", "HP", or "SLOTS"
    /// Matched by EnemyPartySpawner.ApplyPersonalityRolls() for allocation.
    /// </summary>
    public string statName;

    /// <summary>
    /// Weight multiplier for bonus point allocation.
    /// 1.0 = neutral, 2.0 = double allocation, 0.5 = half allocation
    /// </summary>
    public float weight;
}

// ===== ENUM DEFINITIONS =====
/// <summary>
/// Eight personality types defining enemy combat archetypes.
/// Each type has unique stat weights, behavior flags, and move preferences defined in EnemyPersonalityProfile.
/// 
/// Used by:
/// - EnemySynergyMatrix for synergy calculations between enemies
/// - PersonalityMoveAdapter for personality-specific move selection
/// - EnemyPartySpawner for identifying which stat weights to apply
/// - Logs and debugging for personality identification
/// 
/// Personality Archetypes:
/// - BountyHunter: Skilled tracker, balanced stats, versatile moves
/// - Explorer: Adventurous, favors mobility, defensive counters
/// - MilitaryAdmiral: Strategic leader, high ATK, heavy moves preferred
/// - BlindFighter: Aggressive, high-risk high-reward, ignore defense
/// - NobleWarrior: Honorable, balanced approach, counter-heavy
/// - Conqueror: Dominant aggressor, execution moves, finish fights
/// - CheapShot: Underhanded, armor-piercing, fakeout spam
/// - ArrogantNoble: Self-confident, high DEF, defensive but dangerous
/// </summary>
public enum EnemyPersonalityType
{
    BountyHunter,      // Versatile tracker archetype
    Explorer,          // Defensive, mobility-focused archetype
    MilitaryAdmiral,   // Aggressive leader, high ATK
    BlindFighter,      // Risky offense, ignores defense
    NobleWarrior,      // Honorable balanced fighter
    Conqueror,         // Dominant finisher archetype
    CheapShot,         // Deceptive armor-piercer
    ArrogantNoble      // Confident defensive fighter
}
