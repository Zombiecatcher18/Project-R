using UnityEngine;
using System.Collections.Generic;

public class PersonalityBrain
{
    public static bool DebugPersonality = false;   // <— MASTER DEBUG TOGGLE

    private readonly EnemyRuntimeData runtime;

    public PersonalityBrain(EnemyRuntimeData runtime)
    {
        this.runtime = runtime;
    }

    // ============================================================
    // 1. TURN START LIFECYCLE
    // ============================================================
    public void OnTurnStart()
    {
        if (runtime == null)
        {
            if (DebugPersonality)
                Debug.LogError("[PersonalityBrain] Runtime is NULL — cannot apply turn logic.");
            return;
        }

        var p = runtime.personality;
        if (p == null)
        {
            if (DebugPersonality)
                Debug.LogWarning("[PersonalityBrain] Personality is NULL — skipping turn logic.");
            return;
        }

        runtime.turnCount++;

        if (DebugPersonality)
            Debug.Log($"[PersonalityBrain] === TURN {runtime.turnCount} START ({p.personalityType}) ===");

        // Reset temporary modifiers
        runtime.turn.aggressiveBias = 0f;
        runtime.turn.scalingBonus = 0f;
        runtime.turn.randomFactor = 0f;
        runtime.turn.forceFakeout = false;

        if (DebugPersonality)
            Debug.Log("[PersonalityBrain] Temporary modifiers reset.");

        ApplyTurnLogicInternal(p);

        if (DebugPersonality)
            Debug.Log($"[PersonalityBrain] After turn logic → AggroBias:{runtime.turn.aggressiveBias:F2}, ScalingBonus:{runtime.turn.scalingBonus:F2}, RandomFactor:{runtime.turn.randomFactor:F2}, ForceFakeout:{runtime.turn.forceFakeout}");
    }

    // ============================================================
    // 1A. INTERNAL TURN LOGIC
    // ============================================================
    private void ApplyTurnLogicInternal(EnemyPersonalityProfile p)
    {
        if (DebugPersonality)
            Debug.Log($"[PersonalityBrain] TurnLogic for {p.personalityType}");

        switch (p.personalityType)
        {
            case EnemyPersonalityType.Conqueror:
                if (runtime.turnCount >= 3)
                {
                    runtime.turn.scalingBonus += 0.1f;
                    if (DebugPersonality)
                        Debug.Log("[PersonalityBrain] Conqueror ramp → +ScalingBonus");
                }
                break;

            case EnemyPersonalityType.CheapShot:
                if (runtime.turnCount == 1)
                {
                    runtime.turn.forceFakeout = true;
                    if (DebugPersonality)
                        Debug.Log("[PersonalityBrain] CheapShot opener → ForceFakeoutThisTurn = TRUE");
                }
                break;

            case EnemyPersonalityType.MilitaryAdmiral:
                if (runtime.currentHP < runtime.info.maxHP * 0.4f)
                {
                    runtime.turn.aggressiveBias += 0.2f;
                    if (DebugPersonality)
                        Debug.Log("[PersonalityBrain] Admiral low HP → +AggressiveBias");
                }
                break;

            case EnemyPersonalityType.Explorer:
                runtime.turn.randomFactor = Random.Range(-0.2f, 0.2f);
                if (DebugPersonality)
                    Debug.Log($"[PersonalityBrain] Explorer randomness → RandomFactor = {runtime.turn.randomFactor:F2}");
                break;
        }
    }

    // ============================================================
    // 2. MOVE PREFERENCE SCORING
    // ============================================================
    public float GetMovePreferenceScore(EnemyMove move)
    {
        var p = runtime.personality;
        if (p == null)
            return 0f;

        float score = 0f;

        if (p.prefersHeavy && move.isHeavy)
        {
            score += 1f;
            if (DebugPersonality)
                Debug.Log($"[MovePref] {p.personalityType} prefers HEAVY → {move.moveName}");
        }

        if (p.prefersMultiHit && move.isMultiHit)
        {
            score += 1f;
            if (DebugPersonality)
                Debug.Log($"[MovePref] {p.personalityType} prefers MULTI-HIT → {move.moveName}");
        }

        if (p.prefersCounters && move.isCounter)
        {
            score += 1f;
            if (DebugPersonality)
                Debug.Log($"[MovePref] {p.personalityType} prefers COUNTERS → {move.moveName}");
        }

        return score;
    }

