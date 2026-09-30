using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Master move pool and hand management system for player combat.
/// Maintains a deck of available moves and draws a player hand each turn.
/// Implements "hand retention" mechanic: used moves are discarded, unused moves persist.
/// Hand drawing uses weighted random selection based on move slot cost.
/// Integrated with PlayerComboManager.cs for combo selection and BattleManager.cs for move availability.
/// </summary>
public class PlayerMoveDeck : MonoBehaviour
{
    [Header("Master Move Pool")]
    /// <summary>Complete list of all moves available to player in this battle; set via inspector or at battle start.</summary>
    public List<AttackMove> masterPool = new List<AttackMove>();
    public List<AttackMove> defaultMoves = new List<AttackMove>();
    public List<AttackMove> unlockedMoves = new List<AttackMove>();

    [Header("Starter Moves")]
    public AttackMove punchMove;
    public AttackMove kickMove;
    public AttackMove hookMove;

    [Header("Rank Reward Moves")]
    public List<AttackMove> rankRewardMoves = new List<AttackMove>();

    [Header("Draw Settings")]
    /// <summary>Target size of player hand; DrawHand() fills to this count each turn.</summary>
    public int handSize => PlayerStats.Instance.deckSize;
    [Tooltip("Controls how strongly slot cost reduces draw weight. 0 = uniform, 1 = 1/cost, 2 = 1/cost^2")]
    /// <summary>Exponent for move weight calculation; higher values make expensive moves rarer.</summary>
    public float slotCostWeightPower = 1f;
    

    /// <summary>Current hand of moves; persists between turns with hand retention mechanic.</summary>
    [SerializeField] private List<AttackMove> currentHand = new List<AttackMove>();
    /// <summary>Read-only access to current hand; used by UI and BattleManager.</summary>
    public IReadOnlyList<AttackMove> CurrentHand => currentHand;

    public static PlayerMoveDeck Instance { get; private set; }

    void Awake() 
    { 
        Debug.Log("[PlayerMoveDeck] Awake. masterPool count = " + masterPool.Count);
        if (Instance == null) 
        { 
            Instance = this; 
            DontDestroyOnLoad(gameObject); 
        } 
        else 
        { 
            Destroy(gameObject); 
        } 
    }

    /// <summary>
    /// Draws moves to fill empty hand slots.
    /// Uses weighted random selection based on move slotCost (expensive moves less likely).
    /// Allows duplicate draws (draw with replacement).
    /// Called by BattleManager after player turn or by hand refresh mechanics.
    /// </summary>
    public void RefillHand()
    {
        if (masterPool == null || masterPool.Count == 0)
        {
            Debug.LogWarning("[PlayerMoveDeck] masterPool empty; cannot draw.");
            return;
        }

        // Remove nulls or destroyed moves (safety)
        currentHand.RemoveAll(m => m == null);

        // Fill until hand is full
        while (currentHand.Count < handSize)
        {
            // Weighted draw
            float totalWeight = 0f;
            int poolCount = masterPool.Count;
            float[] weights = new float[poolCount];

            for (int i = 0; i < poolCount; i++)
            {
                var m = masterPool[i];
                int cost = Mathf.Max(1, m.slotCost);
                float w = slotCostWeightPower <= 0f ? 1f : 1f / Mathf.Pow(cost, slotCostWeightPower);
                weights[i] = w;
                totalWeight += w;
            }

            float r = Random.Range(0f, totalWeight);
            float acc = 0f;

            for (int i = 0; i < poolCount; i++)
            {
                acc += weights[i];
                if (r <= acc)
                {
                    currentHand.Add(masterPool[i]);
                    break;
                }
            }
        }
    }   

    /// <summary>
    /// Returns a shallow copy of the current hand.
    /// </summary>
    public List<AttackMove> GetCurrentHandCopy()
    {
        return new List<AttackMove>(currentHand);
    }

    /// <summary>
    /// Remove one instance of a used move from the current hand.
    /// Returns true if removed.
    /// </summary>
    public bool RemoveFromHand(AttackMove move)
    {
        if (move == null) return false;
        // Remove the first matching instance (preserves duplicates)
        for (int i = 0; i < currentHand.Count; i++)
        {
            if (currentHand[i] == move)
            {
                currentHand.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public List<AttackMove> GetFullDeck()
    {
        List<AttackMove> full = new List<AttackMove>();

        // Always include default moves
        full.AddRange(defaultMoves);

        // Add player-chosen moves
        full.AddRange(masterPool);

        return full;
    }

    public void RemoveUsedCards(List<AttackMove> used)
    {
        foreach (var move in used)
            RemoveFromHand(move);
    }

    // Utility methods for live changes
    public void AddToPool(AttackMove move)
    {
        if (move == null) return;
        masterPool.Add(move);
    }

    public bool RemoveFromPool(AttackMove move)
    {
        return masterPool.Remove(move);
    }
}
