using UnityEngine;

/// <summary>
/// Simplified combat move definition for generic battle actions.
/// Similar to AttackMove but without QTE pre-delay configuration.
/// Used as flexible template for various combat systems.
/// </summary>
[CreateAssetMenu(fileName = "CombatMove", menuName = "Battle/Combat Move")]
public class CombatMove : ScriptableObject
{
    /// <summary>Display name for move UI and logs.</summary>
    [Header("General Info")]
    public string moveName;
    /// <summary>Icon sprite for move buttons and UI displays.</summary>
    public Sprite icon;

    /// <summary>Base damage before stat scaling (attack stat multiplier applied by battle system).</summary>
    [Header("Combat Stats")]
    public int baseDamage = 10;
    /// <summary>Combo slot cost; restricts how many moves fit in single turn.</summary>
    public int slotCost = 1;
    /// <summary>Multiplier applied to damage on critical hits.</summary>
    public float critMultiplier = 1.5f;

    /// <summary>QTE type required for this move (Timing or Button).</summary>
    [Header("QTE Settings")]
    public QTEType qteType;
    /// <summary>QTE speed multiplier; controls difficulty (higher = faster/harder).</summary>
    public float qteSpeed = 1f;
    /// <summary>Array of possible button prompts if qteType is Button.</summary>
    public KeyCode[] possibleButtons; 
}

