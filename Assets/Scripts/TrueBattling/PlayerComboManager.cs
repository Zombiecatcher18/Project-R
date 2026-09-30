using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates player combo selection UI and move validation.
/// Manages move hand drawing and slot cost calculations for player attacks.
/// Coordinates with PlayerMoveDeck for hand management and MoveSelectionUI for player interaction.
/// Sends final combo to BattleManager.OnMovesSelected() after selection completes.
/// </summary>
public class PlayerComboManager : MonoBehaviour
{
    [Header("Moves (fallback)")]
    /// <summary>Fallback move list if PlayerMoveDeck is not available; used for move selection.</summary>
    public List<AttackMove> availableMoves;

    [Header("UI")]
    /// <summary>UI component for displaying move selections to player; references MoveSelectionUI.cs.</summary>
    public MoveSelectionUI moveSelectionUI;

    [Header("Deck")]
    /// <summary>Move deck system for hand drawing and management; references PlayerMoveDeck.cs.</summary>
    public PlayerMoveDeck moveDeck;

    [Header("Combo Settings")]
    /// <summary>Maximum combo slot units available (sum of selected moves' slotCosts).</summary>
    /// No longer needed since max slots is now derived from PlayerStats
    //public int maxComboSlots = 4;
    /// <summary>Maximum number of individual moves in a combo.</summary>
    public int maxComboSize;
    /// <summary>Current combo being built; passed to BattleManager on completion.</summary>
    public PlayerCombo currentCombo = new PlayerCombo();

    /// <summary>Remaining slot units available for current combo.</summary>
    private int slotsRemaining;

    /// <summary>Cached BattleManager reference to avoid per-frame FindObjectOfType calls.</summary>
    private BattleManager battleManager;

    /// <summary>
    /// Caches BattleManager reference and validates combo setup.
    /// </summary>
    private void Awake()
    {
        if (currentCombo == null)
            Debug.LogError("PlayerComboManager: currentCombo reference not assigned!");

        battleManager = FindObjectOfType<BattleManager>();
        if (battleManager == null)
            Debug.LogError("PlayerComboManager: Could not find BattleManager in scene!");
    }

    // ===================================================
    // Combo Selection Initiation
    // ===================================================
    
    /// <summary>
    /// Initiates combo selection UI.
    /// Draws player hand from deck and displays MoveSelectionUI for move selection.
    /// References PlayerMoveDeck.DrawHand() and MoveSelectionUI.Open().
    /// </summary>
    public void StartComboSelection()
    {
        if (currentCombo == null)
        {
            Debug.LogError("No PlayerCombo assigned!");
            return;
        }

        currentCombo.chosenMoves.Clear();
        slotsRemaining = PlayerStats.Instance.maxslots;

        // Draw player hand from deck or use fallback
        List<AttackMove> hand;
        if (moveDeck != null)
        {
            moveDeck.RefillHand();
            hand = moveDeck.GetCurrentHandCopy();
        }
        else
        {
            hand = availableMoves;
        }

        // Display move selection UI
        if (moveSelectionUI != null)
            moveSelectionUI.Open(hand);
    }

    // ===================================================
    // Move Addition & Validation
    // ===================================================
    
    /// <summary>
    /// Adds a move to the current combo if slot constraints allow.
    /// Validates slotCost fits remaining slots and combo size limit.
    /// Updates slotsRemaining and notifies MoveSelectionUI.
    /// Called by MoveSelectionUI when player clicks a move.
    /// </summary>
    public void AddMove(AttackMove move)
    {
        if (move == null) return;

        if (move == null)
        {
            Debug.LogError("[AddMove] Tried to add a NULL move!");
            return;
        }
        else
        {
            Debug.Log($"[AddMove] Adding move: {move.moveName}");
        }

        /* if (currentCombo.chosenMoves.Count >= maxComboSize)
        {
            Debug.Log("[PlayerComboManager] Combo full.");
            return;
        } */

        if (move.slotCost > slotsRemaining)
        {
            Debug.Log("[PlayerComboManager] Move too expensive.");
            return;
        }

        currentCombo.chosenMoves.Add(move);
        slotsRemaining -= Mathf.Max(1, move.slotCost);
        moveSelectionUI.RefreshMoveButtons(slotsRemaining);

        if (moveSelectionUI != null)
            moveSelectionUI.UpdateSelected(currentCombo.chosenMoves);
    }

    // ---------------------------------------------------------
    // REMOVE LAST MOVE
    // ---------------------------------------------------------
    public AttackMove RemoveLastMove()
    {
        if (currentCombo == null || currentCombo.chosenMoves.Count == 0)
            return null;

        int lastIndex = currentCombo.chosenMoves.Count - 1;
        AttackMove move = currentCombo.chosenMoves[lastIndex];
        currentCombo.chosenMoves.RemoveAt(lastIndex);

        slotsRemaining += Mathf.Max(1, move.slotCost);
        moveSelectionUI.RefreshMoveButtons(slotsRemaining);

        if (moveSelectionUI != null)
            moveSelectionUI.UpdateSelected(currentCombo.chosenMoves);

        return move;
    }

    // ---------------------------------------------------------
    // CONFIRM COMBO
    // ---------------------------------------------------------
    public void ConfirmCombo()
    {
        if (moveSelectionUI != null)
            moveSelectionUI.Close();

        if (currentCombo == null || currentCombo.chosenMoves.Count == 0 || currentCombo.chosenMoves[0] == null)
        {
            Debug.LogWarning("[PlayerComboManager] Cannot confirm combo — no valid moves selected.");
            return;
        }

        // NOW remove used cards
        moveDeck.RemoveUsedCards(currentCombo.chosenMoves);

        if (battleManager != null)
        {
            Debug.Log($"[PlayerComboManager] Confirming combo with {currentCombo.chosenMoves.Count} moves.");

            // IMPORTANT: pass a copy, not the original list
            battleManager.OnMovesSelected(new List<AttackMove>(currentCombo.chosenMoves));
        }
        else
        {
            Debug.LogError("[PlayerComboManager] BattleManager not found!");
        }
    }
}
