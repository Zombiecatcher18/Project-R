using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI for displaying available moves and confirming combo selections during player turn.
/// Manages move buttons, combo slot display, and forwards selections to PlayerComboManager.
/// Called by BattleManager.StartPlayerTurn() and coordinates with PlayerComboManager for move validation.
/// </summary>
public class MoveSelectionUI : MonoBehaviour
{
    /// <summary>Maximum number of moves displayed in selection panel (pool size).</summary>
    private int MAX_MOVES => PlayerStats.Instance.deckSize;
    /// <summary>Maximum combo slots displayed (pool size for slot UI).</summary>
    private int MAX_SLOTS => PlayerStats.Instance.maxslots;

    /// <summary>Main panel containing move list and combo display.</summary>
    [Header("UI References")]
    public GameObject panel;
    /// <summary>Parent transform for move button instantiation and pooling.</summary>
    public Transform moveListParent;
    /// <summary>Parent transform for combo slot UI elements.</summary>
    public Transform comboSlotParent;
    /// <summary>Panel displaying current selected combo moves.</summary>
    public Transform currentComboPanel;
    /// <summary>Button to confirm and finalize combo selection.</summary>
    public Button confirmButton;
    /// <summary>Button to cancel and reset combo selection.</summary>
    public Button backButton;

    /// <summary>Move button prefab with text and icon components; instantiated to pool.</summary>
    [Header("Prefabs")]
    public GameObject moveButtonPrefab;
    /// <summary>Combo slot prefab with Icon and Name child transforms; shows selected move.</summary>
    public GameObject comboSlotPrefab;
    /// <summary>Sprite displayed in empty combo slots.</summary>
    public Sprite emptySlotSprite;

    /// <summary>Reference to battle manager for move execution context.</summary>
    [Header("References")]
    public BattleManager battleManager;
    /// <summary>Reference to player combo manager for move validation and application (called on confirm).</summary>
    public PlayerComboManager comboManager;

    /// <summary>Pooled move button UI elements; reused for each move display.</summary>
    private Button[] moveButtons;
    /// <summary>Text labels for move buttons; displays move name.</summary>
    private TMP_Text[] moveButtonTexts;
    /// <summary>Image components for move button icons.</summary>
    private Image[] moveButtonImages;
    /// <summary>References to AttackMove assets for clicked buttons; enables cost/effect lookup.</summary>
    private AttackMove[] moveRefs;
    /// <summary>Tracks which move buttons have been clicked (prevents double-clicking).</summary>
    private bool[] clicked;

    /// <summary>Pooled combo slot UI elements; displays selected moves in order.</summary>
    private GameObject[] slotObjects;
    /// <summary>Text labels in combo slot displays; shows move name.</summary>
    private TMP_Text[] slotTexts;
    /// <summary>Icon images in combo slot displays; shows move sprite.</summary>
    private Image[] slotIcons;

