using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Container for enemy party composition and configuration (currently unused/commented).
/// 
/// Original Purpose:
/// Held immutable list of EnemyInfo objects for an encounter.
/// Passed via BattleData.enemyParty to battle scene for initialization.
/// Provided simple list wrapper with constructor overloads for flexibility.
/// 
/// Current Status: Deprecated
/// Reason: EnemyEncounter now passes enemyGroup array directly to BattleData.
/// TODO: Re-integrate if party serialization/persistence needed in future.
/// 
/// Historical Usage:
/// var party = new EnemyParty(new List<EnemyInfo> { goblin1, goblin2, orc1 });
/// BattleData.enemyParty = party;  // Pass to battle scene
/// 
/// Future Enhancements:
/// - Could serialize party composition to JSON for save persistence
/// - Could add methods for party-wide behavior/buff application
/// - Could track party affinity/synergy bonuses
/// </summary>

/* Deprecated container - see comment block above for historical context
public class EnemyParty
{
    /// <summary>
    /// List of EnemyInfo objects comprising this party encounter.
    /// Order matters: determines turn order in battle (first = first to act).
    /// </summary>
    public List<EnemyInfo> enemies = new List<EnemyInfo>();

    /// <summary>
    /// Default constructor for serialization compatibility.
    /// </summary>
    public EnemyParty() { }

    /// <summary>
    /// Constructor: Initialize party with enemy list.
    /// </summary>
    public EnemyParty(List<EnemyInfo> infos)
    {
        enemies = infos;
    }
} */