    // ============================================================
    // 3. MOVE PREFERENCE SCORING (SYNERGY-AWARE)
    // ============================================================
    public float GetMovePreferenceScoreSynergy(EnemyMove move)
    {
        float baseScore = GetMovePreferenceScore(move);
        float synergyScore = runtime.synergyScore;
        float bonus = 0f;

        var p = runtime.personality;

        if (synergyScore > 0.1f)
        {
            float factor = Mathf.Clamp(synergyScore, 0.1f, 2f);

            if ((p.personalityType == EnemyPersonalityType.Explorer ||
                 p.personalityType == EnemyPersonalityType.CheapShot) &&
                (move.hasFakeout || move.isMultiHit))
            {
                bonus += 0.5f * factor;
            }

            if ((p.personalityType == EnemyPersonalityType.MilitaryAdmiral ||
                 p.personalityType == EnemyPersonalityType.Conqueror) &&
                (move.ignoresDefense ||
                 move.HasTag(MoveTag.ArmorPiercing) ||
                 move.HasTag(MoveTag.AntiTank) ||
                 move.HasTag(MoveTag.Execute)))
            {
                bonus += 0.5f * factor;
            }

            if ((p.personalityType == EnemyPersonalityType.NobleWarrior ||
                 p.personalityType == EnemyPersonalityType.BlindFighter) &&
                (move.isHeavy || move.isCounter))
            {
                bonus += 0.4f * factor;
            }
        }

        if (synergyScore < -0.1f)
        {
            float factor = Mathf.Clamp(-synergyScore, 0.1f, 2f);

            if (move.HasTag(MoveTag.MultiHit) || move.HasTag(MoveTag.Execute))
                bonus -= 0.3f * factor;
        }

        float finalScore = baseScore + bonus;

        if (DebugPersonality)
            Debug.Log($"[MovePrefFinal] {p.personalityType} → {move.moveName} | Base:{baseScore:F2} Bonus:{bonus:F2} Final:{finalScore:F2}");

        return finalScore;
    }

    // ============================================================
    // 4. SYNERGY CALCULATION
    // ============================================================
    public void ApplySynergyForThisTurn(List<EnemyAttackController> party)
    {
        runtime.synergyScore = 0f;
        runtime.forceSabotageThisTurn = false;

        var p = runtime.personality;
        if (p == null)
            return;

        var myType = p.personalityType;

        for (int i = 0; i < party.Count; i++)
        {
            var ally = party[i];
            if (ally == null || ally.runtime == null)
                continue;

            float aff = EnemySynergyMatrix.GetAffinity(myType, ally.runtime.personality.personalityType);
            runtime.synergyScore += aff;

            if (aff <= -1.5f)
                runtime.forceSabotageThisTurn = true;
        }

        if (DebugPersonality)
            Debug.Log($"[PersonalityBrain] SynergyScore = {runtime.synergyScore:F2}, sabotage={runtime.forceSabotageThisTurn}");

        ApplySynergyBiases();
    }

    private void ApplySynergyBiases()
    {
        float score = runtime.synergyScore;

        if (score > 0.25f)
        {
            float factor = Mathf.Clamp(score, 0.25f, 2f);

            runtime.aggressiveBias += 0.1f * factor;
            runtime.executeBias += 0.1f * factor;
            runtime.armorPiercingBias += 0.05f * factor;
            runtime.info.fakeoutBias += 0.05f * factor;

            if (DebugPersonality)
                Debug.Log($"[PersonalityBrain] Positive synergy boost (factor={factor:F2})");
        }

        if (score < -0.25f)
        {
            float factor = Mathf.Clamp(-score, 0.25f, 2f);

            runtime.aggressiveBias *= 1f - 0.2f * factor;
            runtime.executeBias *= 1f - 0.2f * factor;
            runtime.armorPiercingBias *= 1f - 0.1f * factor;
            runtime.info.fakeoutBias *= 1f - 0.1f * factor;

            if (DebugPersonality)
                Debug.Log($"[PersonalityBrain] Negative synergy penalty (factor={factor:F2})");
        }
    }

