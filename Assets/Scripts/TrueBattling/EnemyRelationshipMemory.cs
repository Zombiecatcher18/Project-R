using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks affinity (relationship score) between enemies for synergy and behavior coordination.
/// Maintains two-tier memory system: longTermAffinity (persists across battles) and shortTermAffinity (battle-local).
/// 
/// Memory System:
/// - longTermAffinity: Permanent affinity scores from previous encounters (decays slowly across many battles).
/// - shortTermAffinity: Volatile affinity modified during current battle (resets when battle ends).
/// - lastInteractionTurn: Timestamp tracking to implement decay-by-inactivity mechanic.
/// 
/// Affinity Impact: Higher affinity = enemies more likely to coordinate combos, protect each other, synergize moves.
/// Used by: EnemySynergyMatrix for combo selection, EnemyPartySynergyCoordinator for party behavior.
/// Serialization: [System.Serializable] enables JSON persistence in SaveData.
/// </summary>
[System.Serializable]
public class EnemyRelationshipMemory
{
    /// <summary>
    /// True if an ally died since this enemy's last turn.
    /// Reset to false after turn logic is applied.
    /// </summary>
    public bool lastAllyDied = false;

    /// <summary>
    /// ID of the ally who died most recently.
    /// Null/empty if none.
    /// </summary>
    public string lastAllyDiedID = null;

    // ===== AFFINITY STORAGE =====
    /// <summary>
    /// Permanent affinity scores between enemies (survives battle end and scene transitions).
    /// Key = enemy ID string, Value = affinity score (positive = ally, negative = rival/hostile).
    /// Accumulates 25% of each battle's shortTermAffinity at end via CommitLongTerm().
    /// </summary>
    public Dictionary<string, float> longTermAffinity = new Dictionary<string, float>();

    /// <summary>
    /// Volatile affinity scores modified during current battle (resets when battle completes).
    /// Key = enemy ID string, Value = battle-local affinity delta.
    /// Decays via DecayShortTerm() if interaction exceeds 3 turns without update.
    /// Commits 25% to longTermAffinity at battle end.
    /// </summary>
    public Dictionary<string, float> shortTermAffinity = new Dictionary<string, float>();

    /// <summary>
    /// Tracks last turn each enemy was interacted with (for decay-by-inactivity mechanic).
    /// Key = enemy ID string, Value = turn number of last AddAffinity() call.
    /// Used by DecayShortTerm() to reduce affinity if enemies ignore each other for 3+ turns.
    /// </summary>
    public Dictionary<string, int> lastInteractionTurn = new Dictionary<string, int>();

    // ===== AFFINITY QUERIES =====
    /// <summary>
    /// Returns combined affinity toward specified enemy (sum of long-term and short-term).
    /// Formula: combined = longTermAffinity[otherID] + shortTermAffinity[otherID]
    /// Returns 0f if otherID not in either dictionary (safe null handling).
    /// Called by: EnemySynergyMatrix for move selection, EnemyPartySynergyCoordinator for behavior.
    /// </summary>
    public float GetAffinity(string otherID)
    {
        float longTerm = longTermAffinity.ContainsKey(otherID) ? longTermAffinity[otherID] : 0f;
        float shortTerm = shortTermAffinity.ContainsKey(otherID) ? shortTermAffinity[otherID] : 0f;
        return longTerm + shortTerm;
    }

    // ===== AFFINITY MODIFICATIONS =====
    /// <summary>
    /// Modifies short-term affinity during battle and records interaction timestamp.
    /// 
    /// Behavior:
    /// - Creates shortTermAffinity entry if first time interacting with otherID
    /// - Adds amount to existing shortTermAffinity (positive = increases bond, negative = increases rivalry)
    /// - Updates lastInteractionTurn[otherID] to currentTurn for decay tracking
    /// - Logs change for debugging ("Affinity toward [ID] changed by +2.5. Total now: 3.50")
    /// 
    /// Called by: EnemyPartySynergyCoordinator after combo interactions, EnemyBattleCoordinator for alliance changes.
    /// Parameters:
    ///   - otherID: Target enemy identifier (string)
    ///   - amount: Affinity delta (typically ±2.5 for standard interactions)
    ///   - currentTurn: Current battle turn number (for decay tracking)
    /// </summary>
    public void AddAffinity(string otherID, float amount, int currentTurn)
    {
        if (!shortTermAffinity.ContainsKey(otherID))
            shortTermAffinity[otherID] = 0f;

        shortTermAffinity[otherID] += amount;

        lastInteractionTurn[otherID] = currentTurn;

        Debug.Log($"[Memory] Affinity toward {otherID} changed by {amount:+0.00;-0.00}. Total now: {GetAffinity(otherID):F2}");
    }

    // ===== DECAY MECHANICS =====
    /// <summary>
    /// Applies decay-by-inactivity to short-term affinity (simulates forgetting interactions).
    /// 
    /// Mechanic: If currentTurn - lastInteractionTurn >= 3 turns, multiply shortTermAffinity[id] by 0.5f.
    /// Effect: Repeated non-interaction causes affinity to exponentially decay (0.5 * 0.5 = 0.25 after 2 decay cycles).
    /// Used for: Prevents long-term affinity build from single interaction.
    /// 
    /// Called by: BattleManager at each turn or EnemyPartySynergyCoordinator each enemy turn.
    /// Example: If enemies didn't interact for 4 turns and had 10.0 affinity, becomes 5.0.
    /// </summary>
    public void DecayShortTerm(int currentTurn)
    {
        List<string> keys = new List<string>(shortTermAffinity.Keys);

        foreach (var id in keys)
        {
            int lastTurn = lastInteractionTurn.ContainsKey(id) ? lastInteractionTurn[id] : 0;

            // Decay if inactivity exceeds 3-turn threshold
            if (currentTurn - lastTurn >= 3)
            {
                shortTermAffinity[id] *= 0.5f;  // 50% decay
                Debug.Log($"[Memory] Short-term affinity toward {id} decayed to {shortTermAffinity[id]:F2}");
            }
        }
    }

    // ===== BATTLE END PERSISTENCE =====
    /// <summary>
    /// Commits short-term affinity gains to long-term memory (end-of-battle consolidation).
    /// 
    /// Formula: longTermAffinity[id] += shortTermAffinity[id] * 0.25f
    /// Effect: Only 25% of battle-local affinity becomes permanent; rest is discarded.
    /// Rationale: Single battles shouldn't define long-term relationships; consistent patterns matter.
    /// 
    /// Called by: BattleManager.EndBattle() after victory/defeat/flee.
    /// After this: shortTermAffinity should be cleared/reset for next battle.
    /// </summary>
    public void CommitLongTerm()
    {
        foreach (var kvp in shortTermAffinity)
        {
            if (!longTermAffinity.ContainsKey(kvp.Key))
                longTermAffinity[kvp.Key] = 0f;

            longTermAffinity[kvp.Key] += kvp.Value * 0.25f; // 25% becomes permanent
        }

        Debug.Log("[Memory] Long-term affinity updated.");
    }
}
