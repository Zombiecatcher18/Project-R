using UnityEngine;

/// <summary>
/// Centralized move weighting logic for enemy AI.
/// 
/// Goal:
/// - One predictable scoring formula
/// - Combines:
///     * personality preferences
///     * synergy score
///     * runtime biases
///     * random factor
///     * combo pattern
/// - Never fully ignores a move (always has a minimum weight)
/// 
/// Used by:
/// - EnemyRuntimeData.DrawCardWeighted()
/// - AIComboBuilder (Build*FromHand, PickBestMoveByScore)
/// - PersonalityComboPatterns (Explorer / internal patterns)
/// </summary>
public static class PersonalityBehavior
{
    /// <summary>
    /// Main entry used by AI systems that have full runtime context.
    /// </summary>
    public static float GetMovePreferenceScore(EnemyMove move, EnemyRuntimeData runtime)
    {
        if (move == null || runtime == null)
            return 0.1f;

        return CoreScore(move, runtime.personality, runtime);
    }

    /// <summary>
    /// Overload used where only the personality profile is known
    /// (e.g. some combo pattern logic).
    /// </summary>
    public static float GetMovePreferenceScore(EnemyMove move, EnemyPersonalityProfile profile)
    {
        if (move == null)
            return 0.1f;

        return CoreScore(move, profile, null);
    }

    /// <summary>
    /// Single normalized scoring formula.
    /// </summary>
    private static float CoreScore(EnemyMove move, EnemyPersonalityProfile profile, EnemyRuntimeData runtime)
    {
        // -----------------------------
        // 0. Safety / defaults
        // -----------------------------
        float score = 0f;
        float synergy = runtime != null ? runtime.synergyScore : 0f;
        float randRuntime = runtime != null ? runtime.turn.randomFactor : 0f;
        float baseRandom = Random.Range(-0.05f, 0.05f); // tiny shake so ties break

        // -----------------------------
        // 1. BASE TAG / SHAPE SCORE
        //    (what kind of move is this?)
        // -----------------------------
        // Start from a neutral baseline so every move has *some* identity.
        float shape = 0.25f;

        if (move.isHeavy)      shape += 0.6f;
        if (move.isMultiHit)   shape += 0.5f;
        if (move.isCounter)    shape += 0.4f;
        if (move.hasFakeout)   shape += 0.4f;
        if (move.HasTag(MoveTag.ArmorPiercing) || move.ignoresDefense) shape += 0.5f;
        if (move.HasTag(MoveTag.AntiTank))      shape += 0.4f;
        if (move.HasTag(MoveTag.Execute))       shape += 0.6f;

        // Slight reward for “interesting” scaling
        shape += Mathf.Clamp(move.scalingValue * 0.02f, 0f, 0.6f);

        score += shape;

        // -----------------------------
        // 2. PERSONALITY PREFERENCES
        //    (what does this personality like?)
        // -----------------------------
        if (profile != null)
        {
            // Hard preferences
            if (profile.prefersHeavy     && move.isHeavy)      score += 1.0f;
            if (profile.prefersMultiHit  && move.isMultiHit)   score += 1.0f;
            if (profile.prefersCounters  && move.isCounter)    score += 1.0f;
            if (profile.prefersFast      && move.HasTag(MoveTag.Fast))        score += 0.8f;
            if (profile.prefersExecute   && move.HasTag(MoveTag.Execute))     score += 0.9f;
            if (profile.prefersArmorPierce &&
                (move.HasTag(MoveTag.ArmorPiercing) || move.ignoresDefense))   score += 0.9f;

            // Baseline bias knobs
            score += profile.extraAggressiveBias     * AggressiveAffinity(move);
            score += profile.extraArmorPiercingBias  * ArmorPierceAffinity(move);
            score += profile.extraExecuteBias        * ExecuteAffinity(move);
            score += profile.extraFakeoutBias        * FakeoutAffinity(move);

            // Combo pattern flavor
            score += PatternAffinity(profile.comboPattern, move);
        }

        // -----------------------------
        // 3. SYNERGY INFLUENCE
        //    (how well is this team working together?)
        // -----------------------------
        if (runtime != null)
        {
            // Positive synergy: reward “on-theme” moves
            if (synergy > 0.1f)
            {
                float factor = Mathf.Clamp(synergy, 0.1f, 3f);

                // “Team combo” style: multi-hit, execute, armor-pierce
                if (move.isMultiHit || move.HasTag(MoveTag.Execute))
                    score += 0.3f * factor;

                if (move.HasTag(MoveTag.ArmorPiercing) || move.HasTag(MoveTag.AntiTank))
                    score += 0.25f * factor;

                if (move.hasFakeout)
                    score += 0.15f * factor;
            }

            // Negative synergy: reduce coordination-heavy stuff
            if (synergy < -0.1f)
            {
                float factor = Mathf.Clamp(-synergy, 0.1f, 3f);

                if (move.isMultiHit || move.HasTag(MoveTag.Execute))
                    score -= 0.25f * factor;

                if (move.hasFakeout)
                    score -= 0.2f * factor;
            }
        }

        // -----------------------------
        // 4. RUNTIME BIASES
        //    (what has this enemy been leaning toward?)
        // -----------------------------
        if (runtime != null)
        {
            // Aggressive bias → heavy / multihit
            if (move.isHeavy || move.isMultiHit)
                score += runtime.aggressiveBias * 1.0f;
            else
                score += runtime.aggressiveBias * 0.25f;

            // Armor-piercing bias → armor / anti-tank / ignore DEF
            if (move.HasTag(MoveTag.ArmorPiercing) || move.HasTag(MoveTag.AntiTank) || move.ignoresDefense)
                score += runtime.armorPiercingBias * 1.0f;
            else
                score += runtime.armorPiercingBias * 0.2f;

            // Execute bias → execute / high damage on miss
            if (move.HasTag(MoveTag.Execute) || move.damageOnMiss >= 18)
                score += runtime.executeBias * 1.0f;
            else
                score += runtime.executeBias * 0.2f;

            // Fakeout bias from EnemyInfo
            if (move.hasFakeout)
                score += runtime.info.fakeoutBias * 0.8f;
        }

        // -----------------------------
        // 5. RANDOM / DIVERSITY FACTOR
        //    (keeps patterns recognizable but not rigid)
        // -----------------------------
        float diversity = profile != null ? profile.diversityBias : 0.2f;

        // Runtime random factor (Explorer etc.) + small base random
        float randomInfluence = (randRuntime * 0.5f) + (baseRandom * diversity);

        score += randomInfluence;

        // -----------------------------
        // 6. FINAL CLAMP / FLOOR
        //    (never fully ignore a move)
        // -----------------------------
        if (score < 0.1f)
            score = 0.1f;

        return score;
    }

