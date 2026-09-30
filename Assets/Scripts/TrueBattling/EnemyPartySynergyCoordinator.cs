using UnityEngine;

/// <summary>
/// Coordinates multi-enemy party tactics by assigning synergy roles and move bonuses.
/// Assigns each enemy in a party a strategic role (Opener/Breaker/Finisher) based on turn order.
/// Provides role-specific damage bonuses to encourage tactical team composition.
/// 
/// Party Role Strategy:
/// - Opener (Index 0): Sets up conditions with fakeouts or armor-piercing attacks (1.5x bonus)
/// - Breaker (Middle indices): Breaks defenses with multi-hit or anti-tank moves (1.5x bonus)
/// - Finisher (Last index): Delivers powerful finishing strikes with heavy/execute moves (1.5x bonus)
/// 
/// Design: Static utility class (GC-free, no allocations). Used by AIComboBuilder and EnemyAttackController.
/// Performance: Avoids LINQ and List allocations; pure integer/enum comparisons.
/// </summary>
public static class EnemyPartySynergyCoordinator
{
    // ===== SYNERGY ROLE TYPES =====
    /// <summary>
    /// Enum defining party combat roles for multi-enemy encounters.
    /// 
    /// - Opener: First enemy to act; sets conditions with preparation/disruption moves
    /// - Breaker: Middle enemies in turn order; breaks enemy defenses systematically
    /// - Finisher: Last enemy to act; delivers high-impact finishing moves
    /// 
    /// Role system encourages turn-order tactical thinking rather than independent action.
    /// </summary>
    public enum SynergyRole { Opener, Breaker, Finisher }

    // ===== ROLE ASSIGNMENT =====
    /// <summary>
    /// Assigns synergy role to enemy based on party position (index) and total party size.
    /// 
    /// Logic:
    /// - Solo enemy (total <= 1): Always Finisher (no party context)
    /// - First enemy (index == 0): Opener (sets conditions)
    /// - Last enemy (index == total - 1): Finisher (delivers finish)
    /// - Middle enemies: Breaker (executes systematic plan)
    /// 
    /// Called by: AIComboBuilder during party initialization, EnemyPartySpawner at encounter start.
    /// Parameters:
    ///   - index: 0-based position in enemy party (0 = first to act)
    ///   - total: Total enemies in this party encounter
    /// Returns: SynergyRole enum (Opener, Breaker, or Finisher)
    /// </summary>
    public static SynergyRole AssignRole(int index, int total)
    {
        // Solo encounter: single enemy is its own finisher
        if (total <= 1)
            return SynergyRole.Finisher;

        // First enemy in turn order opens the party strategy
        if (index == 0)
            return SynergyRole.Opener;

        // Last enemy delivers the finishing blow
        if (index == total - 1)
            return SynergyRole.Finisher;

        // Middle enemies break enemy defenses
        return SynergyRole.Breaker;
    }

    // ===== ROLE-BASED DAMAGE BONUS =====
    /// <summary>
    /// Calculates synergy bonus multiplier for a move based on assigned party role.
    /// 
    /// Bonus System:
    /// - Opener (+1.5x): Moves with fakeout or ArmorPiercing tag get bonus
    ///   Purpose: Disrupt enemy defense to enable teammates
    /// - Breaker (+1.5x): Multi-hit or AntiTank moves get bonus
    ///   Purpose: Systematic defense breaking through volume or tank-busting
    /// - Finisher (+1.5x): Heavy damage or Execute tag moves get bonus
    ///   Purpose: Capitalize on weakened opponent with devastating finish
    /// - No bonus (0f): Default for moves not aligned with role
    /// 
    /// Called by: AIComboBuilder during move selection for damage estimation.
    /// Effect: Bonus multiplier applied to final damage calculation in BattleManager.
    /// GC-free: No allocations, pure enum/bool checks on MoveTag system.
    /// 
    /// Parameters:
    ///   - move: EnemyMove to evaluate (can be null, returns 0f)
    ///   - role: Assigned SynergyRole from AssignRole()
    /// Returns: Float bonus multiplier (0f or 1.5f currently)
    /// </summary>
    public static float GetRoleBonus(EnemyMove move, SynergyRole role)
    {
        // Safety check: null moves have no bonus
        if (move == null)
            return 0f;

        switch (role)
        {
            case SynergyRole.Opener:
                // Openers bonus fakeout moves (deception) or armor-piercing (penetration)
                // Fakeout disrupts player, Armor-Piercing bypasses defense setup
                if (move.hasFakeout || move.HasTag(MoveTag.ArmorPiercing))
                    return 1.5f;
                break;

            case SynergyRole.Breaker:
                // Breakers bonus multi-hit (chipping defense) or anti-tank (penetration)
                // Multi-hit: removes shields/barriers through repetition
                // Anti-Tank: directly counters high-defense opponents
                if (move.isMultiHit || move.HasTag(MoveTag.AntiTank))
                    return 1.5f;
                break;

            case SynergyRole.Finisher:
                // Finishers bonus heavy moves (raw power) or execute (instant effect)
                // Heavy: maximum damage on weakened target
                // Execute: special finisher mechanics
                if (move.isHeavy || move.HasTag(MoveTag.Execute))
                    return 1.5f;
                break;
        }

        // No bonus for moves not aligned with role
        return 0f;
    }
}
