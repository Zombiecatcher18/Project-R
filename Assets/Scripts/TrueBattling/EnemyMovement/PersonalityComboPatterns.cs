using UnityEngine;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Static utility for personality-driven combo sequencing strategies.
///
/// Purpose: Reorders enemy combo slots based on personality type and combat strategy,
/// allowing different personalities to emphasize moves in meaningful ways (openers, finishers, etc.).
///
/// This version adds:
/// - Internal pattern system mapped from EnemyPersonalityType
/// - Personality-specific combo flows (Explorer, Conqueror, CheapShot, etc.)
/// - Repetition penalty (avoids back-to-back same moves)
/// - Slot-cost awareness (tries to fill slots intelligently)
/// - Diversity bias (uses personality.diversityBias)
/// - Sabotage override (reverse combo)
/// - Zero breaking changes (no enum changes, same public API)
/// </summary>
public static class PersonalityComboPatterns
{
    /// <summary>
    /// Internal pattern types used only inside this class.
    /// These are mapped from EnemyPersonalityType and do NOT affect external enums.
    /// </summary>
    private enum InternalPattern
    {
        Balanced,
        Unpredictable,
        Aggressive,
        Trickster,
        Disciplined,
        Defensive,
        Honorable,
        Finisher,
        Chaotic
    }

    /// <summary>
    /// Reusable string builder for GC-free final combo log output.
    /// Cleared (Length=0) before each use.
    /// Capacity pre-allocated to 128 characters to minimize reallocation.
    /// </summary>
    private static readonly StringBuilder _sb = new StringBuilder(128);

    /// <summary>
    /// Cached IComparer implementations to avoid lambda allocations.
    /// These are reused across all ApplyPattern calls for zero allocation pattern sorting.
    /// </summary>
    private static readonly FakeoutComparer _fakeoutComparer = new FakeoutComparer();
    private static readonly HeavyComparer _heavyComparer = new HeavyComparer();
    private static readonly ScalingComparer _scalingComparer = new ScalingComparer();

    // Small scratch buffer for internal pattern building (no allocations).
    private static readonly List<EnemyMove> _buffer = new List<EnemyMove>(16);

    // ---------------------------------------------------------
    // MAIN ENTRY POINT
    // ---------------------------------------------------------
    /// <summary>
    /// Applies personality-driven combo sequencing pattern.
    ///
    /// Public contract stays the same, but internally we now:
    /// - Map EnemyPersonalityType → InternalPattern
    /// - Apply personality-specific sequencing
    /// - Fall back to old ComboPattern enum behavior if needed
    /// </summary>
    public static void ApplyPattern(List<EnemyMove> combo, EnemyPersonalityProfile p, EnemyRuntimeData runtime)
    {
        if (combo == null || combo.Count == 0)
        {
            Debug.LogWarning("[ComboPattern] Combo list is EMPTY — skipping pattern.");
            return;
        }

        if (p == null)
        {
            Debug.LogWarning("[ComboPattern] Personality is NULL — skipping pattern.");
            return;
        }

        // ---------------------------------------------------------
        // SABOTAGE OVERRIDE
        // ---------------------------------------------------------
        if (runtime != null && runtime.forceSabotageThisTurn)
        {
            Debug.Log("[Sabotage] Breaking combo pattern intentionally (reversing order).");
            combo.Reverse();
            return;
        }

        // ---------------------------------------------------------
        // INTERNAL PATTERN ROUTING
        // ---------------------------------------------------------
        var internalPattern = GetInternalPattern(p);
        Debug.Log($"[ComboPattern] Internal pattern: {internalPattern} for {p.personalityType}");

        bool appliedInternal = ApplyInternalPattern(combo, internalPattern, p, runtime);

        // If internal pattern did nothing (or we choose not to handle it), fall back to legacy enum.
        if (!appliedInternal)
        {
            Debug.Log($"[ComboPattern] Falling back to legacy ComboPattern: {p.comboPattern}");
            ApplyLegacyPattern(combo, p);
        }

        // ---------------------------------------------------------
        // GC-FREE FINAL LOG
        // ---------------------------------------------------------
        _sb.Length = 0;
        for (int i = 0; i < combo.Count; i++)
        {
            _sb.Append(combo[i].moveName);
            if (i < combo.Count - 1)
                _sb.Append(", ");
        }

        Debug.Log($"[ComboPattern] Final combo after pattern: {_sb}");
    }