    // ============================
    // SMALL HELPER AFFINITIES
    // ============================

    private static float AggressiveAffinity(EnemyMove move)
    {
        float v = 0f;
        if (move.isHeavy)    v += 1f;
        if (move.isMultiHit) v += 0.8f;
        if (move.critChance >= 0.1f) v += 0.4f;
        return v;
    }

    private static float ArmorPierceAffinity(EnemyMove move)
    {
        float v = 0f;
        if (move.ignoresDefense) v += 1.0f;
        if (move.HasTag(MoveTag.ArmorPiercing)) v += 0.8f;
        if (move.HasTag(MoveTag.AntiTank)) v += 0.6f;
        return v;
    }

    private static float ExecuteAffinity(EnemyMove move)
    {
        float v = 0f;
        if (move.HasTag(MoveTag.Execute)) v += 1.0f;
        if (move.damageOnMiss >= 18) v += 0.5f;
        if (move.critChance >= 0.15f) v += 0.5f;
        return v;
    }

    private static float FakeoutAffinity(EnemyMove move)
    {
        float v = 0f;
        if (move.hasFakeout) v += 1.0f;
        if (move.HasTag(MoveTag.FakeoutHeavy)) v += 0.6f;
        return v;
    }

    private static float PatternAffinity(ComboPattern pattern, EnemyMove move)
    {
        switch (pattern)
        {
            case ComboPattern.FakeoutOpener:
                return move.hasFakeout ? 0.6f : 0.0f;

            case ComboPattern.HeavyFinisher:
                return move.isHeavy ? 0.6f : 0.0f;

            case ComboPattern.ScalingRamp:
                return Mathf.Clamp(move.scalingValue * 0.03f, 0f, 0.6f);

            case ComboPattern.Randomized:
                // Slight nudge toward more “interesting” moves
                return (move.isMultiHit || move.hasFakeout) ? 0.3f : 0.0f;

            case ComboPattern.Balanced:
            default:
                return 0f;
        }
    }

