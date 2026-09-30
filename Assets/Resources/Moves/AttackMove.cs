using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Scriptable object defining player attack move properties and QTE configuration.
/// Used by PlayerMoveDeck for hand draws and combo building, executed via BattleManager.
/// Stores damage, cost, and QTE parameters for battle mechanics.
/// </summary>
[CreateAssetMenu(menuName = "Battle/Attack Move")]
public class AttackMove : ScriptableObject
{
    /// <summary>Display name for UI and logs.</summary>
    [Header("General Info")]
    public string moveName;
    /// <summary>Icon sprite for move buttons and combo displays.</summary>
    public Sprite icon;
    public string moveID;

    // ----------------------------- 
    // MOVE TYPE 
    // ----------------------------- 
    public enum MoveType 
    { 
        Attack, 
        Heal, 
        BuffAttack, 
        BuffDefense, 
        DebuffEnemy, 
        Setup, // boosts next move 
        Finisher, // bonus if last in combo 
        Utility // misc effects 
    }

    [Header("Move Type")]
    public MoveType moveType = MoveType.Attack;

    /// <summary>Base damage before player attack stat scaling (multiplied by 0.04f in BattleManager).</summary>
    [Header("Combat Stats")]
    public int baseDamage = 10;
    /// <summary>Combo slot cost; restricts how many moves fit in single turn.</summary>
    public int slotCost = 1;
    /// <summary>Multiplier applied to damage on critical hits; typical range 1.2-2.0.</summary>
    public float critMultiplier = 1.5f;
    // Used for healing, buffing, debuffing, etc.
    public int effectPower = 0;

    // ----------------------------- 
    // SYNERGY SYSTEM 
    // ----------------------------- 
    [Header("Synergy (Good Combos)")]
    [Tooltip("Moves that combo WELL when used BEFORE this move.")]
    public List<AttackMove> goodSynergyMoves = new List<AttackMove>();

    [Header("Anti-Synergy (Bad Combos)")]
    [Tooltip("Moves that combo POORLY when used BEFORE this move.")]
    public List<AttackMove> badSynergyMoves = new List<AttackMove>();

    /// <summary>QTE type for this move (Timing or Button).</summary>
    [Header("QTE Settings")]
    public QTEType qteType;

    /// <summary>Button QTE pre-delay minimum in seconds.</summary>
    [Header("Player Button QTE Settings")]
    public float buttonPreDelayMin = 0.3f;
    /// <summary>Button QTE pre-delay maximum in seconds; random range selected per QTE.</summary>
    public float buttonPreDelayMax = 0.8f;

    /// <summary>QTE pre-delay in seconds (used by QTEManager for all QTE types).</summary>
    [Header("Unified QTE Timing")]
    public float preDelay = 0.3f;

    /// <summary>QTE speed multiplier; controls visual animation and difficulty (higher = faster/harder).</summary>
    [Tooltip("Higher = faster QTE.")]
    public float qteSpeed = 1f;

    /// <summary>Array of possible button prompts for Button-type QTEs; randomly selected by QTEManager.</summary>
    [Tooltip("Possible buttons the QTE may choose from.")]
    public KeyCode[] possibleButtons;


    /// <summary>
    /// Selects random button from possibleButtons array.
    /// Called by ButtonPromptQTE for button selection.
    /// Returns Space key if array is empty or null.
    /// </summary>
    public KeyCode GetRandomButton()
    {
        if (possibleButtons == null || possibleButtons.Length == 0)
            return KeyCode.Space;

        return possibleButtons[Random.Range(0, possibleButtons.Length)];
    }

    /// <summary>
    /// Gets random pre-delay for button QTE within buttonPreDelayMin/Max range.
    /// Called by ButtonPromptQTE to set variable delay before prompt appears.
    /// </summary>
    public float GetButtonPreDelay()
    {
        return Random.Range(buttonPreDelayMin, buttonPreDelayMax);
    }

    /// <summary>
    /// Calculates total time limit for QTE completion based on qteSpeed.
    /// Formula: 1.2 / qteSpeed, clamped to 0.25-3 second range.
    /// Called by QTEManager to set difficulty curve for QTE windows.
    /// </summary>
    public float GetTimeLimit()
    {
        return Mathf.Clamp(1.2f / Mathf.Max(0.01f, qteSpeed), 0.25f, 3f);
    }
}