    /// <summary>
    /// Initializes UI pools and button listeners on scene load.
    /// Creates pooled move buttons and combo slot displays, registers button callbacks.
    /// Hides panel initially until Open() is called by BattleManager.
    /// OPTIMIZATION: Caches PlayerComboManager once in Awake to avoid per-use FindObjectOfType calls.
    /// </summary>
    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (comboManager == null)
            comboManager = FindObjectOfType<PlayerComboManager>();
    }

    private void Start()
    {
        // Ensure PlayerStats.Instance exists
        if (PlayerStats.Instance == null)
        {
            Debug.LogError("PlayerStats.Instance is NULL in MoveSelectionUI.Start!");
            return;
        }

        // Allocate arrays NOW that PlayerStats.Instance exists
        moveButtons = new Button[PlayerStats.Instance.deckSize];
        moveButtonTexts = new TMP_Text[PlayerStats.Instance.deckSize];
        moveButtonImages = new Image[PlayerStats.Instance.deckSize];
        moveRefs = new AttackMove[PlayerStats.Instance.deckSize];
        clicked = new bool[PlayerStats.Instance.deckSize];

        slotObjects = new GameObject[PlayerStats.Instance.maxslots];
        slotTexts = new TMP_Text[PlayerStats.Instance.maxslots];
        slotIcons = new Image[PlayerStats.Instance.maxslots];

        BuildMoveButtonPool();
        BuildSlotPool();

        confirmButton.interactable = false;
        confirmButton.onClick.AddListener(OnConfirmClicked);
        backButton.onClick.AddListener(OnBackClicked);
    }

    // ===== POOL CREATION =====

    /// <summary>
    /// Creates pooled move button UI elements and registers click handlers.
    /// Each button captures its index safely via closure to identify which move was clicked.
    /// Called by Awake() during initialization.
    /// </summary>
    private void BuildMoveButtonPool()
    {
        for (int i = 0; i < PlayerStats.Instance.deckSize; i++)
        {
            GameObject go = Instantiate(moveButtonPrefab, moveListParent);
            go.SetActive(false);

            moveButtons[i] = go.GetComponent<Button>();
            moveButtonTexts[i] = go.GetComponentInChildren<TMP_Text>();
            moveButtonImages[i] = go.GetComponent<Image>();

            int index = i; // Capture index safely in closure
            moveButtons[i].onClick.AddListener(() => OnMoveButtonClicked(index));
        }
    }

    /// <summary>
    /// Creates pooled combo slot displays for showing selected moves.
    /// Finds Icon and Name child transforms and caches references for efficiency.
    /// Validates prefab structure and logs errors if children are missing.
    /// Called by Awake() during initialization.
    /// </summary>
    private void BuildSlotPool()
    {
        for (int i = 0; i < PlayerStats.Instance.maxslots; i++)
        {
            GameObject slot = Instantiate(comboSlotPrefab, comboSlotParent);
            slotObjects[i] = slot;

            Transform iconT = slot.transform.Find("Icon");
            Transform nameT = slot.transform.Find("Name");

            if (iconT == null)
                Debug.LogError($"ComboSlotPrefab missing 'Icon' child at slot {i}");
            if (nameT == null)
                Debug.LogError($"ComboSlotPrefab missing 'Name' child at slot {i}");

            slotIcons[i] = iconT?.GetComponent<Image>();
            slotTexts[i] = nameT?.GetComponent<TextMeshProUGUI>();

            // Validate prefab structure for critical components
            if (slotIcons[i] == null)
                Debug.LogError($"ComboSlotPrefab 'Icon' child missing Image component at slot {i}");
            if (slotTexts[i] == null)
                Debug.LogError($"ComboSlotPrefab 'Name' child missing TMP_Text component at slot {i}");

            slotIcons[i].sprite = emptySlotSprite;
            slotTexts[i].text = "";
        }
    }

    private Color GetSynergyColor(AttackMove prev, AttackMove curr)
    {
        if (prev == null || curr == null)
            return Color.white; // no previous move = neutral

        if (curr.goodSynergyMoves.Contains(prev))
            return Color.green;

        if (curr.badSynergyMoves.Contains(prev))
            return Color.red;

        return Color.yellow; // neutral
    }

    private void ApplyInitialSlotRestrictions()
    {
        int maxSlots = PlayerStats.Instance.maxslots;

        for (int i = 0; i < MAX_MOVES; i++)
        {
            var move = moveRefs[i];

            if (move == null)
            {
                moveButtons[i].interactable = false;
                continue;
            }

            // If the move costs more than the player's total slots, disable it immediately
            if (move.slotCost > maxSlots)
            {
                moveButtons[i].interactable = false;
                moveButtonImages[i].color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                moveButtonTexts[i].color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            }
            else
            {
                // Move fits — show normally
                moveButtons[i].interactable = true;
                moveButtonImages[i].color = Color.white;
                moveButtonTexts[i].color = Color.white;
            }
        }
    }

    // ===== OPEN UI =====

    /// <summary>
    /// Opens move selection UI with available moves from player hand.
    /// Displays moves as clickable buttons and resets combo slots to empty.
    /// Called by BattleManager.StartPlayerTurn() with hand list from PlayerMoveDeck.
    /// Coordinates with PlayerComboManager for move validation.
    /// </summary>
    public void Open(List<AttackMove> moves)
    {
        panel.SetActive(true);

        // Reset clicked state to allow button re-pressing
        for (int i = 0; i < PlayerStats.Instance.deckSize; i++)
            clicked[i] = false;

        // Populate pooled move buttons with hand contents
        for (int i = 0; i < PlayerStats.Instance.deckSize; i++)
        {
            if (i < moves.Count)
            {
                // Validate move reference (catch null moves from deck)
                if (moves[i] == null)
                {
                    Debug.LogError($"[MoveSelectionUI] Move at index {i} is NULL in the hand list!");
                    moveRefs[i] = null;
                    moveButtonTexts[i].text = "NULL MOVE";
                    moveButtons[i].interactable = false;
                    moveButtons[i].gameObject.SetActive(true);
                    continue;
                }

                // Display move in button
                moveRefs[i] = moves[i];
                moveButtonTexts[i].text = moves[i].moveName;
                moveButtons[i].interactable = true;
                moveButtonImages[i].color = Color.white;
                moveButtonTexts[i].color = Color.white;
                moveButtons[i].gameObject.SetActive(true);
            }
            else
            {
                // Hide unused button slots
                moveRefs[i] = null;
                moveButtons[i].gameObject.SetActive(false);
            }
        }

        ApplyInitialSlotRestrictions();

        // Clear combo slot displays
        for (int i = 0; i < PlayerStats.Instance.maxslots; i++)
        {
            slotIcons[i].sprite = emptySlotSprite;
            slotTexts[i].text = "";
        }

        RefreshMoveButtons(PlayerStats.Instance.maxslots);
        confirmButton.interactable = false;
    }

    // ===== BUTTON CLICK HANDLERS =====

    /// <summary>
    /// Handles move button click; adds move to combo and disables button.
    /// Called by move button OnClick listener via closure with button index.
    /// Prevents double-clicking same move by disabling button and greying out text.
    /// Forwards move to PlayerComboManager.AddMove() for validation and slot management.
    /// </summary>
    private void OnMoveButtonClicked(int index)
    {
        if (clicked[index]) return;

        // Validate move reference
        if (moveRefs[index] == null)
        {
            Debug.LogError($"[MoveSelectionUI] moveRefs[{index}] is NULL when clicked!");
            return;
        }

        // Mark button as clicked to prevent re-clicking
        clicked[index] = true;

        // Visually disable button (grey out) to show it's been used
        moveButtons[index].interactable = false;
        moveButtonImages[index].color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        moveButtonTexts[index].color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        // Add move to combo via combo manager (called by PlayerComboManager.AddMove() for validation)
        comboManager.AddMove(moveRefs[index]);
    }

    // ===== BACK BUTTON =====

    /// <summary>
    /// Cancels move selection and resets combo.
    /// Called by back button OnClick listener.
    /// Clears all selected moves and resets UI to initial state for re-selection.
    /// </summary>
    private void OnBackClicked()
    {
        // Remove last selected move via combo manager and get reference
        AttackMove removed = comboManager.RemoveLastMove();
        if (removed == null) return;

        // Re-enable button corresponding to removed move
        for (int i = MAX_MOVES - 1; i >= 0; i--)
        {
            if (moveRefs[i] == removed && clicked[i])
            {
                // Unmark as clicked and restore visual appearance
                clicked[i] = false;
                moveButtons[i].interactable = true;
                moveButtonImages[i].color = Color.white;
                moveButtonTexts[i].color = Color.white;
                break;
            }
        }
    }

    // ===== UPDATE DISPLAY =====

    /// <summary>
    /// Updates combo slot display with selected moves.
    /// Called by PlayerComboManager when moves are added/removed.
    /// Shows selected moves in order and enables confirm button when combo is non-empty.
    /// </summary>
    public void UpdateSelected(List<AttackMove> selected)
    {
        // Clear all slots first
        for (int i = 0; i < PlayerStats.Instance.maxslots; i++)
        {
            slotIcons[i].sprite = emptySlotSprite;
            slotTexts[i].text = "";
        }

        int slotIndex = 0;

        foreach (var move in selected)
        {
            int cost = Mathf.Max(1, move.slotCost);

            for (int c = 0; c < cost; c++)
            {
                if (slotIndex >= PlayerStats.Instance.maxslots)
                    break;

                slotIcons[slotIndex].sprite = emptySlotSprite;
                slotTexts[slotIndex].text = move.moveName;

                slotIndex++;
            }
        }

        confirmButton.interactable = selected.Count > 0;
    }

    /// <summary>
    /// Updates move button interactability based on remaining combo slots.
    /// Called by PlayerComboManager.AddMove() to reflect slot constraints.
    /// Disables buttons for moves that would exceed slot budget.
    /// </summary>
    public void RefreshMoveButtons(int slotsRemaining)
    {
        for (int i = 0; i < MAX_MOVES; i++)
        {
            var move = moveRefs[i];

            // Invalid/null move
            if (move == null)
            {
                moveButtons[i].interactable = false;
                continue;
            }

            // Already clicked move stays disabled
            if (clicked[i])
            {
                moveButtons[i].interactable = false;
                continue;
            }

            // Check if move fits remaining slots (move.slotCost from AttackMove)
            if (move.slotCost > slotsRemaining)
            {
                // Move is too expensive for remaining slots - disable with visual indication
                moveButtons[i].interactable = false;
                moveButtonImages[i].color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                moveButtonTexts[i].color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            }
            else
            {
                // Determine synergy color based on last selected move
                AttackMove prev = null;
                if (comboManager.currentCombo.chosenMoves.Count > 0)
                prev = comboManager.currentCombo.chosenMoves[^1];

                Color synergyColor = GetSynergyColor(prev, move);

                // Move fits - enable and color based on synergy
                moveButtons[i].interactable = true;
                moveButtonImages[i].color = synergyColor;
                moveButtonTexts[i].color = synergyColor;
            }
        }
    }

    // ===== CONFIRM =====

    /// <summary>
    /// Finalizes combo selection and closes UI.
    /// Called by confirm button OnClick listener.
    /// Forwards finalized combo to BattleManager for battle execution.
    /// References PlayerComboManager.ConfirmCombo() for battle state preparation.
    /// </summary>
    private void OnConfirmClicked()
    {
        comboManager.ConfirmCombo();
        Close();
    }

    /// <summary>
    /// Hides move selection panel.
    /// Called by OnConfirmClicked() after combo confirmation.
    /// </summary>
    public void Close()
    {
        panel.SetActive(false);
    }

    private void OnDisable()
    {
        Debug.LogWarning("[MoveSelectionUI] DISABLED at runtime!");
    }

    private void OnEnable()
    {
        Debug.Log("[MoveSelectionUI] ENABLED at runtime!");
    }
}