    // ============================================================
    // 5. FAKEOUT DECISION LOGIC
    // ============================================================
    public bool ShouldFakeout(EnemyMove move, float lastFakeoutTime, int fakeoutsThisTurn, int maxFakeoutsPerTurn)
{
    Debug.Log($"[FAKEOUT CHECK] === ShouldFakeout START for {move?.moveName} ===");

    if (move == null)
    {
        Debug.Log("[FAKEOUT CHECK] move == NULL → returning FALSE");
        return false;
    }

    if (!move.hasFakeout)
    {
        Debug.Log($"[FAKEOUT CHECK] {move.moveName} hasFakeout == FALSE → returning FALSE");
        return false;
    }

    var p = runtime.personality;

    if (p != null)
    {
        Debug.Log($"[FAKEOUT CHECK] Personality = {p.personalityType}, forbid={p.forbidFakeouts}, force={p.forceFakeouts}");

        if (p.forbidFakeouts)
        {
            Debug.Log("[FAKEOUT CHECK] Personality forbids fakeouts → returning FALSE");
            return false;
        }

        if (p.forceFakeouts)
        {
            Debug.Log("[FAKEOUT CHECK] Personality forces fakeouts → returning TRUE");
            return true;
        }
    }
    else
    {
        Debug.Log("[FAKEOUT CHECK] No personality found.");
    }

    float timeSinceLast = Time.time - lastFakeoutTime;
    Debug.Log($"[FAKEOUT CHECK] Cooldown check: timeSinceLast={timeSinceLast}");

    if (timeSinceLast < 0.1f)
    {
        Debug.Log("[FAKEOUT CHECK] Cooldown BLOCKED fakeout → returning FALSE");
        return false;
    }

    Debug.Log($"[FAKEOUT CHECK] fakeoutsThisTurn={fakeoutsThisTurn}, maxFakeoutsPerTurn={maxFakeoutsPerTurn}");

    if (fakeoutsThisTurn >= maxFakeoutsPerTurn)
    {
        Debug.Log("[FAKEOUT CHECK] Per-turn limit BLOCKED fakeout → returning FALSE");
        return false;
    }

    float chance = move.fakeoutChance;
    Debug.Log($"[FAKEOUT CHECK] Base chance from move = {chance}");

    if (p != null)
    {
        Debug.Log($"[FAKEOUT CHECK] Personality multiplier = {p.fakeoutChanceMultiplier}");
        chance *= p.fakeoutChanceMultiplier;
    }

    float tagMult = move.GetTagModifiers().fakeoutChanceMult;
    Debug.Log($"[FAKEOUT CHECK] Tag multiplier = {tagMult}");
    chance *= tagMult;

    chance = Mathf.Clamp01(chance);
    Debug.Log($"[FAKEOUT CHECK] FINAL chance after multipliers = {chance}");

    float roll = Random.value;
    Debug.Log($"[FAKEOUT CHECK] Random roll = {roll}");

    bool result = roll <= chance;
    Debug.Log($"[FAKEOUT CHECK] ShouldFakeout RESULT = {result}");

    Debug.Log($"[FAKEOUT CHECK] === ShouldFakeout END for {move.moveName} ===");

    return result;
}


    // ============================================================
    // 6. COUNTER LOGIC
    // ============================================================
    public bool ShouldCounter(QTEResult result)
    {
        if (result != QTEResult.Perfect)
            return false;

        var p = runtime.personality;
        if (p == null)
            return false;

        float chance = p.baseCounterChance;
        float synergy = runtime.synergyScore;
        chance += runtime.counterBias;

        if (synergy > 0.5f)
            chance += 0.1f * Mathf.Clamp(synergy, 0.5f, 2f);

        if (synergy < -0.5f)
            chance -= 0.1f * Mathf.Clamp(-synergy, 0.5f, 2f);

        switch (p.personalityType)
        {
            case EnemyPersonalityType.NobleWarrior:
            case EnemyPersonalityType.BlindFighter:
                chance += 0.15f;
                break;

            case EnemyPersonalityType.CheapShot:
                chance -= 0.20f;
                break;

            case EnemyPersonalityType.Explorer:
                chance += Random.Range(-0.1f, 0.1f);
                break;
        }

        return Random.value <= Mathf.Clamp01(chance);
    }

