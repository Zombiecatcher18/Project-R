using UnityEngine;

/// <summary>
/// Scriptable Object defining a player move's mechanics and QTE requirements in battle.
/// 
/// PURPOSE:
/// Stores move data (damage, name, QTE type) as a reusable asset. Allows designers to create,
/// balance, and modify moves without touching combat code. Enables quick iteration on move mechanics.
/// 
/// HOW IT WORKS:
/// 1. BASIC MOVE DATA: moveName and baseDamage define the move's identity and damage output
/// 2. QTE SYSTEM: qteType determines the input challenge (button press, timing, mashing, etc.)
/// 3. QTE PARAMETERS: qteSpeed affects difficulty of the QTE minigame, critMultiplier rewards perfect QTE execution
/// 4. BUTTON INPUT: possibleButtons lists keyboard inputs for Button Prompt QTE type
/// 
/// WHAT IT AFFECTS:
/// - PlayerCombo.cs: References move data to execute attacks and calculate damage
/// - QTESystem.cs: Uses qteType, qteSpeed, and possibleButtons to spawn and evaluate QTE challenges
/// - PlayerTurnLogic.cs: Uses baseDamage * critMultiplier (if QTE succeeds) to determine final damage dealt
/// - UI System: Displays moveName and updates move selection UI based on available player moves
/// </summary>
[CreateAssetMenu(fileName = "PlayerMove", menuName = "Battle/Player Move")]
public class PlayerMove : ScriptableObject
{
    [Tooltip("WHAT: Display name of player attack move. HOW: Shown in move selection UI during battle. AFFECTS: Player readability and move identification.")]
    public string moveName;
    
    [Tooltip("WHAT: Base damage before scaling by player stats. HOW: Multiplied by player ATK stat (×0.04) in damage calculation. AFFECTS: Overall attack output and combat balance.")]
    public int baseDamage;

    [Header("QTE Settings")]
    [Tooltip("WHAT: Type of QTE challenge for this move (Timing or Button). HOW: Determines which minigame executes during attack. AFFECTS: Player skill type required.")]
    public QTEType qteType;
    
    [Tooltip("WHAT: Speed/difficulty multiplier of QTE window. HOW: Higher=faster/harder, lower=slower/easier. AFFECTS: Attack difficulty and crit chance potential.")]
    public float qteSpeed = 1f;
    
    [Tooltip("WHAT: Damage multiplier if player gets perfect QTE. HOW: Multiplies final damage if crit lands. AFFECTS: Risk-reward incentive for perfect execution.")]
    public float critMultiplier = 1.5f;

    [Header("For Button Prompt QTE")]
    [Tooltip("WHAT: Array of possible button inputs for QTE challenge. HOW: One randomly selected per QTE instance. AFFECTS: Player predictability and skill requirement.")]
    public KeyCode[] possibleButtons; // (Example: space, J, K)
}

