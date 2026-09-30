using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Post-battle level-up UI with stat increase slot machine mechanic.
/// Displays stat changes, allows player to spin for bonus increases, and returns to overworld.
/// Called by BattleManager.EndBattle() and coordinates with PlayerStats for stat updates.
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    /// <summary>Main panel containing all level-up UI elements.</summary>
    public GameObject panel;

    /// <summary>Displays current stat values; populated by ShowLevelUp() from battle results.</summary>
    [Header("Stat Texts")]
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI counteratkText;
    public TextMeshProUGUI maxslotsText;
    public TextMeshProUGUI turnControlText;
    public TextMeshProUGUI criticalchanceText;

    /// <summary>Battle UI elements hidden during level-up (attack button, run button, health bar).</summary>
    [Header("Battle UI elements to Hide")]
    public Button attackButton;
    public Button ItemButton;
    public Button runButton;
    public Slider playerHealthBar;

    /// <summary>Slot machine reel speed in units per second; lower values = slower spin.</summary>
    [Header("Slot Reel")]
    public float reelSpeed = 10f;

    /// <summary>Current logical position of reel animation (0 to length of reel array).</summary>
    private float reelPosition = 0f;
    /// <summary>True when slot machine is actively spinning; controls Update() spin animation.</summary>
    private bool isSpinning = false;

    /// <summary>Text fields displaying selected stat bonuses after spin completes.</summary>
    [Header("Bonus Texts")]
    public TextMeshProUGUI hpBonusText;
    public TextMeshProUGUI atkBonusText;
    public TextMeshProUGUI defBonusText;
    public TextMeshProUGUI counteratkBonusText;
    public TextMeshProUGUI maxslotsBonusText;
    public TextMeshProUGUI turnControlBonusText;
    public TextMeshProUGUI criticalchanceBonusText;

    /// <summary>Stat selection buttons and continue/stop control buttons.</summary>
    [Header("Buttons")]
    public Button hpButton;
    public Button atkButton;
    public Button defButton;
    public Button counteratkButton;
    public Button maxslotsButton;
    public Button turnControlButton;
    public Button criticalchanceButton;
    public Button continueButton;
    public Button stopButton;

    /// <summary>Text display for currently spinning reel value; updated each frame during spin.</summary>
    [Header("Slot UI")]
    public TextMeshProUGUI slotDisplayText;

    /// <summary>Available bonus values for slot machine results (1-10).</summary>
    private int[] reelNumbers = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    /// <summary>Reference to PlayerStats; used to apply selected stat bonuses (called by ApplyBonus()).</summary>
    private PlayerStats stats;

    /// <summary>
    /// Cached ExamplePlayerController to avoid repeated GetComponent calls.
    /// OPTIMIZATION: GetComponent is expensive, cache once in ShowLevelUp().
    /// Used to lock/unlock movement during level-up sequence.
    /// </summary>
    private ExamplePlayerController cachedPlayerController;

    /// <summary>Result value from most recent spin; determines bonus amount applied.</summary>
    private int currentSpinValue = 0;

    /// <summary>Tracks running animation coroutines for cleanup.</summary>
    private Coroutine currentAnim;
    /// <summary>Reference to active slot spin coroutine; stopped if player clicks stop button.</summary>
    private Coroutine spinRoutine;

    /// <summary>Stat type selector for bonus application (HP, ATK, or DEF).</summary>
    private enum StatType { HP, Attack, Defense, counteratk, criticalchance, TurnControl }
    /// <summary>Currently selected stat for bonus application.</summary>
    private StatType chosenStat;

    /// <summary>
    /// Displays level-up screen with stat comparison and slot machine interface.
    /// Called by BattleManager.EndBattle() after battle completes.
    /// Shows stat increases and hides battle UI elements.
    /// References PlayerStats for current stat values.
    /// </summary>
    public IEnumerator ShowLevelUp(PlayerStats stats, int oldHP, int oldATK, int oldDEF, int oldcounteratk, int oldmaxslots, int oldturnControl, float oldcriticalchance)
    {
        Debug.Log("[LEVEL UI] ShowLevelUp called. pendingRankUp=" + PlayerStats.Instance.pendingRankUp);
        this.stats = stats;

        // OPTIMIZATION: Cache player controller once to avoid repeated GetComponent calls
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            cachedPlayerController = player.GetComponent<ExamplePlayerController>();
            if (cachedPlayerController != null)
                cachedPlayerController.movementLocked = true;
        }

        panel.SetActive(true);
        slotDisplayText.gameObject.SetActive(false);
        stopButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);

        hpText.text  = $"HP:  {oldHP}  → {stats.MaxHealth}";
        atkText.text = $"ATK: {oldATK} → {stats.attack}";
        defText.text = $"DEF: {oldDEF} → {stats.defense}";
        counteratkText.text = $"C-ATK: {oldcounteratk} → {stats.counteratk}";
        maxslotsText.text = $"Slots: {oldmaxslots} → {stats.maxslots}";
        turnControlText.text = $"TurnCtrl: {oldturnControl} → {stats.turnControl}";
        criticalchanceText.text = $"Crit%: {oldcriticalchance:F2} → {stats.criticalchance:F2}";

        hpButton.gameObject.SetActive(true);
        atkButton.gameObject.SetActive(true);
        defButton.gameObject.SetActive(true);
        counteratkButton.gameObject.SetActive(true);
        turnControlButton.gameObject.SetActive(true);
        criticalchanceButton.gameObject.SetActive(true);

        maxslotsButton.gameObject.SetActive(false);

        if (attackButton != null) attackButton.gameObject.SetActive(false);
        if (ItemButton != null) ItemButton.gameObject.SetActive(false);
        if (runButton != null) runButton.gameObject.SetActive(false);
        if (playerHealthBar != null) playerHealthBar.gameObject.SetActive(false);

        yield return null;
    }

    private void Update()
    {
        // Allow player to stop spin by pressing space (alternative to button click)
        if (isSpinning && Input.GetKeyDown(KeyCode.Space))
            OnStopButton();
    }

    /// <summary>Handler for HP button click; initiates slot spin for HP bonus.</summary>
    public void OnChooseHP()  => StartSlotSpin(StatType.HP);
    /// <summary>Handler for ATK button click; initiates slot spin for ATK bonus.</summary>
    public void OnChooseATK() => StartSlotSpin(StatType.Attack);
    /// <summary>Handler for DEF button click; initiates slot spin for DEF bonus.</summary>
    public void OnChooseDEF() => StartSlotSpin(StatType.Defense);
    public void OnChooseC_ATK() => StartSlotSpin(StatType.counteratk);
    public void OnChooseTurnControl() => StartSlotSpin(StatType.TurnControl);
    public void OnChooseCriticalchance() => StartSlotSpin(StatType.criticalchance);

    /// <summary>
    /// Initiates slot machine spin for selected stat type.
    /// Disables stat buttons except chosen stat, shows reel animation, and waits for stop input.
    /// Called by OnChooseHP(), OnChooseATK(), OnChooseDEF() button handlers.
    /// </summary>
    private void StartSlotSpin(StatType stat)
    {
        chosenStat = stat;
        isSpinning = true;

        // Lock all buttons except the chosen stat (prevents multiple simultaneous spins)
        hpButton.interactable = false;
        atkButton.interactable = false;
        defButton.interactable = false;
        counteratkButton.interactable = false;
        turnControlButton.interactable = false;
        // maxslotsButton.interactable = false;
        criticalchanceButton.interactable = false;

        // Keep only the selected stat button interactable
        switch (stat)
        {
            case StatType.HP:      hpButton.interactable  = true; break;
            case StatType.Attack:  atkButton.interactable = true; break;
            case StatType.Defense: defButton.interactable = true; break;
            case StatType.counteratk: counteratkButton.interactable = true; break;
            // case StatType.maxslots: maxslotsButton.interactable = true; break;
            case StatType.criticalchance: criticalchanceButton.interactable = true; break;
            case StatType.TurnControl: turnControlButton.interactable = true; break;
        }

        // Hide bonus displays and show stop button
        hpBonusText.gameObject.SetActive(false);
        atkBonusText.gameObject.SetActive(false);
        defBonusText.gameObject.SetActive(false);
        counteratkBonusText.gameObject.SetActive(false);
        maxslotsBonusText.gameObject.SetActive(false);
        turnControlBonusText.gameObject.SetActive(false);
        criticalchanceBonusText.gameObject.SetActive(false);

        stopButton.gameObject.SetActive(true);
        continueButton.gameObject.SetActive(false);

        slotDisplayText.gameObject.SetActive(true);
        reelPosition = 0f;

        // Stop previous spin if one was running, then start new spin
        if (spinRoutine != null)
            StopCoroutine(spinRoutine);
        spinRoutine = StartCoroutine(SlotMachineSpin());
    }

    /// <summary>
    /// Stops active slot machine spin and finalizes result.
    /// Called by OnStopButton() when player clicks stop or presses space.
    /// Sets isSpinning to false, which exits SlotMachineSpin() coroutine.
    /// </summary>
    public void OnStopButton()
    {
        if (!isSpinning)
            return;

        // Signal spin loop to stop; coroutine finishes and applies bonus
        isSpinning = false;
        stopButton.interactable = false;
    }

    public void OnContinueButton()
    {
        Debug.Log("[LEVEL UI] OnContinueButton pressed. pendingRankUp=" + PlayerStats.Instance.pendingRankUp + ", justRankedUp=" + PlayerStats.Instance.justRankedUp + ", RankUpUI.Instance=" + RankUpUI.Instance);
        
        // Re-enable battle UI elements
        if (attackButton != null) attackButton.gameObject.SetActive(true);
        if (ItemButton != null) ItemButton.gameObject.SetActive(true);
        if (runButton != null) runButton.gameObject.SetActive(true);
        if (playerHealthBar != null) playerHealthBar.gameObject.SetActive(true);

        panel.SetActive(false);

        // Restore player movement and input control (use cached controller to avoid GetComponent)
        if (cachedPlayerController != null)
        {
            cachedPlayerController.movementLocked = false;
            cachedPlayerController.EnableInput(true);
        }

        Debug.Log("[LevelUpUI] Battle complete, returning to overworld.");

        if (PlayerStats.Instance.pendingRankUp && RankUpUI.Instance != null)
        {
            PlayerStats.Instance.pendingRankUp = false;
            PlayerStats.Instance.justRankedUp = false;

            RankUpUI.Instance.ShowRankUp(
                PlayerStats.Instance.bountyRankName,
                PlayerStats.Instance.lastRankSlotBonus,
                PlayerStats.Instance.lastRankDeckBonus,
                PlayerStats.Instance.lastUnlockedMove
            );

            return; // IMPORTANT: stop here so overworld doesn't trigger
        }
        else
        {
            // Normal behavior
            BattleManager.Instances.FinishBattleReturnToOverworld();
        }
    }

    /// <summary>
    /// Animates slot machine reel and applies bonus when spin completes.
    /// Continuously displays spinning values until OnStopButton() signals stop.
    /// Then calculates final bonus from current reel value and applies via ApplyBonus().
    /// </summary>
    private IEnumerator SlotMachineSpin()
    {
        int len = reelNumbers.Length;

        // Spin continuously until OnStopButton() sets isSpinning = false (user clicks stop button)
        while (isSpinning)
        {
            // Advance reel position in "index space": 0 → len → 0 (wrapping)
            reelPosition += reelSpeed * Time.deltaTime;

            if (reelPosition >= len)
                reelPosition -= len;

            // Convert position to discrete index for display
            int index = Mathf.FloorToInt(reelPosition);
            if (index < 0) index = 0;
            if (index >= len) index = len - 1;

            currentSpinValue = reelNumbers[index];
            PlayNumberAnimation("+" + currentSpinValue);

            yield return null;
        }

        // Optional deceleration for polish: gradually slow reel before stopping
        float slowTime = 0.2f;
        float elapsed = 0f;
        float startSpeed = reelSpeed;

        while (elapsed < slowTime)
        {
            float t = elapsed / slowTime;
            float speed = Mathf.Lerp(startSpeed, 0f, t); // Decelerate from startSpeed to 0

            reelPosition += speed * Time.deltaTime;
            if (reelPosition >= len)
                reelPosition -= len;

            int index = Mathf.FloorToInt(reelPosition);
            if (index < 0) index = 0;
            if (index >= len) index = len - 1;

            currentSpinValue = reelNumbers[index];
            PlayNumberAnimation("+" + currentSpinValue);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap to final reel value
        int finalIndex = Mathf.RoundToInt(reelPosition);
        if (finalIndex < 0) finalIndex = 0;
        if (finalIndex >= len) finalIndex = len - 1;

        currentSpinValue = reelNumbers[finalIndex];
        PlayNumberAnimation("+" + currentSpinValue);

        // Apply bonus stat increase based on final spin value and chosen stat type
        stats.statBonusAmount = currentSpinValue;

        switch (chosenStat)
        {
            case StatType.HP:
                for (int i = 0; i < currentSpinValue; i++)
                    stats.UpgradeHP();
                hpText.text = $"HP:  {stats.oldMaxHealth} → {stats.MaxHealth}";
                hpButton.interactable = false;
                break;

            case StatType.Attack:
                for (int i = 0; i < currentSpinValue; i++)
                    stats.UpgradeATK();
                atkText.text = $"ATK: {stats.oldAttack} → {stats.attack}";
                atkButton.interactable = false;
                break;

            case StatType.Defense:
                for (int i = 0; i < currentSpinValue; i++)
                    stats.UpgradeDEF();
                defText.text = $"DEF: {stats.oldDefense} → {stats.defense}";
                defButton.interactable = false;
                break;

            case StatType.counteratk:
                for (int i = 0; i < currentSpinValue; i++)
                    stats.UpgradeCounteratk();
                counteratkText.text = $"C-ATK: {stats.oldCounteratk} → {stats.counteratk}";
                counteratkButton.interactable = false;
                break;

            /* case StatType.maxslots:
                for (int i = 0; i < currentSpinValue; i++)
                    stats.UpgradeMaxslots();
                maxslotsText.text = $"Slots: {stats.oldMaxslots} → {stats.maxslots}";
                maxslotsButton.interactable = false;
                break;   */

                case StatType.TurnControl:
                for (int i = 0; i < currentSpinValue; i++)
                    stats.UpgradeTurnControl();
                turnControlText.text = $"TurnCtrl: {stats.oldTurnControl} → {stats.turnControl}";
                turnControlBonusText.text = $"+{currentSpinValue}";
                //turnControlBonusText.gameObject.SetActive(true);
                turnControlButton.interactable = false;
                break;

            case StatType.criticalchance:
                for (int i = 0; i < currentSpinValue; i++)
                    stats.UpgradeCriticalchance();
                criticalchanceText.text = $"Crit%: {stats.oldCriticalchance:F2} → {stats.criticalchance:F2}";
                criticalchanceButton.interactable = false;
                break;
        }

        stopButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(true);

        spinRoutine = null;
    }

    private void PlayNumberAnimation(string value)
    {
        if (currentAnim != null)
            StopCoroutine(currentAnim);

        currentAnim = StartCoroutine(FadeInAndSlide(slotDisplayText, value));
    }

    private IEnumerator FadeInAndSlide(TextMeshProUGUI text, string value)
    {
        text.text = value;
        text.alpha = 0.6f;
        text.rectTransform.anchoredPosition = new Vector2(0, 20);

        float duration = 0.3f; // quick tick feel
        float t = 0;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            text.alpha = Mathf.Lerp(0.6f, 1f, t);
            text.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(0, 20), new Vector2(0, -5), t);
        
            yield return null;
        }
    }
}