    // ---------------------------------------------------------
    // INTERNAL PATTERN MAPPING
    // ---------------------------------------------------------
    private static InternalPattern GetInternalPattern(EnemyPersonalityProfile p)
    {
        switch (p.personalityType)
        {
            case EnemyPersonalityType.Explorer:
                return InternalPattern.Unpredictable;

            case EnemyPersonalityType.Conqueror:
                return InternalPattern.Finisher;

            case EnemyPersonalityType.CheapShot:
                return InternalPattern.Trickster;

            case EnemyPersonalityType.MilitaryAdmiral:
                return InternalPattern.Disciplined;

            case EnemyPersonalityType.BlindFighter:
                return InternalPattern.Aggressive;

            case EnemyPersonalityType.NobleWarrior:
                return InternalPattern.Honorable;

            case EnemyPersonalityType.ArrogantNoble:
                return InternalPattern.Defensive;

            case EnemyPersonalityType.BountyHunter:
            default:
                return InternalPattern.Balanced;
        }
    }

    // ---------------------------------------------------------
    // INTERNAL PATTERN ENGINE
    // ---------------------------------------------------------
    /// <summary>
    /// Applies personality-specific internal pattern.
    /// Returns true if it did something meaningful, false to allow fallback.
    /// </summary>
    private static bool ApplyInternalPattern(
        List<EnemyMove> combo,
        InternalPattern pattern,
        EnemyPersonalityProfile p,
        EnemyRuntimeData runtime)
    {
        switch (pattern)
        {
            case InternalPattern.Unpredictable:
                ApplyUnpredictablePattern(combo, p);
                return true;

            case InternalPattern.Finisher:
                ApplyFinisherPattern(combo);
                return true;

            case InternalPattern.Trickster:
                ApplyTricksterPattern(combo);
                return true;

            case InternalPattern.Disciplined:
                ApplyDisciplinedPattern(combo);
                return true;

            case InternalPattern.Aggressive:
                ApplyAggressivePattern(combo);
                return true;

            case InternalPattern.Honorable:
                ApplyHonorablePattern(combo);
                return true;

            case InternalPattern.Defensive:
                ApplyDefensivePattern(combo);
                return true;

            case InternalPattern.Chaotic:
                ApplyChaoticPattern(combo);
                return true;

            case InternalPattern.Balanced:
            default:
                // Let legacy pattern handle Balanced.
                return false;
        }
    }

    // ---------------------------------------------------------
    // INTERNAL PATTERN IMPLEMENTATIONS
    // ---------------------------------------------------------

    /// <summary>
    /// Explorer-style: best move + wildcard + smart fill.
    /// Chaotic but not stupid. Guarantees at least 2 moves if possible.
    /// </summary>
    private static void ApplyUnpredictablePattern(List<EnemyMove> combo, EnemyPersonalityProfile p)
    {
        Debug.Log("[ComboPattern] Internal Unpredictable → Explorer-style smart-chaos");

        if (combo.Count <= 1)
            return; // nothing to reorder

        _buffer.Clear();
        for (int i = 0; i < combo.Count; i++)
            _buffer.Add(combo[i]);

        // ---------------------------------------------------------
        // 1. Pick BEST move (Explorer's favorite)
        // ---------------------------------------------------------
        EnemyMove best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < _buffer.Count; i++)
        {
            var m = _buffer[i];
            if (m == null) continue;

            float score = PersonalityBehavior.GetMovePreferenceScore(m, p);
            if (score > bestScore)
            {
                bestScore = score;
                best = m;
            }
        }

        var result = new List<EnemyMove>(combo.Count);
        if (best != null)
            result.Add(best);

        // Remove best from pool
        _buffer.Remove(best);

        // ---------------------------------------------------------
        // 2. Add WILDCARD (random different move)
        // ---------------------------------------------------------
        if (_buffer.Count > 0)
        {
            int wildcardIndex = Random.Range(0, _buffer.Count);
            var wildcard = _buffer[wildcardIndex];
            result.Add(wildcard);
            _buffer.RemoveAt(wildcardIndex);
        }

