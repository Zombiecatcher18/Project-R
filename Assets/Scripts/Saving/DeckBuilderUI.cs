using UnityEngine;
using System.Collections;
using Maincharacter.PlayerController;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DeckBuilderUI : MonoBehaviour
{
    public static DeckBuilderUI Instance;

    [Header("Deck Data")]
    public PlayerMoveDeck moveDeck;

    [Header("UI Root")]
    public GameObject deckPanel;   // <-- The actual UI Canvas

    [Header("UI Containers")]
    public Transform unlockedListContainer;
    public Transform activeDeckContainer;

    [Header("Deck Stats")]
    public TMP_Text deckSizeText;
    public TMP_Text slotCountText;

    [Header("Move Info Panel")]
    public GameObject moveInfoPanel;
    public TMP_Text infoNameText;
    public TMP_Text infoDamageText;
    public TMP_Text infoSlotCostText;
    public TMP_Text infoCritText;
    public TMP_Text infoQTETypeText;
    public TMP_Text infoPreDelayText;
    public TMP_Text infoSpeedText;
    public TMP_Text infoButtonsText;
    public Image infoIcon;
    public TMP_Text infoMoveTypeText;
    public TMP_Text infoEffectPowerText;
    public TMP_Text infoGoodSynergyText;
    public TMP_Text infoBadSynergyText;
    public TMP_Text infoDescriptionText;


    [Header("Prefabs")]
    public GameObject moveEntryPrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        deckPanel.SetActive(false);   // Hide UI, keep controller alive
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        HookInput();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (GameInputManager.Instance != null)
            GameInputManager.Instance.OnDeckEvent -= OnDeckInput;
    }

    private void OnDestroy()
    {
        if (GameInputManager.Instance != null)
            GameInputManager.Instance.OnDeckEvent -= OnDeckInput;
    }

    private void HookInput()
    {
        if (GameInputManager.Instance == null)
        {
            Debug.LogWarning("DeckBuilderUI: GameInputManager.Instance is null, cannot hook deck input yet.");
            return;
        }

        GameInputManager.Instance.OnDeckEvent -= OnDeckInput;
        GameInputManager.Instance.OnDeckEvent += OnDeckInput;

        Debug.Log("DeckBuilderUI: Hooked OnDeckEvent.");
    }

    // ============================================================
    // INPUT ENTRY POINT
    // ============================================================

    private void OnDeckInput()
    {
        string scene = SceneManager.GetActiveScene().name;

        if (scene == "Main Menu" || scene == "BattleScene")
            return;

        // BLOCK if another UI is open
        if (!deckPanel.activeSelf && GlobalUIManager.Instance.OtherUIOpen("Deck"))
        {
            Debug.Log($"[UI BLOCKED] Deck Builder blocked because {GlobalUIManager.Instance.GetOpenUIName()} is open.");
            return;
        }
       
        // Toggle normally
        if (deckPanel.activeSelf)
            CloseDeckUI();
        else
            OpenDeckUI();
    }

    // ============================================================
    // OPEN / CLOSE
    // ============================================================

    private void OpenDeckUI()
    {
        Debug.Log("DeckBuilderUI: OpenDeckUI called.");

        GlobalUIManager.Instance?.CloseAll();

        deckPanel.SetActive(true);

        CursorManager.Show();
        PauseGame();
        SetPlayerMovement(false);

        StartCoroutine(InitializeUI());
    }

    public void CloseDeckUI()
    {
        Debug.Log("DeckBuilderUI: CloseDeckUI called.");

        deckPanel.SetActive(false);

        CursorManager.Hide();
        ResumeGame();
        SetPlayerMovement(true);
    }

    public void SaveDeck()
    {
        SaveData data = SaveManager.BuildSaveData();
        SaveManager.Save(0, data);
        CloseDeckUI();
    }

    // ============================================================
    // INITIALIZATION
    // ============================================================

    private IEnumerator InitializeUI()
    {
        CursorManager.Show();

        yield return new WaitUntil(() => PlayerMoveDeck.Instance != null);

        moveDeck = PlayerMoveDeck.Instance;
        Refresh();
    }

    // ============================================================
    // REFRESH UI
    // ============================================================

    public void Refresh()
    {
        if (moveDeck == null)
        {
            Debug.LogWarning("DeckBuilderUI: moveDeck is null in Refresh.");
            return;
        }

        int currentDeckCount = moveDeck.masterPool.Count;
        int maxDeckSize = PlayerStats.Instance.deckSize;

        deckSizeText.text = $"Deck: {currentDeckCount}/{maxDeckSize}";
        slotCountText.text = $"Slots: {PlayerStats.Instance.maxslots}";

        foreach (Transform t in unlockedListContainer) Destroy(t.gameObject);
        foreach (Transform t in activeDeckContainer) Destroy(t.gameObject);

        foreach (var move in moveDeck.unlockedMoves)
        {
            var go = Instantiate(moveEntryPrefab, unlockedListContainer);
            var ui = go.GetComponent<MoveEntryUI>();
            ui.Setup(move, "Add", () => AddToDeck(move), this);

            bool deckFull = moveDeck.masterPool.Count >= PlayerStats.Instance.deckSize;
            bool tooExpensive = move.slotCost > PlayerStats.Instance.maxslots;

            ui.actionButton.interactable = !(deckFull || tooExpensive);

            if (tooExpensive)
                ui.SetButtonColor(Color.red);
            else if (deckFull)
                ui.SetButtonColor(new Color(1f, 0.5f, 0f));
            else
                ui.SetButtonColor(Color.white);
        }

        foreach (var move in moveDeck.masterPool)
        {
            var go = Instantiate(moveEntryPrefab, activeDeckContainer);
            var ui = go.GetComponent<MoveEntryUI>();
            ui.Setup(move, "Remove", () => RemoveFromDeck(move), this);

            ui.actionButton.interactable = moveDeck.masterPool.Count > 1;
        }
    }

    // ============================================================
    // ADD / REMOVE MOVES
    // ============================================================

    private void AddToDeck(AttackMove move)
    {
        int maxDeckSize = PlayerStats.Instance.deckSize;
        int maxSlots = PlayerStats.Instance.maxslots;

        if (moveDeck.masterPool.Count >= maxDeckSize)
            return;

        if (move.slotCost > maxSlots)
            return;

        if (!moveDeck.masterPool.Contains(move))
        {
            moveDeck.masterPool.Add(move);
            moveDeck.unlockedMoves.Remove(move);
            Refresh();
        }
    }

    private void RemoveFromDeck(AttackMove move)
    {
        if (moveDeck.masterPool.Count <= 1)
            return;

        if (moveDeck.masterPool.Contains(move))
        {
            moveDeck.masterPool.Remove(move);
            moveDeck.unlockedMoves.Add(move);
            Refresh();
        }
    }

    // ============================================================
    // MOVE INFO PANEL
    // ============================================================

    public void ShowMoveInfo(AttackMove move)
    {
        if (move == null) return;

        moveInfoPanel.SetActive(true);

        // BASIC INFO
        infoNameText.text = move.moveName;
        infoDamageText.text = $"Base Damage: {move.baseDamage}";
        infoSlotCostText.text = $"Slot Cost: {move.slotCost}";
        infoCritText.text = $"Crit Multiplier: x{move.critMultiplier}";
        infoQTETypeText.text = $"QTE Type: {move.qteType}";
        infoPreDelayText.text = $"Pre Delay: {move.preDelay}s";
        infoSpeedText.text = $"QTE Speed: {move.qteSpeed}";
        infoButtonsText.text = move.possibleButtons != null && move.possibleButtons.Length > 0
            ? "Buttons: " + string.Join(", ", move.possibleButtons)
            : "Buttons: None";

        infoIcon.sprite = move.icon;

        // NEW: MOVE TYPE
        infoMoveTypeText.text = $"Move Type: {move.moveType}";

        // EFFECT POWER
        if (move.moveType == AttackMove.MoveType.Heal ||
        move.moveType == AttackMove.MoveType.BuffAttack ||
        move.moveType == AttackMove.MoveType.BuffDefense ||
        move.moveType == AttackMove.MoveType.DebuffEnemy ||
        move.moveType == AttackMove.MoveType.Setup)
        {
            infoEffectPowerText.text = $"Effect Power: {move.effectPower}";
        }
        else
        {
            infoEffectPowerText.text = "Effect Power: —";
        }

        // GOOD SYNERGY
        if (move.goodSynergyMoves != null && move.goodSynergyMoves.Count > 0)
        {
            string goodList = string.Join(", ", move.goodSynergyMoves.ConvertAll(m => m.moveName));
            infoGoodSynergyText.text = $"Good Synergy: {goodList}";
        }
        else
        {
            infoGoodSynergyText.text = "Good Synergy: None";
        }

        // BAD SYNERGY
        if (move.badSynergyMoves != null && move.badSynergyMoves.Count > 0)
        {
            string badList = string.Join(", ", move.badSynergyMoves.ConvertAll(m => m.moveName));
            infoBadSynergyText.text = $"Bad Synergy: {badList}";
        }   
        else
        {
            infoBadSynergyText.text = "Bad Synergy: None";
        }

        // NEW: EFFECT POWER (only relevant for non‑attack moves)
        switch (move.moveType)
        {
            case AttackMove.MoveType.Attack:
                infoDescriptionText.text = "A standard attack. Damage depends on QTE result.";
                break;

            case AttackMove.MoveType.Heal:
                infoDescriptionText.text = "Restores HP. Does not deal damage.";
                break;

            case AttackMove.MoveType.BuffAttack:
                infoDescriptionText.text = "Temporarily increases your Attack for this turn.";
                break;

            case AttackMove.MoveType.BuffDefense:
                infoDescriptionText.text = "Temporarily increases your Defense for this turn.";
                break;

            case AttackMove.MoveType.DebuffEnemy:
                infoDescriptionText.text = "Lowers enemy Defense for this turn.";
                break;

            case AttackMove.MoveType.Setup:
                infoDescriptionText.text = "Boosts the next move in your combo.";
                break;

            case AttackMove.MoveType.Finisher:
                infoDescriptionText.text = "Deals bonus damage if used as the final move.";
                break;

            case AttackMove.MoveType.Utility:
                infoDescriptionText.text = "Special effect move. Does not deal damage.";
                break;
        }
    }

    public void CloseMoveInfo()
    {
        moveInfoPanel.SetActive(false);
    }

    // ============================================================
    // SCENE FILTERING
    // ============================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Main Menu" || scene.name == "BattleScene")
        {
            if (deckPanel.activeSelf)
                CloseDeckUI();
        }

        HookInput();
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private void PauseGame() => Time.timeScale = 0f;
    private void ResumeGame() => Time.timeScale = 1f;

    private void SetPlayerMovement(bool enabled)
    {
        var playerInput = FindObjectOfType<PlayerLocomotionInput>();
        if (playerInput != null)
            playerInput.movementLocked = !enabled;
    }
}
