// PHASE 2: AI HAND SYSTEM — FULL REWRITE OF AIComboBuilder
// ---------------------------------------------------------
// Enemies now:
// - Draw a persistent hand each turn
// - Keep unused cards between turns
// - Draw duplicates (like the player)
// - Build combos ONLY from their hand
// - Remove used cards after playing
// - Refill hand to (deckSize + comboSlots)
// - Still use all your bias, synergy, and personality logic
// ---------------------------------------------------------

using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class AIComboBuilder
{
    private static readonly List<EnemyMove> _fakeoutMoves = new List<EnemyMove>(16);
    private static readonly List<EnemyMove> _nonFakeoutMoves = new List<EnemyMove>(16);
    private static readonly List<EnemyMove> _poolBuffer = new List<EnemyMove>(32);
    private static readonly List<EnemyMove> _tmpList = new List<EnemyMove>(32);

    private static readonly StringBuilder _sb = new StringBuilder(256);

    public static bool DebugAI = false;

    public static List<EnemyMove> BuildCombo(EnemyRuntimeData runtime, PlayerStats player)
    {
        var combo = new List<EnemyMove>();

        int slots = runtime.maxComboSlots > 0 ? runtime.maxComboSlots : runtime.info.maxComboSlots;
        int usedSlots = 0;

        // ---------------------------------------------------------
        // REFILL HAND FIRST (persistent hand system)
        // ---------------------------------------------------------
        runtime.RefillHand();
        var hand = runtime.currentHand;

        // ---------------------------------------------------------
        // DEBUG: SHOW HAND AT START OF TURN
        // ---------------------------------------------------------
        {
            if (DebugAI)
            {
                StringBuilder handLog = new StringBuilder();
                handLog.Append("[AI HAND] Start of Turn → ");

                for (int i = 0; i < hand.Count; i++)
                {
                    handLog.Append(hand[i].moveName);
                    if (i < hand.Count - 1)
                        handLog.Append(", ");
                }

                Debug.Log(handLog.ToString());
            }
        }

        if (hand == null || hand.Count == 0)
        {
            if (DebugAI)
                Debug.LogError("[AI] ERROR: Enemy hand is empty after refill!");
            return combo;
        }

        // ---------------------------------------------------------
        // PARTY ROLE
        // ---------------------------------------------------------
        int index = runtime.partyIndex;
        int total = runtime.partySize;

        var role = EnemyPartySynergyCoordinator.AssignRole(index, total);

        if (runtime.synergyScore < -1.0f)
        {
            if (DebugAI)
                Debug.Log("[AI] Negative synergy → ignoring party role.");
            role = EnemyPartySynergyCoordinator.SynergyRole.Finisher;
        }

        // ---------------------------------------------------------
        // PLAYER SNAPSHOT
        // ---------------------------------------------------------
        if (DebugAI)
            Debug.Log(
                $"[AI] Player Stats → HP:{player.currentHealth}/{player.MaxHealth}, " +
                $"ATK:{player.attack}, DEF:{player.defense}, " +
                $"Upgrades → HP:{player.hpUpgrades}, ATK:{player.atkUpgrades}, DEF:{player.defUpgrades}"
        );

        // ---------------------------------------------------------
        // DECAY BIASES
        // ---------------------------------------------------------
        runtime.armorPiercingBias *= 0.95f;
        runtime.aggressiveBias *= 0.95f;
        runtime.executeBias *= 0.95f;

        // ---------------------------------------------------------
        // PERSONALITY BIASES
        // ---------------------------------------------------------
        ApplyPersonalityBiases(runtime);

        // ---------------------------------------------------------
        // ADAPTIVE BIASES
        // ---------------------------------------------------------
        ApplyAdaptiveBiases(runtime, player);

        // ---------------------------------------------------------
        // STRATEGY SELECTION
        // ---------------------------------------------------------
        float baseWeight = 1f;
        float totalWeight =
            baseWeight +
            runtime.armorPiercingBias +
            runtime.aggressiveBias +
            runtime.executeBias;

        float roll = Random.value * totalWeight;

        // ---------------------------------------------------------
        // STRATEGY: ARMOR PIERCING
        // ---------------------------------------------------------
        if (roll < runtime.armorPiercingBias)
        {
            var result = BuildArmorPiercingComboFromHand(runtime, hand, slots);
            PersonalityComboPatterns.ApplyPattern(result, runtime.personality, runtime);

            // DEBUG: USED MOVES
            PrintUsedMoves(result);

            runtime.RemoveUsedCards(result);

            // DEBUG: REMAINING HAND
            PrintRemainingHand(runtime);

            return LogAndReturnCombo(result);
        }
        roll -= runtime.armorPiercingBias;

        // ---------------------------------------------------------
        // STRATEGY: AGGRESSIVE
        // ---------------------------------------------------------
        if (roll < runtime.aggressiveBias)
        {
            var result = BuildAggressiveComboFromHand(runtime, hand, slots);
            PersonalityComboPatterns.ApplyPattern(result, runtime.personality, runtime);

            PrintUsedMoves(result);
            runtime.RemoveUsedCards(result);
            PrintRemainingHand(runtime);

            return LogAndReturnCombo(result);
        }
        roll -= runtime.aggressiveBias;

        // ---------------------------------------------------------
        // STRATEGY: EXECUTE
        // ---------------------------------------------------------
        if (roll < runtime.executeBias)
        {
            var result = BuildExecuteComboFromHand(runtime, hand, slots);
            PersonalityComboPatterns.ApplyPattern(result, runtime.personality, runtime);

            PrintUsedMoves(result);
            runtime.RemoveUsedCards(result);
            PrintRemainingHand(runtime);

            return LogAndReturnCombo(result);
        }

        // ---------------------------------------------------------
        // FALLBACK: RANDOM COMBO FROM HAND
        // ---------------------------------------------------------
        var fallback = BuildRandomComboFromHand(runtime, hand, slots, role);

        PrintUsedMoves(fallback);
        runtime.RemoveUsedCards(fallback);
        PrintRemainingHand(runtime);

        return LogAndReturnCombo(fallback);
    }

    // ---------------------------------------------------------
    // DEBUG HELPERS
    // ---------------------------------------------------------
    private static void PrintUsedMoves(List<EnemyMove> combo)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("[AI HAND] Used Moves → ");

        for (int i = 0; i < combo.Count; i++)
        {
            sb.Append(combo[i].moveName);
            if (i < combo.Count - 1)
                sb.Append(", ");
        }

        if (DebugAI)
            Debug.Log(sb.ToString());
    }

    private static void PrintRemainingHand(EnemyRuntimeData runtime)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("[AI HAND] Remaining After Removal → ");

        for (int i = 0; i < runtime.currentHand.Count; i++)
        {
            sb.Append(runtime.currentHand[i].moveName);
            if (i < runtime.currentHand.Count - 1)
                sb.Append(", ");
        }

        if (DebugAI)
            Debug.Log(sb.ToString());
    }

    // ---------------------------------------------------------
    // RANDOM COMBO FROM HAND (fallback)
    // ---------------------------------------------------------
    private static List<EnemyMove> BuildRandomComboFromHand(
        EnemyRuntimeData runtime,
        List<EnemyMove> hand,
        int slots,
        EnemyPartySynergyCoordinator.SynergyRole role)
    {
        var combo = new List<EnemyMove>();
        int usedSlots = 0;

        _fakeoutMoves.Clear();
        _nonFakeoutMoves.Clear();

        // Split hand into fakeout / non-fakeout
        for (int i = 0; i < hand.Count; i++)
        {
            var m = hand[i];
            if (m == null) continue;

            if (m.hasFakeout)
                _fakeoutMoves.Add(m);
            else
                _nonFakeoutMoves.Add(m);
        }

        bool wantsFakeout = Random.value < runtime.info.fakeoutBias;

        if (wantsFakeout && _fakeoutMoves.Count == 0)
            wantsFakeout = false;

        if (!wantsFakeout && _nonFakeoutMoves.Count == 0)
            wantsFakeout = true;

        _poolBuffer.Clear();
        if (wantsFakeout)
            _poolBuffer.AddRange(_fakeoutMoves);
        else
            _poolBuffer.AddRange(_nonFakeoutMoves);

        // Diversity injection
        if (Random.value < runtime.personality.diversityBias)
        {
            _tmpList.Clear();

            for (int i = 0; i < hand.Count; i++)
            {
                var m = hand[i];
                if (!_poolBuffer.Contains(m))
                    _tmpList.Add(m);
            }

            if (_tmpList.Count > 0)
            {
                var injected = _tmpList[Random.Range(0, _tmpList.Count)];
                _poolBuffer.Add(injected);
            }
        }

        // Build combo
        while (usedSlots < slots && _poolBuffer.Count > 0)
        {
            EnemyMove chosen;

            if (runtime.forceSabotageThisTurn)
                chosen = PickSabotageMove(_poolBuffer);
            else
                chosen = PickBestMoveByScore(_poolBuffer, runtime, role);

            if (chosen == null)
                break;

            if (usedSlots + chosen.slotCost <= slots)
            {
                combo.Add(chosen);
                usedSlots += chosen.slotCost;
                runtime.RegisterMoveUse(chosen.moveName);
            }

            _poolBuffer.Remove(chosen);
        }

        PersonalityComboPatterns.ApplyPattern(combo, runtime.personality, runtime);

        return combo;
    }

    // ---------------------------------------------------------
    // SMART COMBO BUILDERS (FROM HAND)
    // ---------------------------------------------------------
    private static List<EnemyMove> BuildArmorPiercingComboFromHand(
        EnemyRuntimeData runtime,
        List<EnemyMove> hand,
        int slots)
    {
        var result = new List<EnemyMove>();
        _tmpList.Clear();

        for (int i = 0; i < hand.Count; i++)
        {
            var m = hand[i];
            if (m == null) continue;

            if (m.ignoresDefense ||
                m.baseDamage >= 18 ||
                m.HasTag(MoveTag.ArmorPiercing) ||
                m.HasTag(MoveTag.AntiTank))
            {
                _tmpList.Add(m);
            }
        }

        _tmpList.Sort((a, b) => b.baseDamage.CompareTo(a.baseDamage));

        int used = 0;
        for (int i = 0; i < _tmpList.Count; i++)
        {
            var m = _tmpList[i];
            if (used + m.slotCost <= slots)
            {
                used += m.slotCost;
                result.Add(m);
            }
        }

        return result;
    }

    private static List<EnemyMove> BuildAggressiveComboFromHand(
        EnemyRuntimeData runtime,
        List<EnemyMove> hand,
        int slots)
    {
        var result = new List<EnemyMove>();
        _tmpList.Clear();

        for (int i = 0; i < hand.Count; i++)
        {
            var m = hand[i];
            if (m == null) continue;

            if (m.qteSpeed > 1.2f ||
                m.hasFakeout ||
                m.HasTag(MoveTag.Fast) ||
                m.HasTag(MoveTag.MultiHit) ||
                m.HasTag(MoveTag.FakeoutHeavy))
            {
                _tmpList.Add(m);
            }
        }

        _tmpList.Sort((a, b) =>
        {
            float sa = a.qteSpeed + a.trickiness;
            float sb = b.qteSpeed + b.trickiness;
            return sb.CompareTo(sa);
        });

        int used = 0;
        for (int i = 0; i < _tmpList.Count; i++)
        {
            var m = _tmpList[i];
            if (used + m.slotCost <= slots)
            {
                used += m.slotCost;
                result.Add(m);
            }
        }

        return result;
    }

    private static List<EnemyMove> BuildExecuteComboFromHand(
        EnemyRuntimeData runtime,
        List<EnemyMove> hand,
        int slots)
    {
        var result = new List<EnemyMove>();
        _tmpList.Clear();

        for (int i = 0; i < hand.Count; i++)
        {
            var m = hand[i];
            if (m == null) continue;

            if (m.damageOnMiss >= 18 ||
                m.critChance >= 0.15f ||
                m.HasTag(MoveTag.Execute))
            {
                _tmpList.Add(m);
            }
        }

        _tmpList.Sort((a, b) =>
        {
            float sa = a.damageOnMiss + a.critChance * 100f;
            float sb = b.damageOnMiss + b.critChance * 100f;
            return sb.CompareTo(sa);
        });

        int used = 0;
        for (int i = 0; i < _tmpList.Count; i++)
        {
            var m = _tmpList[i];
            if (used + m.slotCost <= slots)
            {
                used += m.slotCost;
                result.Add(m);
            }
        }

        return result;
    }

    // ---------------------------------------------------------
    // ADAPTIVE BIASES
    // ---------------------------------------------------------
    private static void ApplyAdaptiveBiases(EnemyRuntimeData runtime, PlayerStats player)
    {
        if (player.level < 2)
            return;

        if (player.defense <= 3)
        {
            runtime.armorPiercingBias += 0.25f;
            runtime.aggressiveBias += 0.1f;
        }

        if (player.currentHealth < player.MaxHealth * 0.35f)
        {
            runtime.executeBias += 0.25f;
            runtime.aggressiveBias += 0.1f;
        }

        if (player.attack <= 5)
            runtime.aggressiveBias += 0.2f;

        if (player.currentHealth > player.MaxHealth * 0.75f)
            runtime.armorPiercingBias += 0.15f;

        if (player.attack > player.defense)
            runtime.aggressiveBias += 0.15f;

        if (player.defense >= 8)
            runtime.armorPiercingBias += 0.3f;

        if (player.defUpgrades >= 3)
            runtime.armorPiercingBias += 0.25f;

        if (player.atkUpgrades >= 3)
            runtime.aggressiveBias += 0.25f;

        if (player.hpUpgrades >= 3)
        {
            runtime.executeBias += 0.2f;
            runtime.armorPiercingBias += 0.1f;
        }
    }

    // ---------------------------------------------------------
    // PERSONALITY BIAS APPLICATION
    // ---------------------------------------------------------
    private static void ApplyPersonalityBiases(EnemyRuntimeData runtime)
    {
        var p = runtime.personality;
        if (p == null) return;

        runtime.aggressiveBias += p.extraAggressiveBias;
        runtime.armorPiercingBias += p.extraArmorPiercingBias;
        runtime.executeBias += p.extraExecuteBias;
        runtime.info.fakeoutBias += p.extraFakeoutBias;

        runtime.aggressiveBias = Mathf.Clamp01(runtime.aggressiveBias);
        runtime.armorPiercingBias = Mathf.Clamp01(runtime.armorPiercingBias);
        runtime.executeBias = Mathf.Clamp01(runtime.executeBias);
        runtime.info.fakeoutBias = Mathf.Clamp01(runtime.info.fakeoutBias);
    }

    // ---------------------------------------------------------
    // SABOTAGE PICKER
    // ---------------------------------------------------------
    private static EnemyMove PickSabotageMove(List<EnemyMove> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        float roll = Random.value;

        if (roll < 0.30f)
        {
            EnemyMove worst = null;
            float worstScore = float.MaxValue;

            for (int i = 0; i < pool.Count; i++)
            {
                var m = pool[i];
                if (m == null) continue;

                float score = m.baseDamage + m.qteSpeed + m.trickiness;
                if (score < worstScore)
                {
                    worstScore = score;
                    worst = m;
                }
            }

            return worst;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    // ---------------------------------------------------------
    // BEST MOVE PICKER
    // ---------------------------------------------------------
    private static EnemyMove PickBestMoveByScore(
        List<EnemyMove> pool,
        EnemyRuntimeData runtime,
        EnemyPartySynergyCoordinator.SynergyRole role)
    {
        if (pool == null || pool.Count == 0)
            return null;

        EnemyMove best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < pool.Count; i++)
        {
            var m = pool[i];
            if (m == null) continue;

            float pref = PersonalityBehavior.GetMovePreferenceScore(m, runtime);
            pref += EnemyPartySynergyCoordinator.GetRoleBonus(m, role);

            runtime.moveUsage.TryGetValue(m.moveName, out int usage);
            float fatiguePenalty = usage * 0.25f;

            float randomTiebreak = Random.value * 0.01f;

            float finalScore = pref - fatiguePenalty + randomTiebreak;

            if (usage == 0)
                finalScore += 0.15f;

            if (finalScore > bestScore)
            {
                bestScore = finalScore;
                best = m;
            }
        }

        return best;
    }

    // ---------------------------------------------------------
    // LOGGING
    // ---------------------------------------------------------
    private static List<EnemyMove> LogAndReturnCombo(List<EnemyMove> combo)
    {
        if (DebugAI)
        {
            if (combo == null || combo.Count == 0)
            {
                Debug.Log("[AI] Final Combo -> (none)");
            }
            else
            {
                _sb.Length = 0;

                for (int i = 0; i < combo.Count; i++)
                {
                    var m = combo[i];
                    _sb.Append(m.moveName)
                    .Append("(Cost:")
                    .Append(m.slotCost)
                    .Append(")");

                    if (i < combo.Count - 1)
                        _sb.Append(", ");
                }

                Debug.Log($"[AI] Final Combo → {_sb}");
            }
        }

        return combo;
    }
}
