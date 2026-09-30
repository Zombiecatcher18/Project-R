using UnityEngine;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;
using NUnit.Framework;

/// <summary>
/// Battle-time instance data for an enemy, created from EnemyInfo template.
/// Tracks current HP, AI state, memory, personality behaviors, and turn/strategy modifiers.
/// Cloned from EnemyInfo at battle start to isolate runtime modifications from asset.
/// Used by EnemyAttackController, EnemyBrain, and personality systems for combat decisions.
/// </summary>
[System.Serializable]
public class EnemyRuntimeData
{
    // ============================
    // NESTED STATE STRUCTS
    // ============================

    /// <summary>
    /// Per-turn, temporary modifiers that are reset at the start of each turn.
    /// These influence move weighting and behavior for the current turn only.
    /// </summary>
    [System.Serializable]
    public struct TurnState
    {
        public float aggressiveBias;   // extra aggression this turn
        public float scalingBonus;     // extra scaling/damage this turn
        public float randomFactor;     // extra randomness this turn
        public bool forceFakeout;      // force fakeout this turn

        public void Reset()
        {
            aggressiveBias = 0f;
            scalingBonus = 0f;
            randomFactor = 0f;
            forceFakeout = false;
        }
    }

    /// <summary>
    /// Higher-level strategic tendencies that can be adjusted over time.
    /// Used by brains/personality systems to bias move selection and planning.
    /// </summary>
    [System.Serializable]
    public struct StrategyState
    {
        public float executeBias;        // use executes more
        public float defenseBias;        // use armor/counter more
        public float aggressionBias;     // use heavy/multi-hit more
        public float fakeoutBias;        // use fakeouts more
        public float armorPierceBias;    // use armor-piercing more
        public float finisherBias;       // use big finishers at low HP

        public bool saveBigMovesThisTurn; // prefer cheap moves, hold big ones
        public bool goAllInThisTurn;      // spend everything, no saving
    }

    // ============================
    // BASE STAT INFO
    // ============================

    /// <summary>Reference to cloned EnemyInfo asset; contains stat scaling, move list, and base configuration.</summary>
    public EnemyInfo info;
    /// <summary>Current health during battle; decreases when damaged, triggers defeat at 0.</summary>
    public int currentHP;
    /// <summary>Enemy level at battle start; determines SmartAI eligibility (true if level >= 2).</summary>
    public int enemyLevel;
    /// <summary>Maximum health after all scaling applied; used for health bar calculations.</summary>
    public int maxHP;
    /// <summary>Attack power after scaling; multiplied by 0.04f in damage calculation.</summary>
    public int attack;
    /// <summary>Defense value after scaling; used in damage reduction formula (100/(100+defense)).</summary>
    public int defense;
    public int counteratk;
    public int maxComboSlots;
    public int deckSize;

    public float counterPressure = 0f;

    /// <summary>
    /// The enemy’s persistent hand of moves. Unused moves stay here between turns,
    /// just like the player’s hand system.
    /// </summary>
    public List<EnemyMove> currentHand = new List<EnemyMove>();

    /// <summary>True if enemyLevel >= 2; enables advanced AI decision-making patterns.</summary>
    public bool SmartAI => enemyLevel >= 2;

    /// <summary>Persistent memory system tracking relationships and battle events.</summary>
    public EnemyRelationshipMemory memory = new EnemyRelationshipMemory();

    // Pattern awareness (per-enemy local memory)
    public PatternTracker pattern = new PatternTracker();

    /// <summary>Unique identifier copied from EnemyInfo; enables save/load matching and quest tracking.</summary>
    public string enemyID;

    /// <summary>Personality profile reference; determines behavior patterns and move preferences (copied from EnemyInfo).</summary>
    public EnemyPersonalityProfile personality;

    /// <summary>Position in enemy party (0-3); used for party synergy and positioning logic.</summary>
    public int partyIndex;
    /// <summary>Total enemy party size; used for contextual decision-making.</summary>
    public int partySize;