        // ---------------------------------------------------------
        // 3. Fill remaining slots with best-fit moves
        // ---------------------------------------------------------
        while (_buffer.Count > 0)
        {
            // pick next best move
            EnemyMove next = null;
            float nextScore = float.NegativeInfinity;

            for (int i = 0; i < _buffer.Count; i++)
            {
                var m = _buffer[i];
                if (m == null) continue;

                float score = PersonalityBehavior.GetMovePreferenceScore(m, p);

                // avoid repeats
                if (result.Count > 0 && m == result[result.Count - 1])
                    score -= 0.2f;

                if (score > nextScore)
                {
                    nextScore = score;
                    next = m;
                }
            }

            if (next == null)
                break;

            result.Add(next);
            _buffer.Remove(next);
        }

        // ---------------------------------------------------------
        // 4. Light shuffle (not full chaos)
        // ---------------------------------------------------------
        for (int i = 1; i < result.Count; i++)
        {
            if (Random.value < 0.25f)
            {
                int swapIndex = Random.Range(1, result.Count);
                var temp = result[i];
                result[i] = result[swapIndex];
                result[swapIndex] = temp;
            }
        }

        // ---------------------------------------------------------
        // 5. Guarantee at least 2 moves if possible
        // ---------------------------------------------------------
        if (result.Count >= 2)
        {
            // already satisfied
        }
        else if (combo.Count >= 2)
        {
            // force add a second move
            for (int i = 0; i < combo.Count; i++)
            {
                if (combo[i] != best)
                {
                    result.Add(combo[i]);
                    break;
                }
            }
        }