    public int GetCounterDamage(EnemyMove move)
    {
        var p = runtime.personality;
        float dmg = move.counterAttackDamage;

        if (p != null)
            dmg *= p.counterDamageMultiplier;

        dmg *= 1f + (runtime.counteratk * 0.04f);

        if (runtime.synergyScore > 0.5f)
            dmg *= 1f + (runtime.synergyScore * 0.1f);

        if (runtime.synergyScore < -0.5f)
            dmg += 1f - (Mathf.Abs(runtime.synergyScore) * 0.1f);

        return Mathf.RoundToInt(dmg);
    }

    // ============================================================
    // 7. QTE SPEED / DELAY
    // ============================================================
    public float GetQTESpeed(EnemyMove move)
    {
        var p = runtime.personality;
        float baseSpeed = move.qteSpeed;
        float speed = baseSpeed;

        switch (p.personalityType)
        {
            case EnemyPersonalityType.BlindFighter:
                // Very fast, unpredictable
                speed = baseSpeed * Random.Range(1.2f, 1.6f);
                break;

            case EnemyPersonalityType.MilitaryAdmiral:
                // Slow, heavy timing
                speed = baseSpeed * 0.7f;
                break;

            case EnemyPersonalityType.CheapShot:
                // Medium speed, but fakeout-heavy
                speed = baseSpeed * Random.Range(0.9f, 1.1f);
                break;

            case EnemyPersonalityType.Explorer:
                // Fully random each turn
                speed = baseSpeed * Random.Range(0.7f, 1.4f);
                break;

            case EnemyPersonalityType.Conqueror:
                // Harder each turn
                float ramp = 1f + (runtime.turnCount * 0.05f);
                speed = baseSpeed * ramp;
                break;

            case EnemyPersonalityType.BountyHunter: 
                speed = move.hasFakeout ? baseSpeed * 1.3f : baseSpeed; 
                break;

            default:
                speed = baseSpeed;
                break;
        }

        // APPLY PATTERN AWARENESS BIAS LAST 
        speed *= 1f + runtime.qteSpeedBias; 
        
        return speed;
    }

    public float GetQTEPreDelay(EnemyMove move)
    {
        var p = runtime.personality;
        float baseDelay = move.GetEnemyButtonPreDelay();
        float delay = baseDelay;

        // PERSONALITY LOGIC FIRST
        switch (p.personalityType)
        {
            case EnemyPersonalityType.BlindFighter:
                delay = baseDelay * 0.6f;
                break;

            case EnemyPersonalityType.MilitaryAdmiral:
                delay = baseDelay * 1.4f;
                break;

            case EnemyPersonalityType.CheapShot:
                delay = baseDelay * Random.Range(0.5f, 1.5f);
                break;

            case EnemyPersonalityType.Explorer:
                delay = baseDelay * Random.Range(0.4f, 1.8f);
                break;

            case EnemyPersonalityType.Conqueror:
                float ramp = 1f - (runtime.turnCount * 0.03f);
                delay = Mathf.Max(0.1f, baseDelay * ramp);
                break;

            case EnemyPersonalityType.BountyHunter:
                delay = baseDelay;
                break;

            default:
                delay = baseDelay;
                break;
        }

        // APPLY PATTERN AWARENESS BIAS LAST
        delay *= 1f + runtime.preDelayBias;

        return delay;
    }



    // ============================================================
    // 8. QTE DAMAGE
    // ============================================================
    public int ApplyQTEResult(EnemyMove move, QTEResult result)
    {
        float dmg = move.damageOnMiss;
        dmg *= 1f + (runtime.info.attack * 0.04f);

        if (move.HasTag(MoveTag.Execute))
        {
            float hpPct = (float)PlayerStats.Instance.currentHealth / PlayerStats.Instance.MaxHealth;
            if (hpPct <= 0.3f)
                dmg *= 1.5f;
        }

        if (move.HasTag(MoveTag.AntiTank) && PlayerStats.Instance.defense >= 10)
            dmg *= 1.3f;

        var p = runtime.personality;
        if (p != null)
        {
            switch (result)
            {
                case QTEResult.Good:
                    dmg *= p.qteGoodDamageMultiplier;
                    break;

                case QTEResult.Ok:
                    dmg *= p.qteOkDamageMultiplier;
                    break;

                case QTEResult.Miss:
                    dmg *= p.qteMissDamageMultiplier;
                    break;
            }
        }

        return Mathf.RoundToInt(dmg);
    }

