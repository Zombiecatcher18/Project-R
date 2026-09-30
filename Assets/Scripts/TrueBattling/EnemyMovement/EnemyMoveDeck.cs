using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Serializable container for enemy moves available during battle.
/// Stores list of available moves and combo slot capacity.
/// Attached to EnemyInfo scriptable objects to define enemy combat repertoire.
/// 
/// Usage:
/// - EnemyInfo.moves references EnemyMoveDeck for move selection
/// - AIComboBuilder reads moves list to build multi-move combos
/// - maxSlots determines total combo slots available (e.g., 3 slots = up to 3-move combo)
/// 
/// Slots System:
/// - Each EnemyMove has a slotCost (typically 1-2)
/// - Total combo cost cannot exceed maxSlots
/// - Example: maxSlots=3 with moves costing [1,1,2] can do 1-move, 2-move, or single 2-move combos
/// 
/// Serialization: [System.Serializable] enables inspector editing on EnemyInfo assets
/// </summary>
[System.Serializable]
public class EnemyMoveDeck
{
    /// <summary>
    /// List of available moves for this enemy type.
    /// Used by AIComboBuilder to generate valid combos and EnemyAttackController to execute.
    /// Initialized with capacity 0 (editor will populate).
    /// </summary>
    public List<EnemyMove> moves = new List<EnemyMove>(0);

    /// <summary>
    /// Maximum combo slots available for this enemy.
    /// Determines how many moves (by slot cost) can be combined in single turn.
    /// Scales with level in EnemyPartySpawner: maxComboSlots += (level - baseLevel) * comboSlotsPerLevel
    /// Typical values: 2-4 slots (small combos to prevent overpowering)
    /// </summary>
    public int maxSlots = 3;
}

