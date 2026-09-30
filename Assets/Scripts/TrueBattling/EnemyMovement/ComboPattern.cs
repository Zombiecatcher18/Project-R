using UnityEngine;

/// <summary>
/// Enum defining personality-driven combo sequencing strategies.
/// 
/// Purpose: Allows different personality types to prioritize moves in meaningful combat ways,
/// creating distinct tactical behaviors without changing the actual moves available.
/// 
/// Each pattern type is applied by PersonalityComboPatterns.ApplyPattern() during combo execution,
/// which reorders the move list based on selected strategy.
/// 
/// Integration:
/// - Defined in EnemyPersonalityProfile.comboPattern field
/// - Applied by PersonalityComboPatterns.ApplyPattern() before combo execution
/// - Reorders moves to achieve personality-specific tactical goals
/// - Can be overridden by sabotage flag (forceSabotageThisTurn)
/// 
/// Cross-references:
/// - Set in EnemyPersonalityProfile.cs
/// - Applied by PersonalityComboPatterns.cs
/// </summary>
public enum ComboPattern
{
    /// <summary>
    /// No special ordering applied; moves execute in current sequence.
    /// Default strategy: balanced, even distribution of move types.
    /// 
    /// Used by: Balanced personality types (general approach)
    /// Personality Example: BountyHunter (versatile, no preference)
    /// </summary>
    Balanced,

    /// <summary>
    /// Moves fakeout-type moves to front of combo sequence.
    /// Strategy: Lead with deceptive moves, catch opponent off-guard early.
    /// 
    /// Implementation: Sorts moves so hasFakeout=true moves appear first.
    /// Effect: Fakeout moves execute before heavy/defensive moves.
    /// 
    /// Used by: Deceptive personalities
    /// Personality Example: Explorer, CheapShot (tricksters)
    /// </summary>
    FakeoutOpener,

    /// <summary>
    /// Moves heavy-damage moves to end of combo sequence.
    /// Strategy: Build momentum, finish with strongest attack.
    /// 
    /// Implementation: Sorts moves so isHeavy=true moves appear last.
    /// Effect: Heavy moves execute as finisher (last in combo).
    /// 
    /// Used by: Aggressive finisher personalities
    /// Personality Example: Conqueror, NobleWarrior (finishers)
    /// </summary>
    HeavyFinisher,

    /// <summary>
    /// Randomly shuffles entire combo each turn.
    /// Strategy: Unpredictability, opponent cannot anticipate move order.
    /// 
    /// Implementation: Durstenfeld shuffle algorithm (O(n) in-place).
    /// Effect: Move order is random every execution.
    /// 
    /// Used by: Unpredictable personalities
    /// Personality Example: Explorer (chaos, randomness)
    /// </summary>
    Randomized,

    /// <summary>
    /// Orders moves by scaling value, highest-damage-scaling first.
    /// Strategy: Build damage gradually, momentum ramp to crescendo.
    /// 
    /// Implementation: Sorts moves by move.scalingValue descending.
    /// Effect: High-scaling moves execute early to build advantage.
    /// 
    /// Used by: Scaling/ramping personalities
    /// Personality Example: Conqueror (scaling advantage grows over time)
    /// </summary>
    ScalingRamp
}