    // ============================================================
    // SYNERGY CHAIN LOGIC (restored for compatibility)
    // ============================================================
    public void MarkSynergyChain(EnemyMove move, BattleManager.SynergyChain chain)
    {
        bool defTarget =
            move.ignoresDefense ||
            move.HasTag(MoveTag.ArmorPiercing) ||
            move.HasTag(MoveTag.AntiTank);

        bool hpOrAtkTarget =
            move.HasTag(MoveTag.Execute) ||
            move.HasTag(MoveTag.MultiHit) ||
            move.baseDamage >= 18 ||
            move.damageOnMiss >= 18;

        if (defTarget)
            chain.defTargetedLastTurn = true;

        if (hpOrAtkTarget)
            chain.hpOrAtkTargetedLastTurn = true;
    }

    // ============================================================
    // SYNERGY CHAIN BIASES (restored for compatibility)
    // ============================================================
    public void ApplySynergyChainBiases(BattleManager.SynergyChain chain)
    {
        if (!chain.defTargetedLastTurn && !chain.hpOrAtkTargetedLastTurn)
            return;

        float synergyFactor = Mathf.Clamp(1f + runtime.synergyScore * 0.5f, 0.5f, 2f);

        if (chain.defTargetedLastTurn)
        {
            runtime.executeBias += 0.25f * synergyFactor;
            runtime.aggressiveBias += 0.1f * synergyFactor;
        }

        if (chain.hpOrAtkTargetedLastTurn)
        {
            runtime.armorPiercingBias += 0.2f * synergyFactor;
        }
    }

    // ============================================================
    // PATTERN AWARENESS → BEHAVIOR MODIFIERS
    // ============================================================
    public void ApplyPatternAwareness()
    {
        var p = runtime.pattern;
        var turn = runtime.turn;

        // Awareness stage (0–99)
        float threshold = runtime.personality.awarenessThreshold;

        if (p.awarenessMeter < threshold)
        {
            float t = p.awarenessMeter / threshold; // 0–1

            // Fakeout chance rises
            runtime.info.fakeoutBias += 0.1f * t;

            // Aggression rises
            turn.aggressiveBias += 0.15f * t;

            // QTE becomes slightly faster
            runtime.qteSpeedBias += 0.1f * t;

            // Pre-delay shrinks
            runtime.preDelayBias -= 0.1f * t;

            // Synergy with allies increases
            runtime.synergyScore += 0.1f * t;

            ApplyPersonalityPatternReactions(t);
            return;
        }

        // ============================================================
        // PREDICTION MODE (100+)
        // ============================================================
        float predictionStrength = Mathf.Clamp((p.predictionMeter - 100f) / 100f, 0f, 2f);

        // Fakeout becomes dangerous
        runtime.info.fakeoutBias += 0.25f + 0.25f * predictionStrength;

        // Counter chance rises sharply
        runtime.counterBias += 0.2f + 0.3f * predictionStrength;

        // QTE becomes very fast
        runtime.qteSpeedBias += 0.3f + 0.3f * predictionStrength;

        // Pre-delay becomes tiny
        runtime.preDelayBias -= 0.3f + 0.3f * predictionStrength;

        // Aggression spikes
        turn.aggressiveBias += 0.3f + 0.3f * predictionStrength;

        ApplyPersonalityPatternReactions(predictionStrength);
    }

    private void ApplyPersonalityPatternReactions(float intensity)
    {
        var type = runtime.personality.personalityType;

        switch (type)
        {
            case EnemyPersonalityType.BlindFighter:
                // Learns QTE timing → punishes predictable timing
                runtime.counterBias += 0.2f * intensity;
                runtime.qteSpeedBias += 0.2f * intensity;
                break;

            case EnemyPersonalityType.BountyHunter:
                // Baits predictable openers
                runtime.executeBias += 0.25f * intensity;
                break;

            case EnemyPersonalityType.Conqueror:
                // Reduces player turn meter when patterns appear
                runtime.turnMeterDrain += 5f * intensity;
                break;

            case EnemyPersonalityType.CheapShot:
                // Exploits repeated moves
                runtime.info.fakeoutBias += 0.3f * intensity;
                break;

            case EnemyPersonalityType.Explorer:
                // Normally random, but becomes focused when patterns appear
                runtime.turn.randomFactor += Random.Range(0.1f, 0.3f) * intensity;
                break;
        }
    }
}
