using UnityEngine;

/// <summary>
/// Deprecated enum for move categorization (replaced by MoveTag enum in EnemyMove.cs).
/// Kept for legacy compatibility with existing move definitions.
/// 
/// Historical Context:
/// This was the original move tagging system. Current implementation uses MoveTag enum
/// in EnemyMove.cs which includes: None, Heavy, MultiHit, Counter, Fast, ArmorPiercing,
/// AntiTank, FakeoutHeavy, Execute.
/// 
/// Note: MoveTag in EnemyMove.cs should be used for new move definitions.
/// This enum is maintained for backward compatibility with existing assets.
/// 
/// Replacement: Use EnemyMove.MoveTag enum and EnemyMove.HasTag() method instead.
/// </summary>
public enum Movetags
{
    /// <summary>No special tag or categorization.</summary>
    None,

    /// <summary>
    /// Armor/defense piercing move (renamed to ArmorPiercing in MoveTag).
    /// Ignores or reduces target defense.
    /// </summary>
    Piercing,

    /// <summary>
    /// Multi-hit move that strikes multiple times (note: typo "Mulithit", should be "MultiHit").
    /// Replaced by MoveTag.MultiHit in current system.
    /// </summary>
    MulitHit,

    /// <summary>
    /// High damage move, typically high slot cost.
    /// Equivalent to MoveTag.Heavy in current system.
    /// </summary>
    Heavy,

    /// <summary>
    /// Low slot cost, quick execution move.
    /// Equivalent to MoveTag.Fast in current system.
    /// </summary>
    Fast,

    /// <summary>
    /// Finishing move with bonus damage against low-health targets.
    /// Equivalent to MoveTag.Execute in current system.
    /// </summary>
    Execute,

    /// <summary>
    /// Specialized move for dealing extra damage against high-defense targets.
    /// Equivalent to MoveTag.AntiTank in current system.
    /// </summary>
    AntiTank,

    /// <summary>
    /// Bonus damage against targets with both low health and low defense (glass cannons).
    /// Specialized tactical tag for specific enemy types.
    /// </summary>
    AntiGlass,

    /// <summary>
    /// Heavy move with built-in high fakeout chance.
    /// Equivalent to MoveTag.FakeoutHeavy in current system.
    /// </summary>
    FakeoutHeavy,
}