    // ============================================================
    // TURN-START PERSONALITY LOGIC (ported from PersonalityBrain)
    // ============================================================
    public static void ApplyTurnLogic(EnemyRuntimeData runtime)
    {
        if (runtime == null || runtime.personality == null)
            return;

        var p = runtime.personality;
        float hpPct = (float)runtime.currentHP / runtime.info.maxHP;

        // ============================================================
        // 1. ENRAGE (Low HP)
        // ============================================================
        if (hpPct <= 0.30f)
        {
            switch (p.personalityType)
            {
                case EnemyPersonalityType.Conqueror:
                    runtime.turn.aggressiveBias += 0.4f;
                    runtime.turn.scalingBonus += 0.2f;
                    break;

                case EnemyPersonalityType.MilitaryAdmiral:
                    runtime.turn.aggressiveBias += 0.3f;
                    break;

                case EnemyPersonalityType.BlindFighter:
                    runtime.turn.randomFactor += Random.Range(0.1f, 0.3f);
                    break;

                case EnemyPersonalityType.NobleWarrior:
                    runtime.turn.aggressiveBias += 0.2f;
                    break;

                case EnemyPersonalityType.CheapShot:
                    runtime.turn.forceFakeout = true;
                    break;
            }
        }

        // ============================================================
        // 2. PANIC (Low Synergy)
        // ============================================================
        if (runtime.synergyScore <= -1.0f)
        {
            switch (p.personalityType)
            {
                case EnemyPersonalityType.Explorer:
                    runtime.turn.randomFactor += Random.Range(0.2f, 0.4f);
                    break;

                case EnemyPersonalityType.ArrogantNoble:
                    runtime.turn.aggressiveBias -= 0.2f;
                    break;

                case EnemyPersonalityType.BountyHunter:
                    runtime.turn.scalingBonus -= 0.1f;
                    break;

                case EnemyPersonalityType.CheapShot:
                    runtime.turn.forceFakeout = false;
                    break;
            }
        }

        // ============================================================
        // 3. RALLY (Ally Death — Relationship-Based)
        // ============================================================
        if (runtime.memory.lastAllyDied)
        {
            string deadID = runtime.memory.lastAllyDiedID;
            float affinity = runtime.memory.GetAffinity(deadID);

            // High affinity → strong emotional reaction
            if (affinity >= 3f)
            {
                switch (p.personalityType)
                {
                    case EnemyPersonalityType.Conqueror:
                        runtime.turn.aggressiveBias += 0.6f;
                        runtime.turn.scalingBonus += 0.3f;
                        break;

                    case EnemyPersonalityType.NobleWarrior:
                        runtime.turn.aggressiveBias += 0.4f;
                        runtime.turn.scalingBonus += 0.3f;
                        break;

                    case EnemyPersonalityType.MilitaryAdmiral:
                        runtime.turn.aggressiveBias += 0.5f;
                        runtime.turn.randomFactor -= 0.2f;
                        break;

                    case EnemyPersonalityType.BlindFighter:
                        runtime.turn.randomFactor += Random.Range(0.2f, 0.4f);
                        break;
                }
            }
            // Neutral affinity → mild reaction
            else if (affinity > -1f)
            {
                switch (p.personalityType)
                {
                    case EnemyPersonalityType.Conqueror:
                        runtime.turn.aggressiveBias += 0.2f;
                        break;

                    case EnemyPersonalityType.NobleWarrior:
                        runtime.turn.scalingBonus += 0.1f;
                        break;

                    case EnemyPersonalityType.MilitaryAdmiral:
                        runtime.turn.aggressiveBias += 0.1f;
                        break;
                }
            }
            // Negative affinity → rival died (enemy might be relieved)
            else
            {
            switch (p.personalityType)
                {
                    case EnemyPersonalityType.ArrogantNoble:
                        runtime.turn.aggressiveBias -= 0.2f;
                        break;

                    case EnemyPersonalityType.BountyHunter:
                        runtime.turn.randomFactor -= 0.1f;
                        break;

                    case EnemyPersonalityType.Explorer:
                        runtime.turn.randomFactor += Random.Range(-0.1f, 0.1f);
                        break;
                }
            }
        }

        // ============================================================
        // 4. TAUNT (High Synergy)
        // ============================================================
        if (runtime.synergyScore >= 1.0f)
        {
            switch (p.personalityType)
            {
                case EnemyPersonalityType.Explorer:
                    runtime.turn.randomFactor += Random.Range(0.1f, 0.2f);
                    break;

                case EnemyPersonalityType.MilitaryAdmiral:
                    runtime.turn.aggressiveBias += 0.2f;
                    break;

                case EnemyPersonalityType.Conqueror:
                    runtime.turn.scalingBonus += 0.2f;
                    break;

                case EnemyPersonalityType.NobleWarrior:
                    runtime.turn.aggressiveBias += 0.1f;
                    break;
            }
        }

        // ============================================================
        // 5. RETREAT / DEFENSIVE MODE
        // ============================================================
        if (hpPct <= 0.20f && runtime.synergyScore < 0)
        {
            switch (p.personalityType)
            {
                case EnemyPersonalityType.ArrogantNoble:
                    runtime.turn.aggressiveBias -= 0.3f;
                    break;

                case EnemyPersonalityType.BountyHunter:
                    runtime.turn.scalingBonus -= 0.2f;
                    break;

                case EnemyPersonalityType.Explorer:
                    runtime.turn.randomFactor += Random.Range(0.2f, 0.4f);
                    break;
            }
        }   

        // ============================================================
        // 6. ORIGINAL PERSONALITY TURN LOGIC
        // ============================================================
        switch (p.personalityType)
        {
            case EnemyPersonalityType.Conqueror:
                if (runtime.turnCount >= 3)
                    runtime.turn.scalingBonus += 0.1f;
                break;

            case EnemyPersonalityType.CheapShot:
                if (runtime.turnCount == 1)
                    runtime.turn.forceFakeout = true;
                break;

            case EnemyPersonalityType.MilitaryAdmiral:
                if (runtime.currentHP < runtime.info.maxHP * 0.4f)
                    runtime.turn.aggressiveBias += 0.2f;
                break;

            case EnemyPersonalityType.Explorer:
                runtime.turn.randomFactor += Random.Range(-0.2f, 0.2f);
                break;
        }   
    }
}