    /// <summary>Reference to EnemyBrain for move selection and AI decision orchestration.</summary>
    public EnemyBrain brain;

    // ============================
    // PERSISTENT AI BIASES
    // ============================

    /// <summary>Bias toward armor-piercing moves; decays each turn, incentivizes strategic move selection.</summary>
    public float armorPiercingBias = 0f;
    /// <summary>Bias toward aggressive moves; decays each turn, drives tempo decisions.</summary>
    public float aggressiveBias = 0f;
    /// <summary>Bias toward finishing moves; decays each turn, influences combo building when player health is low.</summary>
    public float executeBias = 0f;
    // Pattern Awareness Biases (added for Pattern System)
    public float counterBias = 0f;     // increases counter chance
    public float qteSpeedBias = 0f;    // speeds up enemy QTE
    public float preDelayBias = 0f;    // reduces pre-delay
    public float turnMeterDrain = 0f;  // drains player's turn meter (Conqueror)

    /// <summary>
    /// Move usage history for frequency analysis; tracks how many times each move has been used.
    /// Used by personality systems to avoid move repetition and create varied strategies.
    /// </summary>
    public Dictionary<string, int> moveUsage = new Dictionary<string, int>(8);

    // ============================
    // TURN / STRATEGY STATE
    // ============================

    /// <summary>Per-turn modifiers, reset at the start of each turn.</summary>
    public TurnState turn;

    /// <summary>Higher-level strategic tendencies for this enemy.</summary>
    public StrategyState strategy;

    // ============================
    // TURN TRACKING / SYNERGY
    // ============================

    /// <summary>Counter tracking which turn this enemy is on; used for turn-based behavior changes.</summary>
    public int turnCount = 0;

    /// <summary>Synergy score with allies in party; influences cooperative move selection (updated each turn).</summary>
    public float synergyScore;

    /// <summary>Flag forcing sabotage move selection when party synergy is poor; forces contrarian strategies.</summary>
    public bool forceSabotageThisTurn;

    public int damageTakenLastTurn = 0;
    public int synergyLevel = 0;

    // ============================
    // CONSTRUCTOR
    // ============================

    /// <summary>
    /// Creates battle-time instance data from EnemyInfo template.
    /// Clones EnemyInfo to prevent runtime modifications affecting the asset.
    /// Initializes all stats, memory, and move tracking from base configuration.
    /// Called by spawners/BattleManager when creating enemy instances during battle initialization.
    /// </summary>
    public EnemyRuntimeData(EnemyInfo baseInfo)
    {
        // Clone ScriptableObject so runtime changes don't affect the asset
        info = ScriptableObject.Instantiate(baseInfo);

        // Copy base stats from cloned info
        maxHP = info.maxHP;
        attack = info.attack;
        defense = info.defense;
        counteratk = info.counteratk;
        currentHP = info.maxHP;

        // NOTE: enemyLevel should be set externally before using deckSize/maxComboSlots
        deckSize = info.GetDeckSizeForLevel(enemyLevel);

        // Copy personality reference (safe, read-only from EnemyInfo)
        personality = info.personality;

        // Initialize move usage tracking with known moves to enable frequency analysis
        if (info.moves != null)
        {
            for (int i = 0; i < info.moves.Count; i++)
            {
                var move = info.moves[i];
                if (move != null && !moveUsage.ContainsKey(move.moveName))
                    moveUsage.Add(move.moveName, 0);
            }
        }

        // Ensure turn state starts clean
        turn.Reset();
    }

    [System.Serializable]
    public class PatternTracker
    {
        // --- CONFIG ---
        public int maxHistory = 6;          // how many recent turns/combos we remember
        public float awarenessMeter = 0f;   // 0–100: learning phase
        public float predictionMeter = 0f;  // 100+: prediction phase

        // Move-level awareness (per move name)
        public Dictionary<string, float> movePredictability = new Dictionary<string, float>();

