using UnityEngine;

/// <summary>
/// Static utility class for applying personality-based stat multipliers to moves.
/// 
/// Purpose: Decouples move definition from personality-specific scaling.
/// Allows same move template to be used by different personalities with different effectiveness.
/// 
/// Architecture:
/// - Uses static ScriptableObject instance for runtime move wrapping (zero allocation)
/// - Applies multipliers from EnemyPersonalityProfile to move stats
/// - Returns modified move instance without altering original asset
/// 
/// Personality Multipliers Applied:
/// - damageMultiplier: Scales baseDamage (0.5x to 1.5x)
/// - qteSpeedMultiplier: Scales QTE timing window (0.5x to 1.5x)
/// - fakeoutChanceMultiplier: Scales fakeout occurrence (0x to 2x)
/// - critChanceMultiplier: Scales critical hit chance (0.5x to 1.5x)
/// 
/// Design Notes:
/// - All stat copies are value-based to preserve original asset
/// - Tags array is shared (read-only, safe reference copy)
/// - Flags (hasFakeout, ignoresDefense, etc.) copied directly (immutable configuration)
/// - Single static instance reused per GetMoveForPersonality call (zero allocation strategy)
/// 
/// Cross-references:
/// - Called by PersonalityTurnLogic.cs for move selection
/// - References EnemyPersonalityProfile.cs for multiplier values
/// - Returns modified EnemyMove.cs instance
/// 
/// Called by: PersonalityTurnLogic and move selection systems during combat
/// </summary>
public static class PersonalityMoveAdapter
{
    /// <summary>
    /// Static runtime move instance for wrapping personality-modified stats.
    /// Reused across calls to minimize allocations.
    /// Contains calculated values, not original asset.
    /// </summary>
    private static readonly EnemyMove _runtimeMove = ScriptableObject.CreateInstance<EnemyMove>();

    /// <summary>
    /// Creates a personality-modified copy of a move with stat multipliers applied.
    /// 
    /// Process:
    /// 1. Guard: Return null if original is null, return original if personality is null
    /// 2. Copy metadata: moveName, slotCost (immutable fields)
    /// 3. Apply personality multipliers to derived stats:
    ///    - baseDamage = original × personality.damageMultiplier (rounded to int)
    ///    - qteSpeed = original × personality.qteSpeedMultiplier
    ///    - fakeoutChance = original × personality.fakeoutChanceMultiplier
    ///    - critChance = original × personality.critChanceMultiplier
    /// 4. Copy behavior flags: hasFakeout, fakeoutRequiresQTE, ignoresDefense, damageOnMiss, etc.
    /// 5. Copy combat properties: trickiness, qteType, requiresQTE, critMultiplier
    /// 6. Share tags reference (read-only, safe reference copy)
    /// 7. Return modified _runtimeMove instance
    /// 8. Log adapted stats for debugging
    /// 
    /// Parameters:
    /// - original: EnemyMove asset with base stats
    /// - personality: EnemyPersonalityProfile with multiplier configuration
    /// 
    /// Returns: EnemyMove instance with personality-adjusted stats
    ///         - null if original is null
    ///         - original if personality is null (no modification)
    ///         - _runtimeMove with multipliers applied otherwise
    /// 
    /// Important: Returned instance is shared across calls, do not store references.
    /// Use immediately for move selection/execution, never cache.
    /// 
    /// Example:
    /// EnemyMove adapted = GetMoveForPersonality(heavyAttack, conquerorProfile);
    /// // If heavyAttack.baseDamage = 100 and conquerorProfile.damageMultiplier = 1.3
    /// // Then adapted.baseDamage = 130
    /// </summary>
    public static EnemyMove GetMoveForPersonality(EnemyMove original, EnemyPersonalityProfile personality)
    {
        if (original == null) return null;
        if (personality == null) return original;
        
        // 1. Get the Tag Modifiers
        var tagMods = original.GetTagModifiers();

        // 2. Apply Personality AND Tag Multipliers
        // Combine base damage with personality multiplier AND tag multiplier
        _runtimeMove.baseDamage = Mathf.RoundToInt(original.baseDamage * personality.damageMultiplier * tagMods.damageMult);

        // Combine QTE speed
        _runtimeMove.qteSpeed = original.qteSpeed * personality.qteSpeedMultiplier * tagMods.qteSpeedMult;

        // Combine Fakeout chance (Personalities like "CheapShot" + FakeoutHeavy tag = very high chance)
        _runtimeMove.fakeoutChance = original.fakeoutChance * personality.fakeoutChanceMultiplier * tagMods.fakeoutChanceMult;

        // 3. Pass through the flags for the Controller to handle
        _runtimeMove.ignoresDefense = tagMods.ignoreDefense;
        // Note: I will need to add new bools to EnemyMove to store isExecute/isAntiTank if you want to cache them,
        // OR simply call GetTagModifiers() inside the AttackController (recommended for dynamic logic).

        // Copy ONLY the fields that matter for runtime behavior
        _runtimeMove.moveName = original.moveName;
        _runtimeMove.slotCost = original.slotCost;

        // Apply multipliers
        _runtimeMove.baseDamage = Mathf.RoundToInt(original.baseDamage * personality.damageMultiplier);
        _runtimeMove.qteSpeed = original.qteSpeed * personality.qteSpeedMultiplier;
        _runtimeMove.fakeoutChance = original.fakeoutChance * personality.fakeoutChanceMultiplier;
        _runtimeMove.critChance = original.critChance * personality.critChanceMultiplier;

        // Copy flags and tags directly (no allocation)
        _runtimeMove.hasFakeout = original.hasFakeout;
        _runtimeMove.fakeoutRequiresQTE = original.fakeoutRequiresQTE;
        _runtimeMove.ignoresDefense = original.ignoresDefense;
        _runtimeMove.damageOnMiss = original.damageOnMiss;
        _runtimeMove.counterAttackDamage = original.counterAttackDamage;
        _runtimeMove.trickiness = original.trickiness;
        _runtimeMove.qteType = original.qteType;
        _runtimeMove.requiresQTE = original.requiresQTE;
        _runtimeMove.critMultiplier = original.critMultiplier;

        // Copy tags array reference (safe because tags are read‑only)
        _runtimeMove.tags = original.tags;

        Debug.Log(
            $"[MoveAdapter] {personality.personalityType} adapted move {original.moveName} → " +
            $"DMG:{_runtimeMove.baseDamage}, QTE:{_runtimeMove.qteSpeed:F2}, Fakeout:{_runtimeMove.fakeoutChance:F2}, Crit:{_runtimeMove.critChance:F2}"
        );

        return _runtimeMove;
    }
}
