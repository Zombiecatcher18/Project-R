using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Static orchestrator for personality-driven turn initialization and state management.
/// 
/// Purpose: Manages turn lifecycle for personality-based combat, ensuring all temporary
/// state modifications are reset between turns and personality behaviors are applied fresh.
/// 
/// Architecture:
/// - Single entry point: OnTurnStart() called by BattleManager at combat turn initialization
/// - Resets all temporary modifiers (aggression, scaling, randomness)
/// - Delegates personality-specific behavior to PersonalityBehavior.ApplyTurnLogic()
/// - Provides comprehensive debug logging for behavior visibility
/// 
/// Turn State Flow:
/// 1. OnTurnStart() called by BattleManager.ExecuteEnemyTurn()
/// 2. Increment turnCount (1, 2, 3, ...)
/// 3. Reset all temp modifiers to 0/false:
///    - tempAggressiveBias = 0f
///    - tempScalingBonus = 0f
///    - tempRandomFactor = 0f
///    - forceFakeoutThisTurn = false
/// 4. Call PersonalityBehavior.ApplyTurnLogic() for personality-specific behavior
/// 5. Log final state for debugging
/// 
/// Personality Turn Effects Applied:
/// - CONQUEROR: +0.1 scaling bonus (accumulates each turn after turn 3)
/// - CHEAPSHOT: +1 aggressive bias on turn 1 (turn 1 opener fakeout)
/// - MILITARYADMIRAL: +0.2 aggressive bias when HP < 40%
/// - EXPLORER: ±0.2 random factor each turn
/// 
/// Cross-references:
/// - Called by BattleManager.cs ExecuteEnemyTurn()
/// - Calls PersonalityBehavior.cs ApplyTurnLogic()
/// - Modifies EnemyRuntimeData.cs temporary modifiers
/// - References EnemyPersonalityProfile.cs for personality type
/// 
/// Integration Points:
/// - Turn-start called from BattleManager enemy turn execution
/// - Temp modifiers used by PersonalityMoveAdapter for move weighting
/// - Scaling bonus applied to move damage calculations
/// - Random factor applied to all weighted selections
/// </summary>
public static class PersonalityTurnLogic
{
    /// <summary>
    /// Called at the start of every enemy turn to initialize personality state.
    /// 
    /// Process:
    /// 1. Guard: Validate runtime is not null (error) and personality is not null (warning)
    /// 2. Increment turnCount for turn tracking (1, 2, 3, ...)
    /// 3. Log turn start with personality type
    /// 4. Reset all temporary modifiers (flags, bonuses, random factors):
    ///    - tempAggressiveBias = 0f (per-turn aggression adjustment)
    ///    - tempScalingBonus = 0f (per-turn scaling multiplier bonus)
    ///    - tempRandomFactor = 0f (per-turn unpredictability factor)
    ///    - forceFakeoutThisTurn = false (fakeout override flag)
    /// 5. Call PersonalityBehavior.ApplyTurnLogic(runtime) for personality-specific behaviors:
    ///    - Conqueror: Scaling ramp after turn 3
    ///    - CheapShot: Guaranteed fakeout on turn 1
    ///    - MilitaryAdmiral: Aggression boost when low HP
    ///    - Explorer: Random variation per turn
    /// 6. Log final state with all modifiers for debugging
    /// 
    /// Parameters:
    /// - runtime: EnemyRuntimeData containing personality, turn count, and temp modifiers
    /// 
    /// Called by: BattleManager.cs ExecuteEnemyTurn() at combat turn initialization
    /// 
    /// Cross-references:
    /// - Sets EnemyRuntimeData fields: turnCount, tempAggressiveBias, tempScalingBonus, tempRandomFactor, forceFakeoutThisTurn
    /// - Calls PersonalityBehavior.ApplyTurnLogic() for personality-specific logic
    /// - These temp modifiers are used by PersonalityMoveAdapter for move weighting
    /// </summary>
    public static void OnTurnStart(EnemyRuntimeData runtime)
    {
        if (runtime == null)
        {
            Debug.LogError("[TurnLogic] Runtime is NULL — cannot apply turn logic.");
            return;
        }

        if (runtime.personality == null)
        {
            Debug.LogWarning("[TurnLogic] Personality is NULL — skipping personality turn logic.");
            return;
        }

        var p = runtime.personality;

        // ---------------------------------------------------------
        // TURN COUNT
        // ---------------------------------------------------------
        /// <summary>
        /// Increment turn counter for personality-specific turn conditions.
        /// Used by personality logic: Conqueror turn >= 3, CheapShot turn == 1, etc.
        /// </summary>
        runtime.turnCount++;
        Debug.Log($"[TurnLogic] === TURN {runtime.turnCount} START ({p.personalityType}) ===");

        // ---------------------------------------------------------
        // RESET TEMPORARY VALUES
        // ---------------------------------------------------------
        /// <summary>
        /// Reset per-turn modifiers to neutral (0 or false).
        /// These are recalculated every turn by personality logic.
        /// 
        /// tempAggressiveBias: Added to aggressive move weight selection
        /// tempScalingBonus: Multiplier for scaling-type move damage
        /// tempRandomFactor: Random variance applied to all move weights
        /// forceFakeoutThisTurn: Hard override to force fakeout move selection
        /// </summary>
        runtime.turn.aggressiveBias = 0f;
        runtime.turn.scalingBonus = 0f;
        runtime.turn.randomFactor = 0f;
        runtime.turn.forceFakeout = false;

        Debug.Log("[TurnLogic] Temporary modifiers reset.");

        // ============================================================
        // PATTERN AWARENESS TURN MODIFIERS
        // ============================================================
        var pattern = runtime.pattern;

        // Awareness Phase (0–99)
        if (pattern.awarenessMeter < 100f)
        {
            float t = pattern.awarenessMeter / 100f; // 0–1

            // More aggression as enemy learns your patterns
            runtime.turn.aggressiveBias += 0.2f * t;

            // More likely to fakeout
            runtime.turn.forceFakeout |= (Random.value < 0.05f * t);

            // Slightly faster scaling (damage)
            runtime.turn.scalingBonus += 0.1f * t;

            // Slight unpredictability
            runtime.turn.randomFactor += Random.Range(0f, 0.15f * t);

            return;
        }

        // ============================================================
        // PREDICTION MODE (100+)
        // ============================================================
        float predictionStrength = Mathf.Clamp((pattern.predictionMeter - 100f) / 100f, 0f, 2f);

        // Very aggressive
        runtime.turn.aggressiveBias += 0.4f + 0.4f * predictionStrength;

        // High chance to force fakeout
        runtime.turn.forceFakeout |= (Random.value < 0.25f + 0.25f * predictionStrength);

        // Strong scaling bonus
        runtime.turn.scalingBonus += 0.3f + 0.3f * predictionStrength;

        // Unpredictable but dangerous
        runtime.turn.randomFactor += Random.Range(0.1f, 0.4f * predictionStrength);

        // ---------------------------------------------------------
        // APPLY PERSONALITY TURN LOGIC
        // ---------------------------------------------------------
        /// <summary>
        /// Delegate to PersonalityBehavior for personality-specific turn behaviors.
        /// This applies ramps, openers, and adaptations specific to personality type.
        /// </summary>
        PersonalityBehavior.ApplyTurnLogic(runtime);

        Debug.Log(
            $"[TurnLogic] After personality logic → " +
            $"AggroBias:{runtime.turn.aggressiveBias:F2}, " +
            $"ScalingBonus:{runtime.turn.scalingBonus:F2}, " +
            $"RandomFactor:{runtime.turn.randomFactor:F2}, " +
            $"ForceFakeout:{runtime.turn.forceFakeout}"
        );
    }
}
