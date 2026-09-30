using System.Collections.Generic;

/// <summary>
/// Static affinity matrix defining personality-based compatibility between enemy types.
/// Determines how well different personalities work together in team synergy and combo selection.
/// 
/// Affinity Scale (7-point scale):
/// - Strong Ally (+1.0f):  Natural teammates, boost each other's effectiveness
/// - Ally (+0.5f):         Compatible personalities, mild boost to coordination
/// - Neutral (0f):         No inherent bonus/penalty, independent fighters
/// - Rival (-0.5f):        Different philosophies, reduced team coordination
/// - Nemesis (-1.0f):      Actively opposed goals, significant penalty to synergy
/// - Anti-synergy (-2.0f): Directly counter each other (rare special case)
/// 
/// Usage:
/// Called by EnemyPartySynergyCoordinator to evaluate move combinations.
/// Higher affinity encourages targeted combo chains, support moves, simultaneous attacks.
/// Lower affinity encourages independent action, defensive positioning.
/// 
/// Personality Types Included (8 total):
/// BountyHunter, Explorer, MilitaryAdmiral, BlindFighter, NobleWarrior, Conqueror, CheapShot, ArrogantNoble
/// 
/// Special Cases:
/// - Explorer ↔ ArrogantNoble = -2.0f (anti-synergy: freedom-seeker vs tyranny)
/// - Same personality match = +0.5f (mild self-synergy bonus)
/// - Asymmetric pairs use default neutral 0f (one direction not explicitly defined)
/// </summary>
public static class EnemySynergyMatrix
{
    // ===== AFFINITY SCORES =====
    /// <summary>
    /// Dictionary mapping personality type pairs to synergy affinity scores.
    /// Key: Tuple of (SourcePersonality, TargetPersonality)
    /// Value: Float affinity score (-2.0 to +1.0 range)
    /// 
    /// Structure: Organized by source personality with all target affinities listed.
    /// Coverage: 42 directed pairs (not fully symmetrical; some pair-directions omitted for balance).
    /// 
    /// Example entries:
    /// - (BountyHunter, Explorer) = +1.0f (Strong allies, complementary tactics)
    /// - (Explorer, ArrogantNoble) = -2.0f (Ideological conflict, anti-synergy)
    /// - (MilitaryAdmiral, Conqueror) = +1.0f (Shared command structure, teamwork natural)
    /// </summary>
    private static readonly Dictionary<(EnemyPersonalityType, EnemyPersonalityType), float> affinity =
        new Dictionary<(EnemyPersonalityType, EnemyPersonalityType), float>
    {
        // ===== BOUNTY HUNTER SYNERGIES =====
        // Philosophy: Professional, results-focused, honorable within mercenary code
        {(EnemyPersonalityType.BountyHunter, EnemyPersonalityType.Explorer),         +1.0f}, // Allied explorers = shared adventure goals
        {(EnemyPersonalityType.BountyHunter, EnemyPersonalityType.MilitaryAdmiral), +1.0f}, // Chain of command compatible
        {(EnemyPersonalityType.BountyHunter, EnemyPersonalityType.NobleWarrior),    +0.5f}, // Honor-bound compatibility
        {(EnemyPersonalityType.BountyHunter, EnemyPersonalityType.CheapShot),       -0.5f}, // Dishonorable tactics clash
        {(EnemyPersonalityType.BountyHunter, EnemyPersonalityType.ArrogantNoble),   -1.0f}, // Serve no master but themselves

        // ===== EXPLORER SYNERGIES =====
        // Philosophy: Freedom-seeking, discovery-driven, opportunistic
        {(EnemyPersonalityType.Explorer, EnemyPersonalityType.CheapShot),           +1.0f}, // Risk-takers align
        {(EnemyPersonalityType.Explorer, EnemyPersonalityType.BountyHunter),        +0.5f}, // Shared adventure potential
        {(EnemyPersonalityType.Explorer, EnemyPersonalityType.BlindFighter),        +0.5f}, // Unconventional perspectives compatible
        {(EnemyPersonalityType.Explorer, EnemyPersonalityType.MilitaryAdmiral),     -0.5f}, // Structure opposes freedom
        {(EnemyPersonalityType.Explorer, EnemyPersonalityType.Conqueror),           -1.0f}, // Conquest means control, antithetical to exploration
        {(EnemyPersonalityType.Explorer, EnemyPersonalityType.ArrogantNoble),       -2.0f}, // ANTI-SYNERGY: Freedom vs tyranny (core ideological conflict)

        // ===== MILITARY ADMIRAL SYNERGIES =====
        // Philosophy: Hierarchy, strategy, organized force projection
        {(EnemyPersonalityType.MilitaryAdmiral, EnemyPersonalityType.NobleWarrior), +1.0f}, // Chain of command, shared values
        {(EnemyPersonalityType.MilitaryAdmiral, EnemyPersonalityType.Conqueror),    +1.0f}, // Territorial expansion strategy
        {(EnemyPersonalityType.MilitaryAdmiral, EnemyPersonalityType.BountyHunter), +0.5f}, // Mercenary command integration
        {(EnemyPersonalityType.MilitaryAdmiral, EnemyPersonalityType.Explorer),     -0.5f}, // Explorers resist structure
        {(EnemyPersonalityType.MilitaryAdmiral, EnemyPersonalityType.CheapShot),    -1.0f}, // Dishonorable tactics beneath military code

        // ===== BLIND FIGHTER SYNERGIES =====
        // Philosophy: Intuition-driven, unconventional, independent spirit
        {(EnemyPersonalityType.BlindFighter, EnemyPersonalityType.Explorer),        +1.0f}, // Unconventional approaches align
        {(EnemyPersonalityType.BlindFighter, EnemyPersonalityType.NobleWarrior),    +0.5f}, // Warrior code respected
        {(EnemyPersonalityType.BlindFighter, EnemyPersonalityType.MilitaryAdmiral), -0.5f}, // Intuition vs rigid structure
        {(EnemyPersonalityType.BlindFighter, EnemyPersonalityType.ArrogantNoble),   -1.0f}, // Noble arrogance vs humble intuition

        // ===== NOBLE WARRIOR SYNERGIES =====
        // Philosophy: Honor, duty, strength in service
        {(EnemyPersonalityType.NobleWarrior, EnemyPersonalityType.MilitaryAdmiral), +1.0f}, // Leadership hierarchy natural fit
        {(EnemyPersonalityType.NobleWarrior, EnemyPersonalityType.BountyHunter),    +0.5f}, // Honorable mercenary compatible
        {(EnemyPersonalityType.NobleWarrior, EnemyPersonalityType.BlindFighter),    +0.5f}, // Warrior respect transcends methods
        {(EnemyPersonalityType.NobleWarrior, EnemyPersonalityType.Conqueror),       -0.5f}, // Conquest conflicts with service
        {(EnemyPersonalityType.NobleWarrior, EnemyPersonalityType.CheapShot),       -1.0f}, // Honorable warrior despises dishonorable tactics

        // ===== CONQUEROR SYNERGIES =====
        // Philosophy: Dominance, expansion, power concentration
        {(EnemyPersonalityType.Conqueror, EnemyPersonalityType.MilitaryAdmiral),    +1.0f}, // Military expansion partnership
        {(EnemyPersonalityType.Conqueror, EnemyPersonalityType.ArrogantNoble),      +0.5f}, // Power-seeking alignment
        {(EnemyPersonalityType.Conqueror, EnemyPersonalityType.NobleWarrior),       -0.5f}, // Warriors serve, Conqueror dominates (tension)
        {(EnemyPersonalityType.Conqueror, EnemyPersonalityType.Explorer),           -1.0f}, // Conquest incompatible with exploration freedom

        // ===== CHEAP SHOT SYNERGIES =====
        // Philosophy: Win by any means, no honor code, pragmatic amorality
        {(EnemyPersonalityType.CheapShot, EnemyPersonalityType.Explorer),           +1.0f}, // Anything-goes adventuring
        {(EnemyPersonalityType.CheapShot, EnemyPersonalityType.Conqueror),          +0.5f}, // Ruthless methodology alignment
        {(EnemyPersonalityType.CheapShot, EnemyPersonalityType.BountyHunter),       -0.5f}, // Mercenaries have honor code
        {(EnemyPersonalityType.CheapShot, EnemyPersonalityType.NobleWarrior),       -1.0f}, // Warriors despise dishonorable tactics
        {(EnemyPersonalityType.CheapShot, EnemyPersonalityType.MilitaryAdmiral),    -1.0f}, // Military code rejects cowardice

        // ===== ARROGANT NOBLE SYNERGIES =====
        // Philosophy: Superiority, entitlement, dominance through birthright
        {(EnemyPersonalityType.ArrogantNoble, EnemyPersonalityType.Conqueror),      +1.0f}, // Shared domination goals
        {(EnemyPersonalityType.ArrogantNoble, EnemyPersonalityType.MilitaryAdmiral),+0.5f}, // Command structures suit arrogance
        {(EnemyPersonalityType.ArrogantNoble, EnemyPersonalityType.BountyHunter),   -0.5f}, // Mercenaries serve no one
        {(EnemyPersonalityType.ArrogantNoble, EnemyPersonalityType.BlindFighter),   -1.0f}, // Arrogance despises humble intuition
        {(EnemyPersonalityType.ArrogantNoble, EnemyPersonalityType.Explorer),       -2.0f}, // ANTI-SYNERGY: Tyranny vs freedom (core ideological conflict)
    };

    // ===== AFFINITY LOOKUP =====
    /// <summary>
    /// Retrieves synergy affinity between two personality types.
    /// 
    /// Logic:
    /// 1. If personalities are identical: return +0.5f (mild self-synergy bonus)
    /// 2. If pair exists in affinity matrix: return mapped value (-2.0 to +1.0)
    /// 3. If pair not found: return 0f (neutral default for unmapped pairs)
    /// 
    /// Called by: EnemyPartySynergyCoordinator for combo team evaluation.
    /// Impact: Higher affinity increases coordination likelihood in multi-enemy battles.
    /// 
    /// Parameters:
    ///   - a: Source personality type (EnemyPersonalityType enum)
    ///   - b: Target personality type (EnemyPersonalityType enum)
    /// Returns: Float affinity score (-2.0 to +1.0 range)
    /// </summary>
    public static float GetAffinity(EnemyPersonalityType a, EnemyPersonalityType b)
    {
        // Same personality type gets mild bonus (mild familiarity/understanding)
        if (a == b)
            return 0.5f;

        // Try to find directed pair in matrix
        if (affinity.TryGetValue((a, b), out float value))
            return value;

        // Default neutral for unmapped pair-directions
        return 0f;
    }
}
