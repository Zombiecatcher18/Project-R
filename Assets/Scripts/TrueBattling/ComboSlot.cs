using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for displaying a single combo slot move.
/// Shows move icon and name; supports both filled and empty states.
/// Used in MoveSelectionUI combo display and battle UI slot presentations.
/// </summary>
public class ComboSlot : MonoBehaviour
{
    /// <summary>Image component for displaying move icon sprite.</summary>
    public Image iconImage;
    /// <summary>Text component for displaying move name.</summary>
    public TMP_Text nameText;

    /// <summary>Reference to move stored in this slot; null if empty.</summary>
    [HideInInspector] public AttackMove storedMove;

    /// <summary>Sprite displayed when slot is empty (typically blank/greyed icon).</summary>
    public Sprite emptySprite;

    /// <summary>
    /// Sets slot to empty state.
    /// Called when combo is cleared or slot is deselected.
    /// Displays empty sprite and clears text.
    /// </summary>
    public void SetEmpty()
    {
        storedMove = null;
        if (iconImage != null) iconImage.sprite = emptySprite;
        if (nameText != null) nameText.text = "";
    }

    /// <summary>
    /// Sets slot to display given move.
    /// Called when move is added to combo.
    /// Updates icon and name text from move data.
    /// </summary>
    public void SetMove(AttackMove move)
    {
        storedMove = move;
        if (iconImage != null) iconImage.sprite = move.icon;
        if (nameText != null) nameText.text = move.moveName;
    }
}