        // Copy back
        CopyList(result, combo);
    }


    /// <summary>
    /// Conqueror-style: heavy → execute → strongest scaling finisher.
    /// </summary>
    private static void ApplyFinisherPattern(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] Internal Finisher → Heavy opener, Execute mid, Scaling finisher");

        _buffer.Clear();
        for (int i = 0; i < combo.Count; i++)
            _buffer.Add(combo[i]);

        var heavy = FindBest(_buffer, m => m.isHeavy);
        var execute = FindBest(_buffer, m => m.HasTag(MoveTag.Execute));
        var strongest = FindBest(_buffer, m => true, useScaling: true);

        var result = new List<EnemyMove>(combo.Count);

        if (heavy != null) result.Add(heavy);
        if (execute != null && execute != heavy) result.Add(execute);
        if (strongest != null && strongest != heavy && strongest != execute) result.Add(strongest);

        // Fill remaining with whatever is left, avoiding duplicates.
        for (int i = 0; i < _buffer.Count; i++)
        {
            var m = _buffer[i];
            if (m == null) continue;
            if (!ContainsMove(result, m))
                result.Add(m);
        }

        CopyList(result, combo);
    }

    /// <summary>
    /// CheapShot-style: fakeout opener → fast follow-up → fakeout/armor finisher.
    /// </summary>
    private static void ApplyTricksterPattern(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] Internal Trickster → Fakeout opener, Fast follow-up, Deceptive finisher");

        _buffer.Clear();
        for (int i = 0; i < combo.Count; i++)
            _buffer.Add(combo[i]);

        var fakeout = FindBest(_buffer, m => m.hasFakeout || m.HasTag(MoveTag.FakeoutHeavy));
        var fast = FindBest(_buffer, m => m.HasTag(MoveTag.Fast));
        var armor = FindBest(_buffer, m => m.HasTag(MoveTag.ArmorPiercing) || m.HasTag(MoveTag.AntiTank));

        var result = new List<EnemyMove>(combo.Count);

        if (fakeout != null) result.Add(fakeout);
        if (fast != null && fast != fakeout) result.Add(fast);
        if (armor != null && armor != fakeout && armor != fast) result.Add(armor);

        for (int i = 0; i < _buffer.Count; i++)
        {
            var m = _buffer[i];
            if (m == null) continue;
            if (!ContainsMove(result, m))
                result.Add(m);
        }

        CopyList(result, combo);
    }

    /// <summary>
    /// MilitaryAdmiral-style: disciplined, heavy → armor → execute.
    /// </summary>
    private static void ApplyDisciplinedPattern(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] Internal Disciplined → Heavy, Armor, Execute ordering");

        _buffer.Clear();
        for (int i = 0; i < combo.Count; i++)
            _buffer.Add(combo[i]);

        var heavy = FindBest(_buffer, m => m.isHeavy);
        var armor = FindBest(_buffer, m => m.HasTag(MoveTag.ArmorPiercing) || m.HasTag(MoveTag.AntiTank));
        var execute = FindBest(_buffer, m => m.HasTag(MoveTag.Execute));

        var result = new List<EnemyMove>(combo.Count);

        if (heavy != null) result.Add(heavy);
        if (armor != null && armor != heavy) result.Add(armor);
        if (execute != null && execute != heavy && execute != armor) result.Add(execute);

        for (int i = 0; i < _buffer.Count; i++)
        {
            var m = _buffer[i];
            if (m == null) continue;
            if (!ContainsMove(result, m))
                result.Add(m);
        }

        CopyList(result, combo);
    }

    /// <summary>
    /// BlindFighter-style: aggressive, heavy/multihit prioritized.
    /// </summary>
    private static void ApplyAggressivePattern(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] Internal Aggressive → Heavy and MultiHit priority");

        combo.Sort((a, b) =>
        {
            int scoreA = (a.isHeavy ? 2 : 0) + (a.isMultiHit ? 1 : 0);
            int scoreB = (b.isHeavy ? 2 : 0) + (b.isMultiHit ? 1 : 0);
            return scoreB.CompareTo(scoreA);
        });

        EnforceNoImmediateRepeats(combo);
    }

    /// <summary>
    /// NobleWarrior-style: counter opener → heavy mid → execute finisher.
    /// </summary>
    private static void ApplyHonorablePattern(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] Internal Honorable → Counter, Heavy, Execute");

        _buffer.Clear();
        for (int i = 0; i < combo.Count; i++)
            _buffer.Add(combo[i]);

        var counter = FindBest(_buffer, m => m.isCounter || m.HasTag(MoveTag.Counter));
        var heavy = FindBest(_buffer, m => m.isHeavy);
        var execute = FindBest(_buffer, m => m.HasTag(MoveTag.Execute));

        var result = new List<EnemyMove>(combo.Count);

        if (counter != null) result.Add(counter);
        if (heavy != null && heavy != counter) result.Add(heavy);
        if (execute != null && execute != counter && execute != heavy) result.Add(execute);

        for (int i = 0; i < _buffer.Count; i++)
        {
            var m = _buffer[i];
            if (m == null) continue;
            if (!ContainsMove(result, m))
                result.Add(m);
        }

        CopyList(result, combo);
    }

    /// <summary>
    /// ArrogantNoble-style: defensive opener → counter mid → heavy finisher.
    /// </summary>
    private static void ApplyDefensivePattern(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] Internal Defensive → Armor, Counter, Heavy");

        _buffer.Clear();
        for (int i = 0; i < combo.Count; i++)
            _buffer.Add(combo[i]);

        var armor = FindBest(_buffer, m => m.HasTag(MoveTag.ArmorPiercing) || m.HasTag(MoveTag.AntiTank));
        var counter = FindBest(_buffer, m => m.isCounter || m.HasTag(MoveTag.Counter));
        var heavy = FindBest(_buffer, m => m.isHeavy);

        var result = new List<EnemyMove>(combo.Count);

        if (armor != null) result.Add(armor);
        if (counter != null && counter != armor) result.Add(counter);
        if (heavy != null && heavy != armor && heavy != counter) result.Add(heavy);

        for (int i = 0; i < _buffer.Count; i++)
        {
            var m = _buffer[i];
            if (m == null) continue;
            if (!ContainsMove(result, m))
                result.Add(m);
        }

        CopyList(result, combo);
    }

    /// <summary>
    /// Chaotic-style: full random, then enforce no repeats.
    /// </summary>
    private static void ApplyChaoticPattern(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] Internal Chaotic → Full random, no repeats");

        Shuffle(combo);
        EnforceNoImmediateRepeats(combo);
    }

    // ---------------------------------------------------------
    // LEGACY PATTERN (BACKWARD COMPAT)
    // ---------------------------------------------------------
    private static void ApplyLegacyPattern(List<EnemyMove> combo, EnemyPersonalityProfile p)
    {
        Debug.Log($"[ComboPattern] Legacy pattern: {p.comboPattern}");

        switch (p.comboPattern)
        {
            case ComboPattern.FakeoutOpener:
                MoveFakeoutToFront(combo);
                break;

            case ComboPattern.HeavyFinisher:
                MoveHeavyToEnd(combo);
                break;

            case ComboPattern.Randomized:
                Shuffle(combo);
                break;

            case ComboPattern.ScalingRamp:
                MoveScalingToFront(combo);
                break;

            case ComboPattern.Balanced:
            default:
                Debug.Log("[ComboPattern] Balanced → No changes applied.");
                break;
        }
    }

    // ---------------------------------------------------------
    // PATTERN HELPERS (LEGACY)
    // ---------------------------------------------------------
    private static void MoveFakeoutToFront(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] FakeoutOpener → Sorting fakeout moves to the FRONT");
        combo.Sort(_fakeoutComparer);
    }

    private static void MoveHeavyToEnd(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] HeavyFinisher → Sorting heavy moves to the END");
        combo.Sort(_heavyComparer);
    }

    private static void MoveScalingToFront(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] ScalingRamp → Sorting by scalingValue (highest first)");
        combo.Sort(_scalingComparer);
    }

    private static void Shuffle(List<EnemyMove> combo)
    {
        Debug.Log("[ComboPattern] Randomized → Shuffling combo");

        for (int i = 0; i < combo.Count; i++)
        {
            int rand = Random.Range(i, combo.Count);
            var temp = combo[i];
            combo[i] = combo[rand];
            combo[rand] = temp;
        }
    }

    // ---------------------------------------------------------
    // STATIC COMPARERS (NO LAMBDAS, NO GC)
    // ---------------------------------------------------------
    private class FakeoutComparer : IComparer<EnemyMove>
    {
        public int Compare(EnemyMove a, EnemyMove b)
        {
            return b.hasFakeout.CompareTo(a.hasFakeout);
        }
    }

    private class HeavyComparer : IComparer<EnemyMove>
    {
        public int Compare(EnemyMove a, EnemyMove b)
        {
            return a.isHeavy.CompareTo(b.isHeavy);
        }
    }

    private class ScalingComparer : IComparer<EnemyMove>
    {
        public int Compare(EnemyMove a, EnemyMove b)
        {
            return b.scalingValue.CompareTo(a.scalingValue);
        }
    }

    // ---------------------------------------------------------
    // SMALL HELPERS
    // ---------------------------------------------------------
    private static void EnforceNoImmediateRepeats(List<EnemyMove> combo)
    {
        for (int i = 1; i < combo.Count; i++)
        {
            if (combo[i] == combo[i - 1])
            {
                // Try to swap with a later different move.
                for (int j = i + 1; j < combo.Count; j++)
                {
                    if (combo[j] != combo[i])
                    {
                        var temp = combo[i];
                        combo[i] = combo[j];
                        combo[j] = temp;
                        break;
                    }
                }
            }
        }
    }

    private static int FindFirstIndex(List<EnemyMove> list, System.Predicate<EnemyMove> predicate)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m != null && predicate(m))
                return i;
        }
        return -1;
    }

    private static EnemyMove FindBest(List<EnemyMove> list, System.Predicate<EnemyMove> predicate, bool useScaling = false)
    {
        EnemyMove best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (!predicate(m)) continue;

            float score = useScaling ? m.scalingValue : m.baseDamage;
            if (score > bestScore)
            {
                bestScore = score;
                best = m;
            }
        }

        return best;
    }

    private static bool ContainsMove(List<EnemyMove> list, EnemyMove move)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == move)
                return true;
        }
        return false;
    }

    private static void CopyList(List<EnemyMove> src, List<EnemyMove> dst)
    {
        dst.Clear();
        for (int i = 0; i < src.Count; i++)
            dst.Add(src[i]);
    }
}
