using UnityEngine;

/// <summary>
/// Alternative enemy data container component (simplified variant of EnemyInfo).
/// Provides flat MonoBehaviour-based stats storage instead of scriptable object approach.
/// 
/// Design Rationale:
/// - Simpler setup than EnemyInfo (no cloning, no level scaling references)
/// - Directly attached to enemy GameObjects for instance-specific data
/// - Less hierarchical than EnemyInfo → EnemyRuntimeData separation
/// 
/// Usage Context:
/// Intended for scene-placed enemies with fixed stats (no dynamic scaling).
/// Compare with EnemyInfo (scriptable object template) and EnemyRuntimeData (battle instance).
/// 
/// Current Status: Appears to be legacy/experimental variant.
/// Active system uses EnemyInfo → EnemyRuntimeData pipeline.
/// </summary>
public class EnemyData_New : MonoBehaviour
{
    // ===== IDENTITY =====
    /// <summary>
    /// Unique identifier for this enemy (e.g., "Goblin_001", "Boss_FireDragon").
    /// Used for tracking, logging, and potential save persistence.
    /// </summary>
    public string enemyID;

    // ===== STATS =====
    /// <summary>
    /// Display name for enemy (e.g., "Lesser Goblin", "Fire Elemental").
    /// Shown in UI and combat text.
    /// </summary>
    [Header("Basic Stats")]
    public string enemyName;

    /// <summary>
    /// Maximum health points for this enemy.
    /// Static value (no level scaling applied).
    /// </summary>
    public int maxHealth;

    /// <summary>
    /// Current health points (runtime value, decreases when damaged).
    /// Reset to maxHealth on battle start.
    /// </summary>
    public int currentHealth;

    // ===== VISUALS =====
    /// <summary>
    /// Arena/battle visual theme color (tints background or UI elements).
    /// Used for visual variety in different encounter areas.
    /// </summary>
    [Header("Visuals")]
    public Color capsuleColor;

    /// <summary>
    /// Enemy sprite for battle UI display (character portrait or avatar).
    /// Shown in battle UI elements and party display.
    /// </summary>
    public Sprite enemySprite;

    // ===== COMBAT CONFIGURATION =====
    /// <summary>
    /// Array of available attack moves for this enemy.
    /// Used by combat system for move selection and combo building.
    /// </summary>
    [Header("Moves")]
    public EnemyMove[] moves;
}