        // Recent structural patterns
        public List<string> recentOpeners   = new List<string>();
        public List<string> recentFinishers = new List<string>();
        public List<string> recentCombos    = new List<string>(); // combo signatures

        // QTE timing awareness
        public List<QTEResult> recentQteResults = new List<QTEResult>();

        // Cached scores (updated when we feed data)
        public float moveSpamScore;        // 0–1: how much one move dominates
        public float openerRepeatScore;    // 0–1: same opener often?
        public float finisherRepeatScore;  // 0–1: same finisher often?
        public float comboRepeatScore;     // 0–1: same combo pattern often?
        public float qtePredictableScore;  // 0–1: QTE timing consistency

        public void DebugPatternState(string enemyName)
        {
            UnityEngine.Debug.Log(
                $"[PATTERN-STATE] {enemyName} | " +
                $"Awareness={awarenessMeter:F1}, Prediction={predictionMeter:F1}"
            );

            UnityEngine.Debug.Log(
                $"[PATTERN-STATE] Recent Openers: {string.Join(", ", recentOpeners)}"
            );

            UnityEngine.Debug.Log(
                $"[PATTERN-STATE] Recent Finishers: {string.Join(", ", recentFinishers)}"
            );

            UnityEngine.Debug.Log(
                $"[PATTERN-STATE] Recent Combos: {string.Join(" | ", recentCombos)}"
            );

            UnityEngine.Debug.Log(
                $"[PATTERN-STATE] Move Predictability: " +
                $"{string.Join(", ", movePredictability)}"
            );
        }

        public void AddPrediction(float amount, EnemyPersonalityProfile p, string enemyName)
        {
            float before = predictionMeter;
            predictionMeter += amount * p.predictionGainMultiplier;

            Debug.Log($"[PREDICTION-UP] {enemyName} | {before:F1} → {predictionMeter:F1}");
        }
       
        public void AddAwareness(
            QTEResult result,
        float synergyScore,
        bool fakeoutHit,
        bool counterTriggered,
        EnemyPersonalityProfile p,
        string enemyName
        )
        {
            float gain = 0f;

            // Base gain from QTE result
            switch (result)
            {
                case QTEResult.Perfect: gain = 2f; break;
                case QTEResult.Good:    gain = 1f; break;
                case QTEResult.Ok:      gain = 0.5f; break;
                case QTEResult.Miss:    gain = 0.25f; break;
            }

            // Synergy influence
            gain += synergyScore * 0.5f;

            // Fakeout influence
            if (fakeoutHit)
                gain += 3f;

            // Counter influence
            if (counterTriggered)
                gain += 5f;

            // Personality multiplier
            gain *= p.awarenessGainMultiplier;

            float before = awarenessMeter;
            awarenessMeter += gain;

            Debug.Log($"[AWARENESS-UP] {enemyName} | {before:F1} → {awarenessMeter:F1} (gain={gain:F2})");
        }

        public void Clear()
        {
            awarenessMeter = 0f;
            predictionMeter = 0f;

            movePredictability.Clear();
            recentOpeners.Clear();
            recentFinishers.Clear();
            recentCombos.Clear();
            recentQteResults.Clear();

            moveSpamScore = 0f;
            openerRepeatScore = 0f;
            finisherRepeatScore = 0f;
            comboRepeatScore = 0f;
            qtePredictableScore = 0f;
        }

        // Helper: clamp list size
        public void TrimHistory()
        {
            TrimList(recentOpeners,   maxHistory);
            TrimList(recentFinishers, maxHistory);
            TrimList(recentCombos,    maxHistory);
            TrimList(recentQteResults, maxHistory);
        }

        private void TrimList<T>(List<T> list, int max)
        {
            if (list.Count <= max) return;
            int removeCount = list.Count - max;
            list.RemoveRange(0, removeCount);
        }
    }

    // ============================
    // SAFE HELPERS
    // ============================

    /// <summary>
    /// Increments usage count for a move without allocations.
    /// Called by move selection systems to track move frequency.
    /// Used by personality systems to bias against repeated moves.
    /// </summary>
    public void RegisterMoveUse(string moveName)
    {
        if (moveUsage.TryGetValue(moveName, out int count))
            moveUsage[moveName] = count + 1;
    }

    /// <summary>
    /// Resets temporary turn-based modifiers to default values.
    /// Called at start of each turn by PersonalityTurnLogic to clear last turn's state.
    /// </summary>
    public void ResetTurnModifiers()
    {
        turn.Reset();
    }

    /// <summary>
    /// Applies exponential decay to persistent biases.
    /// Called each turn to gradually reduce the influence of old decisions.
    /// Default decay = 0.95f means biases retain 95% each turn (5% reduction).
    /// Enables dynamic strategy shifts as battle progresses.
    /// </summary>
    public void DecayPersistentBiases(float decay = 0.95f)
    {
        armorPiercingBias *= decay;
        aggressiveBias *= decay;
        executeBias *= decay;
    }

    /// <summary>
    /// Draws a single card from the enemy's deck using weighted personality logic.
    /// This does NOT remove the card from the deck — enemies can draw duplicates
    /// just like the player can draw the same move multiple times.
    /// </summary>
    public EnemyMove DrawCardWeighted()
    {
        var deck = info.moves;
        if (deck == null || deck.Count == 0)
            return null;

        float totalWeight = 0f;

        // First pass: calculate total weight
        for (int i = 0; i < deck.Count; i++)
        {
            var m = deck[i];
            if (m == null) continue;

            if (m.isCounter)
                continue;

            float w = PersonalityBehavior.GetMovePreferenceScore(m, this);
            if (w < 0.1f) w = 0.1f;
            totalWeight += w;
        }

        float roll = Random.value * totalWeight;

        // Second pass: pick based on roll
        for (int i = 0; i < deck.Count; i++)
        {
            var m = deck[i];
            if (m == null) continue;

            if (m.isCounter)
                continue;

            float w = PersonalityBehavior.GetMovePreferenceScore(m, this);
            if (w < 0.1f) w = 0.1f;

            if (roll <= w)
                return m;

            roll -= w;
        }

        // Fallback (should rarely hit)
        return deck[Random.Range(0, deck.Count)];
    }

    /// <summary>
    /// Refills the enemy's hand up to (deckSize + maxComboSlots).
    /// Unused cards stay in hand forever, matching the player's system.
    /// </summary>
    public void RefillHand()
    {
        int targetSize;

        if (info.handSizeOverride > 0)
        {
            targetSize = info.handSizeOverride + (enemyLevel - 1) * info.handSizePerLevel;
        }
        else
        {
            targetSize = deckSize + maxComboSlots;
        }

        Debug.Log($"[AI HAND] Refill start → Current:{currentHand.Count}, Target:{targetSize}");
        
        while (currentHand.Count < targetSize)
        {
            var drawn = DrawCardWeighted();

            if (drawn.isCounter)
            {
                Debug.Log("[AI HAND] Skipped counter move during normal refill.");
                continue;
            }

            if (drawn != null)
            {
                currentHand.Add(drawn);
                Debug.Log($"[AI HAND] Drew: {drawn.moveName}");
            }
            else
            {
                Debug.LogWarning("[AI HAND] DrawCardWeighted() returned NULL");
                break;
            }
        }

        // Print final hand
        StringBuilder sb = new StringBuilder();
        sb.Append("[AI HAND] Final Hand → ");

        for (int i = 0; i < currentHand.Count; i++)
        {
            sb.Append(currentHand[i].moveName);
            if (i < currentHand.Count - 1)
                sb.Append(", ");
        }

        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Removes the used moves from the enemy's hand after a combo is executed.
    /// </summary>
    public void RemoveUsedCards(List<EnemyMove> usedMoves)
    {
        for (int i = 0; i < usedMoves.Count; i++)
        {
            var m = usedMoves[i];
            currentHand.Remove(m);
        }
    }
}
