using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Diagnostics;
using UnityDebug = UnityEngine.Debug;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEditor.Rendering;

/// <summary>
/// BattleManager: handles player/enemy turns, combo/QTE flow,
/// multi-enemy parties, and synergy chain state.
/// </summary>
public class BattleManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI battleText;
    public Slider playerHealthBar;
    public Slider enemyHealthBar; // legacy single-enemy bar
    public Button runButton;
    public Button attackButton;
    public Button ItemButton;
    public MoveSelectionUI moveSelectionUI;

    [Header("Turn Meter")]
    public Slider turnMeterBar;   // assign in inspector
    public int turnMeterCurrent = 0;
    public int turnMeterMax = 100;
    public bool enemyTurnInterrupted = false;

    /// <summary>Singleton instance of BattleManager for global access.</summary>
    public static BattleManager Instances;

    /// <summary>Maximum health value of the current enemy.</summary>
    private int enemyMaxHealth;
    /// <summary>Current health value of the enemy. Updated during combat.</summary>
    private int enemyCurrentHealth;
    /// <summary>Display name of the enemy from BattleData.enemyName.</summary>
    private string enemyName;
    /// <summary>Unique identifier of the enemy from BattleData.enemyID.</summary>
    private string enemyID;

    /// <summary>Prefab for the target indicator shown when selecting enemies.</summary>
    public GameObject targetIndicatorPrefab;
    /// <summary>Instantiated target indicator; positioned over currently targeted enemy.</summary>
    private GameObject targetIndicatorInstance;
    /// <summary>Currently selected enemy target for player combo attacks.</summary>
    private EnemyAttackController currentTarget;

    [Header("Level Up UI")]
    /// <summary>UI component for displaying level up progression.</summary>
    public LevelUpUI levelUpUI;
    /// <summary>UI component for displaying bounty progress.</summary>
    public BountyProgressUI bountyProgressUI;
    /// <summary>Battle scene camera; panned during level-up sequences.</summary>
    public Camera battleCamera;

    /// <summary>Flag indicating if the battle has concluded (victory or defeat).</summary>
    public bool battleOver = false;
    public bool playerWasCountered = false;

    private AttackMove missedMoveForCounter;

    /// <summary>Flag preventing input during animations or transitions.</summary>
    private bool isBusy = false;
    /// <summary>Tracks the current turn number in battle.</summary>
    public int turnCounter = 0;
    /// <summary>Flag indicating if enemy QTE sequence has finished.</summary>
    private bool enemyQTEFinished = false;
    /// <summary>Result of the enemy's QTE sequence.</summary>
    private QTEResult enemyQTEResult;
    [Header("Turn Pattern")]
    public List<Turn> turnPattern = new List<Turn>() { Turn.Player, Turn.Enemy };
    private int turnPatternIndex = 0;

    /// <summary>Enum representing whose turn it is: Player or Enemy.</summary>
    public enum Turn { Player, Enemy }
    /// <summary>Current turn in the battle.</summary>
    private Turn currentTurn;

    [Header("Battle Spawn")]
    /// <summary>Transform where the player spawns when entering battle.</summary>
    public Transform battleSpawnPoint;

    [Header("Map Spawn Point")]
    /// <summary>Transform indicating where the player returns after battle.</summary>
    public Transform mapSpawnPoint;

    [Header("Combo / QTE")]
    /// <summary>List of available attack moves the player can select from; sourced from PlayerMoveDeck.</summary>
    public List<AttackMove> availableMoves;
    /// <summary>List of enemy attack moves for QTE sequences.</summary>
    public List<EnemyMove> enemyQTE;
    /// <summary>Current player combo being executed; populated by MoveSelectionUI.OnMovesSelected().</summary>
    public PlayerCombo currentCombo;
    /// <summary>QTE manager handling quick-time event logic; reference from QTEManager.cs.</summary>
    public QTEManager qteManager;

    /// <summary>Player's move deck providing available moves for combat selection.</summary>
    private PlayerMoveDeck moveDeck;
    /// <summary>Manages player combo selection UI; calls BattleManager.OnMovesSelected().</summary>
    public PlayerComboManager comboManager;
    public BattleInventoryUI battleInventoryUI;

    /// <summary>Bounty earned during the current battle; applied to PlayerStats on victory.</summary>
    public int bountyGainedThisBattle = 0;

    /// <summary>Index of the move currently being executed in the player's combo.</summary>
    public int currentMoveIndex = 0;

    public bool counterUsedThisPlayerTurn = false;
    /// <summary>Flag preventing target changes once combat has begun.</summary>
    private bool targetLocked = false;
    /// <summary>Results of each QTE in the current combo sequence (Perfect, Good, Ok, Miss).</summary>
    public readonly List<QTEResult> qteResults = new List<QTEResult>();
    /// <summary>Cached list of living enemies in the current battle encounter.</summary>
    private readonly List<EnemyAttackController> activeEnemies = new List<EnemyAttackController>();
    /// <summary>Flag indicating if player's combo was interrupted by a miss (triggers counter damage).</summary>
    private bool comboEndedByMiss = false;
    private bool itemUsedThisTurn = false;
    private bool battleInitialized = false;
    private bool enemyTurnRunning = false;

    private float lastPlayerHPValue = -1f;
    private float lastEnemyHPValue = -1f;
    private float lastTurnMeterValue = -1f;

    private bool meterDraining = false;

    private Coroutine turnMeterRoutine;
    private Coroutine smoothMeterRoutine;
    private Coroutine playerHpRoutine;

    private int tempAttackBuff = 0;
    private int tempDefenseBuff = 0;

    private float tempDamageMultiplier = 1f;

    /// <summary>Reference to the enemy currently executing their turn; used for QTE result routing in EnemyAttackController.DoEnemyTurn().</summary>
    private EnemyAttackController currentEnemyActing;

    /// <summary>Cached player GameObject to avoid repeated FindWithTag calls during hot paths.</summary>
    private GameObject cachedPlayer;

    private Coroutine messageRoutine;

    /// <summary>
    /// Cached ExamplePlayerController to avoid repeated GetComponent calls.
    /// OPTIMIZATION: GetComponent is expensive, cache once at battle start.
    /// </summary>
    private ExamplePlayerController cachedPlayerController;

    /// <summary>
    /// Cached CharacterController to avoid repeated GetComponent calls.
    /// OPTIMIZATION: GetComponent is expensive, cache once at battle start.
    /// </summary>
    private CharacterController cachedCharController;

    /// <summary>Reusable WaitForSeconds cache to reduce GC allocations in ShowMessage coroutines.</summary>
    private static readonly WaitForSeconds messageDelay = WaitCache.Get(1.5f);

    private static readonly WaitForSeconds defaultMessageDelay = new WaitForSeconds(1.5f);

    /// <summary>Tracks synergy chain state across multiple enemy turns for bonus damage calculations.</summary>
    /* [System.Serializable]
    public class SynergyChainState
    {
        /// <summary>Flag set when an enemy was damaged on DEF/armor; enables DEF-break synergy bonuses.</summary>
        public bool defTargetedLastTurn;
        /// <summary>Flag set when significant damage was dealt to HP or ATK; enables damage accumulation synergies.</summary>
        public bool hpOrAtkTargetedLastTurn;

        /// <summary>Resets synergy chain flags at the start of each enemy turn.</summary>
        public void Reset()
        {
            defTargetedLastTurn = false;
            hpOrAtkTargetedLastTurn = false;
        }
    } */

    [Header("Synergy")]
    /// <summary>Tracks cross-enemy, cross-turn synergy chains for combo bonus calculations.</summary>
    public SynergyChain synergyChain = new SynergyChain();

    // ====================================================
    // Lifecycle Methods
    // ====================================================

    /// <summary>
    /// Initializes singleton and optional QTEManager reference. 
    /// Sets performance targets for the battle scene.
    /// </summary>
    private void Awake()
    {
        // Set global performance cap for the battle scene
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;

        if (qteManager == null)
        {
            // Auto-find QTEManager if not assigned in inspector
            qteManager = FindFirstObjectByType<QTEManager>();
            //UnityDebug.LogWarning("[BattleManager] qteManager was NULL — Found: " + (qteManager ? qteManager.name : "NONE"));
        }
    }

    /// <summary>
    /// Registers for QTE completion events from QTEManager.cs.
    /// </summary>
    private void OnEnable()
    {
        if (qteManager != null)
            qteManager.OnQTECompleted += OnQTEFinished;
    }

    /// <summary>
    /// Unregisters QTE completion events to prevent callback leaks.
    /// </summary>
    private void OnDisable()
    {
        if (qteManager != null)
            qteManager.OnQTECompleted -= OnQTEFinished;
    }

    /// <summary>
    /// Initializes battle scene: loads battle map, spawns enemies, and starts player turn.
    /// References BattleData.cs for enemy configuration.
    /// </summary>
    private void Start()
    {
        Instances = this;
        /* UnityDebug.Log($"[BattleManager] BattleData check -> " +
               $"enemyName='{BattleData.enemyName}', " +
               $"partyCount={(BattleData.enemyPartyRuntime != null ? BattleData.enemyPartyRuntime.Count : -1)}");
        UnityDebug.Log($"[DEBUG] Loaded initial health from BattleData: {BattleData.enemyCurrentHealth}/{BattleData.enemyMaxHealth}"); */

        battleInventoryUI.Init(this);
        battleInventoryUI.Hide();

        if (targetIndicatorPrefab != null)
        {
            targetIndicatorInstance = Instantiate(targetIndicatorPrefab);
            targetIndicatorInstance.SetActive(false);
        }

        // Instantiate battle map once and initialize all scene elements via coroutine
        if (BattleData.battleMapPrefab != null)
        {
            Instantiate(BattleData.battleMapPrefab, mapSpawnPoint.position, mapSpawnPoint.rotation);
            StartCoroutine(SetupBattleScene());
        }
        else
        {
            //UnityDebug.LogWarning("No battle map prefab assigned for this enemy!");
        }

        // Restore player position if returning from a previous battle
        if (GameManager.Instance.returningFromBattle)
        {
            var player = GetCachedPlayer();
            if (player != null)
                player.transform.position = GameManager.Instance.playerPositionBeforeBattle;

            GameManager.Instance.returningFromBattle = false;
        }

        moveDeck = FindObjectOfType<PlayerMoveDeck>();

        if (moveDeck == null) 
        { 
            //UnityDebug.LogError("[BattleManager] Could not find PlayerMoveDeck in GameRoot!"); 
        } 
        else 
        {
            //UnityDebug.Log("[BattleManager] Connected to PlayerMoveDeck: " + moveDeck.name);
            availableMoves = moveDeck.GetFullDeck(); 
        }

        /* if (qteManager == null)
            UnityDebug.LogError("[BattleManager] qteManager is NULL at runtime!");
        else
            UnityDebug.Log("[BattleManager] qteManager found: " + qteManager.name); */

        CursorManager.Show();

        // Load enemy data from BattleData singleton
        enemyMaxHealth = BattleData.enemyMaxHealth;
        enemyCurrentHealth = BattleData.enemyCurrentHealth;
        enemyName = BattleData.enemyName;
        enemyID = BattleData.enemyID;

        if (enemyMaxHealth <= 0)
        {
            //UnityEngine.Debug.LogWarning($"[BattleManager] enemyMaxHealth was <= 0 ({enemyMaxHealth}). Clamping to 1.");
            enemyMaxHealth = 1;
        }

        //UnityEngine.Debug.Log($"[BattleManager] Enemy '{enemyName}' loaded. Max={enemyMaxHealth}, Current={enemyCurrentHealth}, ID={enemyID}");


        UpdateHealthBars();
        activeEnemies.Clear();
        ShowBattleMessage($"A wild {enemyName} appeared!");
    }

    /// <summary>
    /// Orchestrates the complete battle scene initialization sequence.
    /// Waits for spawned objects to initialize, then establishes player position, camera, and enemies.
    /// </summary>
    private IEnumerator SetupBattleScene()
    {
        yield return null;

        // Chache spawn point
        if (battleSpawnPoint == null)
            battleSpawnPoint = GameObject.FindWithTag("BattleSpawn")?.transform;

        PlacePlayerInBattle();

        // Chache camera
        if (battleCamera == null)
            battleCamera = GameObject.FindWithTag("BattleCamera")?.GetComponent<Camera>();

        yield return InitializeBattleEnemies();

        turnPattern = BattleData.turnPattern ?? new List<Turn>()
        {
            Turn.Player, Turn.Enemy
        };
        turnPatternIndex = 0;
        currentTurn = turnPattern[0];

        battleInitialized = true;

        if (currentTurn == Turn.Player)
            StartPlayerTurn();
        else
            StartCoroutine(EnemyTurn());

        UpdateButtonState();
    }

    // ====================================================
    // Enemy Initialization & Caching
    // ====================================================F

    /// <summary>
    /// Polls for spawned EnemyAttackController instances and populates activeEnemies list.
    /// Used during scene initialization in SetupBattleScene().
    /// </summary>
    private IEnumerator InitializeBattleEnemies()
    {
        EnemyAttackController[] enemies = null;
        int safety = 300; // ~5 seconds at 60 FPS

        while ((enemies == null || enemies.Length == 0) && safety-- > 0)
        {
            enemies = FindObjectsByType<EnemyAttackController>(FindObjectsSortMode.None);
            yield return null;
        }

        activeEnemies.Clear();
        if (enemies != null)
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e != null && !e.IsDead())
                    activeEnemies.Add(e);
            }
        }
    }

    /// <summary>
    /// Returns the list of currently active enemies.
    /// Used by UI and targeting systems to determine valid targets.
    /// </summary>
    public List<EnemyAttackController> GetActiveEnemies()
    {
        return activeEnemies;
    }

    // ====================================================
    // Spawn Point & Camera Initialization
    // ====================================================

    /// <summary>
    /// Polls for a GameObject tagged 'BattleSpawn' and caches its transform.
    /// Called during SetupBattleScene() to establish player spawn location.
    /// </summary>
    private IEnumerator FindBattleSpawnPointDelayed()
    {
        GameObject spawn = null;
        int safety = 300;

        while (spawn == null && safety-- > 0)
        {
            spawn = GameObject.FindWithTag("BattleSpawn");
            yield return null;
        }

        if (spawn != null)
            battleSpawnPoint = spawn.transform;
    }

    /// <summary>
    /// Polls for a Camera tagged 'BattleCamera' and caches the component.
    /// Called during SetupBattleScene() for camera-based animations like level-up sequences.
    /// </summary>
    private IEnumerator FindBattleCameraDelayed()
    {
        Camera foundCam = null;
        int safety = 300;

        while (foundCam == null && safety-- > 0)
        {
            var camObj = GameObject.FindWithTag("BattleCamera");
            if (camObj != null)
                foundCam = camObj.GetComponent<Camera>();

            yield return null;
        }

        if (foundCam != null)
            battleCamera = foundCam;
    }

    /// <summary>
    /// Positions player at battle spawn point and disables movement/input.
    /// OPTIMIZATION: Caches player controller and char controller to avoid repeated GetComponent calls.
    /// References ExamplePlayerController.cs and CharacterController for movement control.
    /// </summary>
    private void PlacePlayerInBattle()
    {
        var player = GetCachedPlayer();
        if (player == null)
        {
            //UnityEngine.Debug.LogWarning("[BattleManager] No player found in scene to place in battle.");
            return;
        }

        // OPTIMIZATION: Cache these components to avoid repeated GetComponent calls throughout battle
        if (cachedPlayerController == null)
            cachedPlayerController = player.GetComponent<ExamplePlayerController>();
        
        if (cachedCharController == null)
            cachedCharController = player.GetComponent<CharacterController>();

        if (cachedPlayerController != null)
        {
            cachedPlayerController.movementLocked = true;
            cachedPlayerController.EnableInput(false);
            //UnityEngine.Debug.Log("[BattleManager] Player movement locked for battle.");
        }

        if (cachedCharController != null) 
            cachedCharController.enabled = false;

        if (battleSpawnPoint != null)
        {
            player.transform.position = battleSpawnPoint.position;
            player.transform.rotation = battleSpawnPoint.rotation;
        }
        else
        {
            //UnityEngine.Debug.LogWarning("[BattleManager] No battle spawn point set, player position not changed.");
        }
        if (cachedCharController != null) cachedCharController.enabled = true;
    }

    // ====================================================
    // Player Turn & Input Handling
    // ====================================================

    /// <summary>
    /// Initializes the player turn: clears locks, displays UI, and positions target indicator.
    /// Called by SetupBattleScene() initially and by EnemyTurn() after enemy actions conclude.
    /// </summary>
    private void StartPlayerTurn()
    {
        UnityDebug.Log(
            $"[PLAYER TURN] StartPlayerTurn() | " +
            $"patternIndex={turnPatternIndex} | " +
            $"currentTurn={currentTurn}"
        );

        counterUsedThisPlayerTurn = false;

        foreach (var enemy in activeEnemies)
        {
            if (enemy.runtime.pattern.predictionMeter >= 100f)
                enemy.runtime.counterPressure += 0.10f;
        }

        foreach (var enemy in activeEnemies)
        {
            var p = enemy.runtime.pattern;

            UnityDebug.Log(
                $"[PATTERN] Enemy '{enemy.GetEnemyName()}' | " +
                $"Awareness={p.awarenessMeter:F1} | " +
                $"Prediction={p.predictionMeter:F1} | " +
                $"CounterPressure={enemy.runtime.counterPressure:F2}"
            );
        }

        /* if (turnMeterCurrent >= turnMeterMax)
        {
            turnMeterCurrent = 0;
            if (turnMeterBar != null)
                StartCoroutine(DrainWhenVisible());
        } */

        if (!battleInitialized || currentTurn != Turn.Player)
            return;

        isBusy = false;
        targetLocked = false;
        itemUsedThisTurn = false;
        enemyTurnInterrupted = false;

        if (turnMeterBar != null)
            turnMeterBar.gameObject.SetActive(false);

        if (moveDeck != null && comboManager != null)
            comboManager.moveDeck = moveDeck;

        if (tempAttackBuff != 0)
        {
            PlayerStats.Instance.attack -= tempAttackBuff;
            tempAttackBuff = 0;
        }

        if (tempDefenseBuff != 0)
        {
            PlayerStats.Instance.defense -= tempDefenseBuff;
            tempDefenseBuff = 0;
        }

        var enemies = activeEnemies;
        if (targetIndicatorInstance != null && enemies.Count > 0)
        {
            currentTarget = enemies[Random.Range(0, enemies.Count)];
            targetIndicatorInstance.SetActive(true);

            var pos = currentTarget.transform.position;
            pos.y -= 0.4f;
            targetIndicatorInstance.transform.position = pos;
        }

        battleText.text = "Pick Your Target!";
        UpdateButtonState();
    }

    /// <summary>
    /// Handles the attack button press. Initiates combo selection via PlayerComboManager.
    /// Locks input and targets until combo selection is complete.
    /// </summary>
    public void OnAttackButton()
    {
        if (isBusy || currentTurn != Turn.Player || battleOver) return;

        isBusy = true;
        targetLocked = true;
        UpdateButtonState();

        if (moveSelectionUI != null && comboManager != null)
        {
            // Delegate combo selection to PlayerComboManager; will call OnMovesSelected() when complete
            comboManager.StartComboSelection();
        }
        else
        {
            //UnityEngine.Debug.LogWarning("[BattleManager] MoveSelectionUI not assigned - starting auto combo.");
            StartPlayerComboAutoTest();
        }
    }

    public void OnItemButton()
    {
        if (isBusy || currentTurn != Turn.Player || battleOver) return;

        //UnityDebug.Log($"ItemButton pressed. isBusy={isBusy}, turn={currentTurn}, battleOver={battleOver}");

        OpenBattleInventory();
    }

    public void UseBattleItem(InventoryItem item)
    {
        // Apply the item's effect (heal, buff, etc.)
        ApplyItemEffect(item);

        // Remove the item from inventory
        InventorySystem.current.RemoveItem(item.data);

        // Close the inventory UI
        CloseBattleInventory();

        // If the item consumes the player's turn
        if (item.data.skipTurn)
        {
            isBusy = true;
            UpdateButtonState();

            // Item ends the turn
            StartCoroutine(EndTurnAfterItem());
            return;
        }

        // ================================
        // FREE ACTION ITEM (Z-Power, etc.)
        // ================================

        // Mark that the player used a free action this turn
        itemUsedThisTurn = true;

        // Player can still act (attack, run, etc.)
        isBusy = false;
        UpdateButtonState();

        // IMPORTANT: no AdvanceTurnUnified() here.
        // Turn only ends when the player actually finishes their action flow
        // (attack combo, run, etc.).
    }


    private IEnumerator EndTurnAfterItem()
    {
        yield return WaitCache.Get(0.5f);
        AdvanceTurnUnified();
    }

    private void ApplyItemEffect(InventoryItem item)
    {
        var data = item.data;

        switch (data.consumableEffect)
        {
            case ConsumableEffectType.HealHP:
                PlayerStats.Instance.Heal(data.effectPower);
                break;

            case ConsumableEffectType.HealPercent:
                int amount = Mathf.RoundToInt(PlayerStats.Instance.MaxHealth * (data.effectPower / 100f));
                PlayerStats.Instance.Heal(amount);
                break;

            case ConsumableEffectType.BoostAttack:
            {
                tempDamageMultiplier = 1f + (data.effectPower / 100f);
                break;
            }

            case ConsumableEffectType.BoostDefense:
            {
                PlayerStats.Instance.tempDamageTakenMultiplier = 1f - (data.effectPower / 100f);
                break;
            }

            case ConsumableEffectType.None:
                //UnityDebug.LogWarning("Item has no effect.");
                break;
        }

        UpdateHealthBars();
    }
   
    private void OpenBattleInventory()
    {
        isBusy = true;
        battleInventoryUI.Show();
    }

    public void CloseBattleInventory()
    {
        battleInventoryUI.Hide();
        isBusy = false;
        UpdateButtonState();
    }

    /// <summary>
    /// Fallback combo initialization when MoveSelectionUI is unavailable.
    /// Automatically selects up to 3 moves from availableMoves for testing.
    /// </summary>
    private void StartPlayerComboAutoTest()
    {
        currentCombo = new PlayerCombo();
        currentCombo.chosenMoves.Add(availableMoves.Count > 0 ? availableMoves[0] : null);
        if (availableMoves.Count > 1) currentCombo.chosenMoves.Add(availableMoves[1]);
        if (availableMoves.Count > 2) currentCombo.chosenMoves.Add(availableMoves[2]);

        qteResults.Clear();
        currentMoveIndex = 0;
        StartNextQTE();
    }

    /// <summary>
    /// Callback invoked by PlayerComboManager when the player completes move selection.
    /// Removes selected moves from moveDeck and initiates QTE sequences.
    /// </summary>
    public void OnMovesSelected(List<AttackMove> selected)
    {
        if (currentCombo == null)
        {
            //UnityDebug.LogError("[BattleManager] currentCombo is NOT assigned in the inspector!");
            return;
        }

        currentCombo.chosenMoves.Clear(); 
        currentCombo.chosenMoves.AddRange(selected);

        //UnityDebug.Log($"[BattleManager] Received {selected.Count} moves. First move: {(selected.Count > 0 && selected[0] != null ? selected[0].moveName : "null")}");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var st = new StackTrace(true);
        //UnityEngine.Debug.Log("[TRACE] OnMovesSelected triggered by:\n" + st);
#endif

        //UnityEngine.Debug.Log($"[BattleManager] Received {selected.Count} moves. First move: {(selected.Count > 0 && selected[0] != null ? selected[0].moveName : "null")}");

        if (moveDeck != null)
        {
            // Remove selected moves from the player's hand
            for (int i = 0; i < selected.Count; i++)
            {
                var used = selected[i];
                if (used != null)
                    moveDeck.RemoveFromHand(used);
            }
        }

        if (selected.Count > PlayerStats.Instance.maxslots)
        {
            selected.RemoveRange(PlayerStats.Instance.maxslots, selected.Count - PlayerStats.Instance.maxslots);
            //UnityDebug.Log("[ChainLimit] Combo trimmed to match ChainLimit.");
        }

        qteResults.Clear();
        currentMoveIndex = 0;

        StartNextQTE();
    }

    // ====================================================
    // QTE & Combo Execution Flow
    // ====================================================

    /// <summary>
    /// Initiates the next QTE in the combo sequence.
    /// Wrapped in a coroutine to handle frame-perfect timing.
    /// </summary>
    private void StartNextQTE()
    {
        StartCoroutine(StartNextQTECoroutine());
    }

    /// <summary>
    /// Coroutine that sequentially executes each move's QTE.
    /// Waits for QTEManager to be idle, then starts the next QTE via QTEManager.StartQTEForMove().
    /// Results are collected and processed in FinishAllQTEs().
    /// </summary>
    private IEnumerator StartNextQTECoroutine()
    {
        ShowBattleMessage("Time it Right!");

        if (currentCombo == null || currentCombo.chosenMoves.Count == 0)
            yield break;

        if (currentMoveIndex >= currentCombo.chosenMoves.Count)
        {
            FinishAllQTEs();
            yield break;
        }

        var move = currentCombo.chosenMoves[currentMoveIndex];
        if (move == null)
        {
            qteResults.Add(QTEResult.Miss);
            currentMoveIndex++;
            StartNextQTE();
            yield break;
        }

        yield return null;

        // Replace WaitUntil with manual loop (zero allocations)
        float timeout = 5f;
        while (qteManager.IsRunning || qteManager.CurrentOwner != QTEManager.QTEOwner.None)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        qteManager.StartQTEForMove(move, QTEManager.QTEOwner.Player);
    }   

    /// <summary>
    /// Callback from QTEManager.OnQTECompleted event.
    /// Routes results to enemies during enemy turns, or processes player QTE results.
    /// References EnemyAttackController.ReceiveQTEResultFromBattleManager().
    /// </summary>
    public void OnQTEFinished(QTEResult result)
    {
        if (currentTurn == Turn.Enemy)
        {
            // Enemy QTE should NEVER trigger player combo resolution
            //UnityDebug.Log("[BattleManager] Ignoring FinishAllQTEs during enemy turn.");
            return;
        }

        // ============================================================
        // PATTERN COUNTER DURING PLAYER TURN
        // ============================================================
        if (currentTarget != null)
        {
            var enemy = currentTarget;

            if (enemy.TryPlayerTurnCounter(this))
            {
                // Stop the combo immediately
                comboEndedByMiss = true;

                // End player turn and go straight to enemy turn
                StartCoroutine(EndPlayerTurnAfterCounter());
                return;
            }
        }

        if (battleOver || currentCombo == null || currentCombo.chosenMoves.Count == 0)
        {
            FinishAllQTEs();
            return;
        }

        if (currentTurn == Turn.Enemy)
        {
            //UnityDebug.Log("[BattleManager] OnQTEFinished (ENEMY) result=" + result);

            if (currentEnemyActing != null)
            {
                //UnityDebug.Log("[BattleManager] Forwarding QTE result to " + currentEnemyActing.GetEnemyName());
                // Forward result to the enemy controller for damage calculation
                currentEnemyActing.ReceiveQTEResultFromBattleManager(result);
            }
            else
            {
                //UnityDebug.LogWarning("[BattleManager] currentEnemyActing is NULL during enemy QTE resolution!");
            }

            return;
        }

        //UnityDebug.Log("[BattleManager] OnQTEFinished (PLAYER) result=" + result + ", currentMoveIndex=" + currentMoveIndex);
        //UnityDebug.Log("[BattleManager] QTE finished with result: " + result);

        qteResults.Add(result);

        // Awareness gain for player QTE
        foreach (var enemy in activeEnemies)
        {
            enemy.runtime.pattern.AddAwareness(
                result,
                enemy.runtime.synergyScore,
                fakeoutHit: false,
                counterTriggered: false,
                enemy.runtime.personality,
                enemy.GetEnemyName()
            );
        }

        if (result == QTEResult.Miss)
        {
            comboEndedByMiss = true;
            missedMoveForCounter = currentCombo.chosenMoves[currentMoveIndex];
            ShowBattleMessage("You missed! Combo ends early.");
            FinishAllQTEs();
            return;
        }

        currentMoveIndex++;
        StartNextQTE();
    }

    private IEnumerator EndPlayerTurnAfterCounter()
    {
        UnityDebug.Log(
            $"[COUNTER] EndPlayerTurnAfterCounter() | " +
            $"currentTurn={currentTurn} | " +
            $"patternIndex={turnPatternIndex}"
        );

        UnityDebug.Log("[COUNTER-FLOW] EndPlayerTurnAfterCounter() called.");

        yield return new WaitForSeconds(2f);

        UnityDebug.Log("[COUNTER-FLOW] Advancing to enemy turn...");

        AdvanceTurnUnified();
    }

    /// <summary>
    /// Calculates total combo damage based on move base damage, player stats, and QTE results.
    /// Applies combo length and perfect-combo bonuses.
    /// Triggers grace turn if player achieves all-perfect combo.
    /// References PlayerStats.cs and PlayerSkillTracker for stat calculations.
    /// </summary>
    private void FinishAllQTEs()
    {
        int totalDamage = 0;
        int comboLen = currentCombo.chosenMoves.Count;
        bool allPerfect = true;

        if (playerWasCountered)
        {
            UnityDebug.Log("[BattleManager] FinishAllQTEs() ignored — player was countered."); 
            return;
        }

        // ============================
        // 1. QTE DAMAGE CALCULATION
        // ============================
        for (int i = 0; i < comboLen; i++)
        {
            var move = currentCombo.chosenMoves[i];
            var qte = (i < qteResults.Count) ? qteResults[i] : QTEResult.Miss;
            if (move == null) continue;

            float atkScale = 1f + (PlayerStats.Instance.attack * 0.04f);
            int dmg = Mathf.RoundToInt(move.baseDamage * atkScale);

            bool crit = Random.value < PlayerStats.Instance.criticalchance;
            if (crit)
            {
                dmg = Mathf.RoundToInt(dmg * 1.5f);
                //UnityDebug.Log($"[BattleManager] Move {move.moveName} crit! Damage increased to {dmg}.");
            }

            float perMoveMultiplier;
            switch (qte)
            {
                case QTEResult.Perfect:
                    perMoveMultiplier = move.critMultiplier;
                    break;
                case QTEResult.Good:
                    perMoveMultiplier = 1f;
                    allPerfect = false;
                    break;
                case QTEResult.Ok:
                    perMoveMultiplier = 0.5f;
                    allPerfect = false;
                    break;
                case QTEResult.Miss:
                default:
                    perMoveMultiplier = 0f;
                    allPerfect = false;
                    break;
            }

            totalDamage += Mathf.RoundToInt(dmg * perMoveMultiplier);
        }

        // ============================
        // 2. SYNERGY CALCULATION
        // ============================
        float synergyMultiplier = 1f;

        for (int i = 1; i < comboLen; i++)
        {
            var prev = currentCombo.chosenMoves[i - 1];
            var curr = currentCombo.chosenMoves[i];

            if (prev == null || curr == null) continue;

            // GOOD SYNERGY
            if (curr.goodSynergyMoves.Contains(prev))
            {
                synergyMultiplier += 0.15f;
                //UnityDebug.Log($"[PLAYER SYNERGY] {prev.moveName} → {curr.moveName} = +15%");
            }

            // BAD SYNERGY
            if (curr.badSynergyMoves.Contains(prev))
            {
                synergyMultiplier -= 0.20f;
                //UnityDebug.Log($"[PLAYER ANTI-SYNERGY] {prev.moveName} → {curr.moveName} = -20%");
            }
        }

        // ============================
        // 3. COMBO LENGTH + PERFECT BONUS
        // ============================
        float comboLengthBonus = 1f + 0.05f * Mathf.Max(0, comboLen - 1);
        float perfectAllBonus = allPerfect ? 1f + 0.25f * comboLen : 1f;

        int damageAfterBonus = Mathf.RoundToInt(totalDamage * comboLengthBonus * perfectAllBonus);

        // ============================
        // 4. TEMPORARY SETUP BUFF
        // ============================
        damageAfterBonus = Mathf.RoundToInt(damageAfterBonus * tempDamageMultiplier);

        // ============================
        // 5. FINISHER BONUS (LAST MOVE)
        // ============================
        if (comboLen > 0)
        {
            var lastMove = currentCombo.chosenMoves[comboLen - 1];
            if (lastMove != null && lastMove.moveType == AttackMove.MoveType.Finisher)
            {
                damageAfterBonus = Mathf.RoundToInt(damageAfterBonus * 1.5f);
                //UnityDebug.Log($"[FINISHER] {lastMove.moveName} boosted final damage by 50%.");
            }
        }

        FeedPatternToEnemies();

        //UnityDebug.Log($"[BattleManager] Combo finished. raw={totalDamage}, synergy={synergyMultiplier:F2}, comboLenBonus={comboLengthBonus:F2}, perfectAllBonus={perfectAllBonus:F2}, final={damageAfterBonus}, len={comboLen}, allPerfect={allPerfect}");

        if (allPerfect)
        {
            PlayerSkillTracker.justDidPerfectCombo = true;
            //UnityDebug.Log("[GraceTurn] Player earned a grace turn (all perfect combo).");
        }

        // If the player was countered, DO NOT apply Combo damage
        if (playerWasCountered)
        {
            UnityDebug.Log("[BattleManager] Combo skipped due to counter - no damage applied00");
            return;
        }

        StartCoroutine(ApplyComboDamage(damageAfterBonus));
    }

    // ============================================================
    // PATTERN AWARENESS FEEDING (GLOBAL + LOCAL)
    // ============================================================
    /* private void FeedPatternAwarenessToEnemies()
    {
        if (currentCombo == null || currentCombo.chosenMoves.Count == 0)
            return;

        var enemies = activeEnemies;
        if (enemies == null || enemies.Count == 0)
            return;

        // --- Extract player pattern data ---
        string opener   = currentCombo.chosenMoves[0].moveName;
        string finisher = currentCombo.chosenMoves[^1].moveName;

        // Build combo signature: "Kick|Punch|Hook"
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < currentCombo.chosenMoves.Count; i++)
        {
            sb.Append(currentCombo.chosenMoves[i].moveName);
            if (i < currentCombo.chosenMoves.Count - 1)
                sb.Append("|");
        }
        string comboSignature = sb.ToString();

        // QTE results for this combo
        List<QTEResult> qtes = new List<QTEResult>(qteResults);

        // --- Update each enemy ---
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.runtime == null)
                continue;

            bool isTarget = (enemy == currentTarget);

            UpdateEnemyPattern(enemy.runtime.pattern, opener, finisher, comboSignature, qtes, isTarget);
        }
    } */

    private void FeedPatternToEnemies()
    {
        foreach (var enemy in activeEnemies)
        {
            var pattern = enemy.runtime.pattern;
            var personality = enemy.runtime.personality;
            string name = enemy.GetEnemyName();

            // 1. Record opener
            if (currentCombo.chosenMoves.Count > 0)
            {
                string opener = currentCombo.chosenMoves[0].moveName;
                pattern.recentOpeners.Add(opener);
            }

            // 2. Record finisher
            if (currentCombo.chosenMoves.Count > 0)
            {
                string finisher = currentCombo.chosenMoves[^1].moveName;
                pattern.recentFinishers.Add(finisher);
            }

            // 3. Record combo signature
            string signature = string.Join("→", currentCombo.chosenMoves.ConvertAll(m => m.moveName));
            pattern.recentCombos.Add(signature);

            // 4. Increase move predictability
            foreach (var move in currentCombo.chosenMoves)
            {
                if (!pattern.movePredictability.ContainsKey(move.moveName))
                    pattern.movePredictability[move.moveName] = 0f;

                pattern.movePredictability[move.moveName] += 1f * personality.awarenessGainMultiplier;
            }

            // 5. Awareness gain
            float awarenessGain = 5f; // base gain per combo
            pattern.AddAwareness(
                QTEResult.Good,                 // treat combo as a neutral awareness event
                enemy.runtime.synergyScore,     // synergy influence
                fakeoutHit: false,              // no fakeout here
                counterTriggered: false,        // no counter here
                personality,
                name
            );

            // 6. If awareness crosses threshold → prediction rises
            if (pattern.awarenessMeter >= personality.awarenessThreshold)
            {
                float predictionGain = 5f; // base prediction gain
                pattern.AddPrediction(predictionGain, personality, name);
            }

            // 7. Trim history
            pattern.TrimHistory();
        }
    }

    private void UpdateEnemyPattern(
        EnemyRuntimeData.PatternTracker pattern,
        string opener,
        string finisher,
        string comboSignature,
        List<QTEResult> qtes,
        bool isTarget)
    {
        float globalGain = 10f;   // all enemies learn this much
        float localGain  = 15f;   // targeted enemy learns faster

        float gain = isTarget ? localGain : globalGain;

        // --- MOVE SPAM TRACKING ---
        if (!pattern.movePredictability.ContainsKey(opener))
            pattern.movePredictability[opener] = 0f;

        pattern.movePredictability[opener] += gain;

        // Decay all other moves slightly
        foreach (var key in new List<string>(pattern.movePredictability.Keys))
        {
            if (key != opener)
                pattern.movePredictability[key] = Mathf.Max(0f, pattern.movePredictability[key] - gain * 0.5f);
        }

        // --- STRUCTURAL PATTERNS ---
        pattern.recentOpeners.Add(opener);
        pattern.recentFinishers.Add(finisher);
        pattern.recentCombos.Add(comboSignature);

        pattern.TrimHistory();

        // --- QTE TIMING ---
        foreach (var r in qtes)
            pattern.recentQteResults.Add(r);

        pattern.TrimHistory();

        // --- CALCULATE SCORES ---
        pattern.moveSpamScore       = CalculateMoveSpam(pattern);
        pattern.openerRepeatScore   = CalculateRepeatScore(pattern.recentOpeners);
        pattern.finisherRepeatScore = CalculateRepeatScore(pattern.recentFinishers);
        pattern.comboRepeatScore    = CalculateRepeatScore(pattern.recentCombos);
        pattern.qtePredictableScore = CalculateQTEPredictability(pattern.recentQteResults);

        // --- UPDATE METERS ---
        float awarenessGain =
            pattern.moveSpamScore * 0.5f +
            pattern.openerRepeatScore * 0.3f +
            pattern.finisherRepeatScore * 0.3f +
            pattern.comboRepeatScore * 0.4f +
            pattern.qtePredictableScore * 0.4f;

        awarenessGain *= gain * 0.1f; // scale down to reasonable growth

        pattern.awarenessMeter += awarenessGain;

        // Enter prediction mode
        if (pattern.awarenessMeter >= 100f)
        {
            pattern.predictionMeter += awarenessGain * 1.5f;
        }

        // Decay if player is unpredictable
        if (pattern.moveSpamScore < 0.2f &&
            pattern.openerRepeatScore < 0.2f &&
            pattern.finisherRepeatScore < 0.2f &&
            pattern.comboRepeatScore < 0.2f)
        {
            pattern.awarenessMeter = Mathf.Max(0f, pattern.awarenessMeter - 5f);
            pattern.predictionMeter = Mathf.Max(0f, pattern.predictionMeter - 10f);
        }
    }

    private float CalculateMoveSpam(EnemyRuntimeData.PatternTracker p)
    {
        if (p.movePredictability.Count == 0)
            return 0f;

        float max = 0f;
        float total = 0f;

        foreach (var kv in p.movePredictability)
        {
            total += kv.Value;
            if (kv.Value > max)
                max = kv.Value;
        }

        if (total <= 0f)
            return 0f;

        return max / total; // 0–1
    }

    private float CalculateRepeatScore(List<string> list)
    {
        if (list.Count < 3)
            return 0f;

        int repeats = 0;
        for (int i = 1; i < list.Count; i++)
            if (list[i] == list[i - 1])
                repeats++;

        return (float)repeats / (list.Count - 1);
    }

    private float CalculateQTEPredictability(List<QTEResult> list)
    {
        if (list.Count < 3)
            return 0f;

        int perfects = 0;
        foreach (var r in list)
            if (r == QTEResult.Perfect)
                perfects++;

        return (float)perfects / list.Count;
    }

    /// <summary>
    /// Applies calculated combo damage to the target enemy.
    /// Handles target validation, counter-damage on miss, and enemy death.
    /// Transitions to enemy turn if enemies remain alive.
    /// References EnemyAttackController.TakeDamage() and PlayerStats.TakeDamage().
    /// </summary>
    private IEnumerator ApplyComboDamage(int damage)
    {
        // ============================
        // 1. APPLY NON-DAMAGE MOVE EFFECTS
        // ============================
        foreach (var move in currentCombo.chosenMoves)
        {
            if (move == null) continue;

            switch (move.moveType)
            {
                case AttackMove.MoveType.Heal:
                    PlayerStats.Instance.Heal(move.effectPower);
                    //UnityDebug.Log($"[MOVE EFFECT] {move.moveName} healed {move.effectPower} HP.");
                    break;

                case AttackMove.MoveType.BuffAttack:
                    tempAttackBuff += move.effectPower;
                    PlayerStats.Instance.attack += move.effectPower;
                    //UnityDebug.Log($"[MOVE EFFECT] {move.moveName} buffed attack by {move.effectPower}.");
                    break;

                case AttackMove.MoveType.BuffDefense:
                    tempDefenseBuff += move.effectPower;
                    PlayerStats.Instance.defense += move.effectPower;
                    //UnityDebug.Log($"[MOVE EFFECT] {move.moveName} buffed defense by {move.effectPower}.");
                    break;

                case AttackMove.MoveType.DebuffEnemy:
                    if (currentTarget != null && currentTarget.runtime != null)
                    {
                        currentTarget.runtime.defense -= move.effectPower;
                        //UnityDebug.Log($"[MOVE EFFECT] {move.moveName} lowered enemy defense by {move.effectPower}.");
                    }
                    break;

                case AttackMove.MoveType.Setup:
                    tempDamageMultiplier += 0.25f;
                    //UnityDebug.Log($"[MOVE EFFECT] {move.moveName} boosted next attack by 25%.");
                    break;

                case AttackMove.MoveType.Utility:
                    // Placeholder for special effects (stun, cleanse, shield, etc.)
                    //UnityDebug.Log($"[MOVE EFFECT] {move.moveName} triggered a utility effect (implement custom logic).");
                    break;
            }
        }

        // ============================
        // 2. APPLY DAMAGE
        // ============================
        //UnityDebug.Log($"[BattleManager] Applying {damage} damage to target.");

        if (currentTarget == null || currentTarget.IsDead())
        {
            currentTarget = GetFirstLivingEnemy();
            if (currentTarget == null)
            {
                //UnityDebug.LogWarning("[BattleManager] No valid target to apply damage.");
                yield return ShowMessage("No valid target.");
                yield break;
            }
            //UnityDebug.Log($"[BattleManager] Auto-selected target: {currentTarget.GetEnemyName()}");
        }

        currentTarget.TakeDamage(damage);
        yield return ShowMessage($"You deal {damage} to {currentTarget.GetEnemyName()}!");

        // ============================
        // 3. COUNTERATTACK ON MISS
        // ============================
        if (comboEndedByMiss)
        {
            comboEndedByMiss = false;
            AttackMove missedMove = missedMoveForCounter;

            int counterDamage = Mathf.RoundToInt(currentTarget.runtime.counteratk * missedMove.slotCost * Random.Range(0.8f, 1.3f));
            PlayerStats.Instance.TakeDamage(counterDamage);
            yield return ShowMessage($"{currentTarget.GetEnemyName()} counters for {counterDamage} damage!");

            if (PlayerStats.Instance.currentHealth <= 0)
            {
                yield return ShowMessage("You were defeated...");
                GameManager.Instance.SaveGame();
                EndBattle(false);
                yield break;
            }
        }

        // ============================
        // 4. CHECK ENEMY DEATH
        // ============================
        if (currentTarget.IsDead())
        {
            yield return ShowMessage($"{currentTarget.GetEnemyName()} is defeated!");
            currentTarget = GetFirstLivingEnemy();
        }

        bool anyAlive = false;
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            var e = activeEnemies[i];
            if (e != null && !e.IsDead())
            {
                anyAlive = true;
                break;
            }
        }

        if (!anyAlive)
        {
            EndBattle(true);
            yield break;
        }

        if (targetIndicatorInstance != null)
            targetIndicatorInstance.SetActive(false);

        // ============================
        // 5. ADVANCE TURN
        // ============================
        if (!battleOver)
            AdvanceTurnUnified();
    }

    /// <summary>
    /// Finds the first alive enemy in the activeEnemies list.
    /// Used for target validation and auto-selection when current target is invalid.
    /// </summary>
    private EnemyAttackController GetFirstLivingEnemy()
    {
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            var e = activeEnemies[i];
            if (e != null && !e.IsDead())
                return e;
        }

        return null;
    }

    // ====================================================
    // Enemy Turn Orchestration
    // ====================================================
    /// <summary>
    /// Orchestrates the complete enemy turn cycle.
    /// Each active enemy executes their turn via EnemyAttackController.DoEnemyTurn().
    /// Updates party context (index, size) and memory decay at turn end.
    /// Returns control to player turn if all enemies survive.
    /// References EnemyAttackController.DoEnemyTurn() and EnemyRelationshipMemory.DecayShortTerm().
    /// </summary>
    private IEnumerator EnemyTurn()
    {
        RefreshActiveEnemies();
        UnityDebug.Log(
            $"[ENEMY TURN] ENTER | " +
            $"enemyTurnRunning={enemyTurnRunning} | " +
            $"currentTurn={currentTurn} | " +
            $"patternIndex={turnPatternIndex}"
        );

        //UnityDebug.Log($"[EnemyTurn] ENTER. enemyTurnRunning={enemyTurnRunning}, battleInitialized={battleInitialized}, currentTurn={currentTurn}, battleOver={battleOver}, activeEnemies={activeEnemies.Count}");

        if (enemyTurnRunning)
        {
            //UnityDebug.Log("[EnemyTurn] EXIT: enemyTurnRunning was TRUE.");
            yield break;
        }

        enemyTurnRunning = true;

        try
        {
            // ============================
            // INNER TRY/CATCH (NO YIELDS)
            // ============================
            bool abortEarly = false;

            try
            {
                if (!battleInitialized)
                {
                    //UnityDebug.Log("[EnemyTurn] EXIT: battleInitialized == FALSE.");
                    abortEarly = true;
                }

                if (currentTurn != Turn.Enemy || battleOver)
                {
                    //UnityDebug.Log($"[EnemyTurn] EXIT: currentTurn != Enemy (currentTurn={currentTurn}).");
                    abortEarly = true;
                }
            }
            catch (System.Exception ex)
            {
                //UnityDebug.LogError("[EnemyTurn] PRE-YIELD EXCEPTION:\n" + ex);
                abortEarly = true;
            }

            if (abortEarly)
                yield break;

            // ============================
            // SAFE ZONE — YIELDS ALLOWED
            // ============================

            //UnityDebug.Log("[EnemyTurn] PASSED ALL GUARDS — starting enemy phase.");

            turnCounter++;
            synergyChain.Reset();
            currentEnemyActing = null;

            isBusy = true;
            yield return StartCoroutine(ShowMessage("Enemy party's turn..."));

            if (turnMeterBar != null)
            {
                turnMeterBar.gameObject.SetActive(true);
                turnMeterBar.value = (float)turnMeterCurrent / turnMeterMax; // ← FIX
            }
                
            // Party context
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var enemy = activeEnemies[i];
                if (enemy != null && enemy.runtime != null)
                {
                    enemy.runtime.partyIndex = i;
                    enemy.runtime.partySize = activeEnemies.Count;
                }
            }

            if (activeEnemies.Count == 0)
            {
                //UnityDebug.Log("[EnemyTurn] No active enemies left. Skipping enemy slot.");
                AdvanceTurnUnified(skipEnemy: true);
                yield break;
            }

            int turnIndex = 1;

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var controller = activeEnemies[i];

                if (controller == null || controller.IsDead())
                {
                    //UnityDebug.Log($"[BattleManager] Removing dead enemy from active list: {controller?.GetEnemyName()}");
                    activeEnemies.Remove(controller);
                    i--;
                    continue;
                }

                currentEnemyActing = controller;

                //UnityDebug.Log($"[BattleManager] Enemy turn {turnIndex}: {controller.GetEnemyName()}");
                yield return StartCoroutine(controller.DoEnemyTurn(this));

                currentEnemyActing = null;

                if (battleOver)
                {
                    //UnityDebug.Log("[EnemyTurn] battleOver TRUE → Ending battle cleanly.");
                    isBusy = false;

                    // Hide UI
                    if (turnMeterBar != null)
                        turnMeterBar.gameObject.SetActive(false);

                    // DO NOT advance turn — battle is over
                    yield break;
                }

                if (turnMeterCurrent >= turnMeterMax)
                {
                    AdvanceTurnUnified();
                    isBusy = false;
                    targetLocked = false;
                    currentTarget = null;

                    if (targetIndicatorInstance != null)
                        targetIndicatorInstance.SetActive(false);

                    if (turnMeterBar != null)
                        turnMeterBar.gameObject.SetActive(false);

                    yield break;
                }

                turnIndex++;
            }

            /* if (turnIndex == 1)
            {
                //UnityDebug.Log("[EnemyTurn] No enemies acted this phase. Skipping enemy slot.");
                AdvanceTurnUnified(skipEnemy: true);
                yield break;
            } */

            // Memory decay
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var enemy = activeEnemies[i];
                if (enemy != null && enemy.runtime != null)
                    enemy.runtime.memory.DecayShortTerm(turnCounter);
            }

            // End of enemy phase
            AdvanceTurnUnified();
            isBusy = false;
            targetLocked = false;
            currentTarget = null;

            if (targetIndicatorInstance != null)
                targetIndicatorInstance.SetActive(false);

            if (turnMeterBar != null)
                turnMeterBar.gameObject.SetActive(false);

            UpdateButtonState();
        }
        finally
        {
            enemyTurnRunning = false;
            UnityDebug.Log(
                $"[ENEMY TURN] EXIT | " +
                $"enemyTurnRunning={enemyTurnRunning} | " +
                $"currentTurn={currentTurn} | " +
                $"patternIndex={turnPatternIndex}"
            );

            //UnityDebug.Log("[EnemyTurn] FINALLY → enemyTurnRunning reset to FALSE.");
        }

        // Safety: re-call AdvanceTurnUnified if turn is still Enemy
        /* if (currentTurn == Turn.Enemy && !enemyTurnRunning && !battleOver)
        {
            //UnityDebug.Log("[EnemyTurn] Re-calling AdvanceTurnUnified to continue enemy phase.");
            AdvanceTurnUnified();
        } */
    }

    private Turn GetNextTurn()
    {
        return turnPattern[turnPatternIndex];
    }

    private void AdvanceTurnUnified(bool skipEnemy = false)
    {
        UnityDebug.Log(
            $"[TURN] AdvanceTurnUnified() called | " +
            $"skipEnemy={skipEnemy} | " +
            $"currentTurn={currentTurn} | " +
            $"patternIndex={turnPatternIndex} | " +
            $"patternCount={turnPattern.Count} | " +
            $"enemyTurnRunning={enemyTurnRunning}"
        );

        Turn previousTurn = currentTurn;
        // ============================================================
        // SAFETY GUARDS
        // ============================================================
        if (!battleInitialized)
        {
            UnityDebug.Log("[TURN] AdvanceTurnUnified called before initialization. Aborting.");
            return;
        }

        if (battleOver)
        {
            UnityDebug.Log("[TURN] AdvanceTurnUnified called but battleOver = TRUE. Aborting.");
            return;
        }

        /* UnityDebug.Log($"[TURN] AdvanceTurnUnified called. " +
                        $"skipEnemy={skipEnemy}, " +
                        $"currentTurn={currentTurn}, " +
                        $"patternIndex={turnPatternIndex}, " +
                        $"meter={turnMeterCurrent}/{turnMeterMax}"); */

        // ============================================================
        // TURN METER OVERRIDE
        // ============================================================
        if (turnMeterCurrent >= turnMeterMax)
        {
            UnityDebug.Log("[TURN] TURN METER FULL → Interrupting enemy phase and jumping to PLAYER.");

            //turnMeterCurrent = 0;

            int safety = 0;
            while (turnPattern[turnPatternIndex] != Turn.Player && safety < turnPattern.Count)
            {
                turnPatternIndex = (turnPatternIndex + 1) % turnPattern.Count;
                safety++;
            }

            UnityDebug.Log($"[TURN] Meter jump complete. New patternIndex={turnPatternIndex}, nextTurn=Player");

            currentTurn = Turn.Player;
            UpdateButtonState();
            StartPlayerTurn();
            return;
        }

        // ============================================================
        // NORMAL PATTERN ADVANCE
        // ============================================================
        int oldIndex = turnPatternIndex;
        turnPatternIndex = (turnPatternIndex + 1) % turnPattern.Count;

        UnityDebug.Log($"[TURN] Normal pattern advance: {oldIndex} → {turnPatternIndex}");

        // ============================================================
        // OPTIONAL: SKIP ENEMY SLOTS
        // ============================================================
        if (skipEnemy)
        {
            UnityDebug.Log("[TURN] skipEnemy = TRUE → skipping enemy slots");

            int safety = 0;
            while (turnPattern[turnPatternIndex] == Turn.Enemy && safety < turnPattern.Count)
            {
                //UnityDebug.Log($"[TURN] Skipping enemy at index {turnPatternIndex}");
                turnPatternIndex = (turnPatternIndex + 1) % turnPattern.Count;
                safety++;
            }

            UnityDebug.Log($"[TURN] skipEnemy complete. New patternIndex={turnPatternIndex}");
        }

        // ============================================================
        // FINAL TURN DECISION
        // ============================================================
        currentTurn = turnPattern[turnPatternIndex];

        //UnityDebug.Log($"[TURN] Final decision → currentTurn={currentTurn}, patternIndex={turnPatternIndex}");

        // Only update UI if the turn actually changed
        if (currentTurn != previousTurn)
            UpdateButtonState();

        if (currentTurn == Turn.Player)
        {
            //UnityDebug.Log("[TURN] Starting PLAYER turn.");
            StartPlayerTurn();
        }
        else
        {
            //UnityDebug.Log("[TURN] Starting ENEMY turn.");

            if (!enemyTurnRunning)
            {
                StartCoroutine(EnemyTurn());
            }
            else
            {
                //UnityDebug.Log("[TURN] Enemy turn already running — skipping duplicate call.");
            }
        }
    }

    private void RefreshActiveEnemies()
    {
        activeEnemies.RemoveAll(e => e == null || e.IsDead());
    }

    /// <summary>
    /// Sets the target for the player's next combo attack.
    /// Locked once combo selection begins (targetLocked prevents changes during combat).
    /// Updates target indicator position and UI state.
    /// </summary>
    public void SetCurrentTarget(EnemyAttackController enemy)
    {
        if (targetLocked || currentTurn != Turn.Player || isBusy || battleOver)
            return;

        currentTarget = enemy;
        if (targetIndicatorInstance != null)
        {
            targetIndicatorInstance.SetActive(true);
            var pos = enemy.transform.position;
            pos.y -= 0.4f;
            targetIndicatorInstance.transform.position = pos;
        }

        //UnityDebug.Log($"[BattleManager] Targeting {enemy.GetEnemyName()}");
        UpdateButtonState();
    }

    /// <summary>
    /// Called by EnemyAttackController when an enemy finishes their turn.
    /// Checks player death condition and updates button states.
    /// </summary>
    public void EndEnemyTurn()
    {
        if (battleOver) return;
        UnityDebug.Log(
            $"[ENEMY END] EndEnemyTurn() | " +
            $"currentTurn={currentTurn} | " +
            $"patternIndex={turnPatternIndex}"
        );

        // Check if player was defeated during enemy turn
        if (PlayerStats.Instance.currentHealth <= 0)
        {
            ShowBattleMessage("You were defeated...");
            EndBattle(false);
            return;
        }

        // Reset ally-death flag AFTER this enemy has reacted to it
        if (currentEnemyActing != null && currentEnemyActing.runtime != null)
        {
            currentEnemyActing.runtime.memory.lastAllyDied = false;
            currentEnemyActing.runtime.memory.lastAllyDiedID = null;
        }

        UpdateButtonState();
    }

    // ====================================================
    // Run Away & Battle End
    // ====================================================

    /// <summary>
    /// Handles the player attempting to run from battle.
    /// Applies enemy cooldown to prevent immediate re-encounter.
    /// References GameManager.cs and BattleData for cooldown configuration.
    /// </summary>
    public void OnRunButton()
    {
        if (battleOver) return;

        battleOver = true;
        isBusy = true;
        UpdateButtonState();

        StartCoroutine(RunAway());
    }

    /// <summary>
    /// Coroutine handling the flee sequence.
    /// Restores player control, applies enemy cooldown, and transitions back to overworld.
    /// References GameManager.cs for world state and EnemyScript for cooldown application.
    /// </summary>
    private IEnumerator RunAway()
    {
        yield return StartCoroutine(ShowMessage("You ran away!"));

        var player = GetCachedPlayer();
        if (player != null)
        {
            // Use cached controller to avoid GetComponent call (OPTIMIZATION)
            if (cachedPlayerController == null)
                cachedPlayerController = player.GetComponent<ExamplePlayerController>();

            if (cachedPlayerController != null)
            {
                // Re-enable player movement; references ExamplePlayerController.cs
                cachedPlayerController.movementLocked = false;
                cachedPlayerController.EnableInput(true);
                //UnityEngine.Debug.Log("[BattleManager] Player movement unlocked after running away.");
            }
        }

        // Store which enemy we ran from so overworld can apply cooldown
        GameManager.Instance.playerLastBattledID = enemyID;
        GameManager.Instance.runAwayTriggered = true;

        if (player != null)
            GameManager.Instance.RunFromBattle(player.transform.position);
    }

    /// <summary>
    /// Concludes the battle and processes victory/defeat outcomes.
    /// On victory: applies bounty, marks enemy as defeated, triggers level-up if applicable.
    /// On defeat: initiates respawn sequence with checkpoint restoration.
    /// References PlayerStats.cs, GameManager.cs, and LevelUpUI for outcome handling.
    /// </summary>
    public void EndBattle(bool playerWon)
    {
        if (battleOver) return;
        battleOver = true;
        isBusy = true;

        // Stop ONLY battle-related coroutines
        if (turnMeterRoutine != null) StopCoroutine(turnMeterRoutine);
        if (smoothMeterRoutine != null) StopCoroutine(smoothMeterRoutine);
        if (playerHpRoutine != null) StopCoroutine(playerHpRoutine);

        // STOP ALL ENEMY ACTIONS IMMEDIATELY 
        enemyTurnInterrupted = true; 
        foreach (var enemy in activeEnemies) 
        { 
            if (enemy != null) 
                enemy.ForceStopAllEnemyActions(); 
        }

        if (battleText != null)
            battleText.gameObject.SetActive(false);

        UpdateButtonState();

        var player = GetCachedPlayer();

        if (playerWon)
        {
            // Commit enemy memories on victory
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var enemy = activeEnemies[i];
                if (enemy != null && enemy.runtime != null)
                    enemy.runtime.memory.CommitLongTerm();
            }

            PlayerStats.Instance.GainBounty(bountyGainedThisBattle);
            GameManager.Instance.defeatedEnemyIDs.Add(BattleData.enemyID);
            int gained = bountyGainedThisBattle;
            bountyGainedThisBattle = 0;

            // LEVEL UP SEQUENCE MUST NOT BE INTERRUPTED
            if (PlayerStats.Instance.leveledUp)
            {
                StartCoroutine(PlayLevelUpSequence());
                return;
            }

            // Bounty progress UI
            bountyProgressUI.Show(
                gained,
                PlayerStats.Instance.currentBounty,
                PlayerStats.Instance.bountyToNextLevel
            );
            return;
        }

        if (!playerWon)
        {
            // Player defeat sequence (also must NOT be interrupted)
            StartCoroutine(HandlePlayerDeath());
            return;
        }

        if (player != null)
        {
            if (cachedPlayerController == null)
                cachedPlayerController = player.GetComponent<ExamplePlayerController>();
        
            if (cachedPlayerController != null) 
                cachedPlayerController.enabled = true;
        }
    }

    /// <summary>
    /// Handles player defeat: sets death flags and loads checkpoint/previous scene.
    /// Restores player with half health at checkpoint if available.
    /// References GameManager.cs, PlayerStats.cs, and checkpoint system.
    /// </summary>
    private IEnumerator HandlePlayerDeath()
    {
        yield return StartCoroutine(ShowMessage("You were defeated..."));

        // Flag that player died in battle
        GameManager.Instance.diedInBattle = true;
        GameManager.Instance.returningFromBattle = true;

        // Load the scene the player was in before the battle
        string sceneBeforeBattle = GameManager.Instance.sceneBeforeBattle;

        SceneManager.LoadScene(sceneBeforeBattle);

        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == sceneBeforeBattle);
        yield return null;

        // Restore player at checkpoint if one exists
        if (GameManager.Instance.hasCheckpoint)
        {
            // Restore player with half maximum health
            int halfHP = Mathf.CeilToInt(PlayerStats.Instance.MaxHealth / 2f);
            PlayerStats.Instance.currentHealth = halfHP;

            var player = GetCachedPlayer();
            if (cachedCharController == null && player != null)
                cachedCharController = player.GetComponent<CharacterController>();

            if (cachedCharController != null) cachedCharController.enabled = false;

            // Restore player position to checkpoint location; references GameManager.cs
            if (player != null)
                player.transform.position = GameManager.Instance.lastCheckpointPosition;

            if (cachedCharController != null) cachedCharController.enabled = true;

            //UnityDebug.Log("[Respawn] Player respawned at checkpoint with " + halfHP + " HP.");
        }
    }

    /// <summary>
    /// Transitions player from battle back to the overworld map.
    /// Restores player position and resets battle state.
    /// </summary>
    public void FinishBattleReturnToOverworld()
    {
        var player = GetCachedPlayer();
        if (player != null)
            player.transform.position = GameManager.Instance.playerPositionBeforeBattle;

        GameManager.Instance.returningFromBattle = true;
        string sceneBeforeBattle = GameManager.Instance.sceneBeforeBattle;
        SceneManager.LoadScene(sceneBeforeBattle);
    }

    // ====================================================
    // UI Updates & Display
    // ====================================================

    /// <summary>
    /// Updates health bar sliders for player and enemy.
    /// Synchronizes with PlayerStats.cs and BattleData for current values.
    /// </summary>

    public void UpdateHealthBars()
    {
        if (!playerHealthBar) 
            return;

        float newValue = (float)PlayerStats.Instance.currentHealth / PlayerStats.Instance.MaxHealth;

        // Only update if HP actually changed
        if (!Mathf.Approximately(newValue, lastPlayerHPValue))
        {
            float oldValue = lastPlayerHPValue < 0 ? newValue : lastPlayerHPValue;

            // Smooth animation only when needed
            if (playerHpRoutine != null)
                StopCoroutine(playerHpRoutine);

            playerHpRoutine = StartCoroutine(SmoothPlayerHP(oldValue, newValue));

            // Damage feedback
            if (newValue < oldValue)
            {
                StartCoroutine(FlashPlayerHP());
                StartCoroutine(ShakePlayerHP());
            }

            // Heal feedback
            if (newValue > oldValue)
            {
                StartCoroutine(HealGlow());
            }

            lastPlayerHPValue = newValue;
        }

        // Enemy bar update (instant)
        float enemyValue = (float)BattleData.enemyCurrentHealth / enemyMaxHealth;

        if (!Mathf.Approximately(enemyValue, lastEnemyHPValue))
        {
            enemyHealthBar.value = enemyValue;
            lastEnemyHPValue = enemyValue;
        }
    }

    /// <summary>
    /// Refreshes the enemy health display.
    /// Typically called after enemy takes damage.
    /// </summary>
    public void ShowEnemyHealth()
    {
        UpdateHealthBars();
    }

    /// <summary>
    /// Updates attack button interactability based on game state.
    /// Buttons are only active when player can take action (not busy, not dead, correct turn).
    /// </summary>
    void UpdateButtonState()
    {
        if (!battleInitialized)
            return;

        // Player can only act on their turn, with a valid target, when not busy or dead
        bool canAct =
            currentTurn == Turn.Player &&
            !isBusy &&
            !battleOver &&
            currentTarget != null;

        if (attackButton != null)
            attackButton.interactable = canAct;

        if (runButton != null)
            runButton.interactable = canAct && !BattleData.disableRunOption;

        if (ItemButton != null)
            ItemButton.interactable = canAct && !itemUsedThisTurn;
    }

    // Cache last meter value to avoid unnecessary updates
    public void UpdateTurnMeterUI()
    {
        if (!turnMeterBar)
            return;

        float newValue = (float)turnMeterCurrent / turnMeterMax;

        // Only update if value actually changed
        if (!Mathf.Approximately(newValue, lastTurnMeterValue))
        {
            turnMeterBar.value = newValue;
            lastTurnMeterValue = newValue;
        }
    }

    // Smooth meter animation coroutine (optimized)
    private IEnumerator SmoothTurnMeterChange(float startValue, float endValue)
    {
        float duration = 0.25f;
        float elapsed = 0f;

        // Only animate if values differ
        if (Mathf.Approximately(startValue, endValue))
        {
            turnMeterBar.value = endValue;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            float newValue = Mathf.Lerp(startValue, endValue, t);
            turnMeterBar.value = newValue;

            yield return null;
        }

        turnMeterBar.value = endValue;
        lastTurnMeterValue = endValue;
    }

    // Optimized drain routine (no per-frame polling)
    private IEnumerator DrainWhenVisible()
    {
        // Wait until visible, but with a safety timeout
        float timeout = 2f;
        while (!turnMeterBar.gameObject.activeSelf && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        meterDraining = true;

        float start = turnMeterBar.value;
        float end = 0f;

        if (smoothMeterRoutine != null)
            StopCoroutine(smoothMeterRoutine);

        smoothMeterRoutine = StartCoroutine(SmoothTurnMeterChange(start, end));

        yield return smoothMeterRoutine;

        meterDraining = false;
    }

    // Adjust meter with optimized smoothing
    public void AdjustTurnMeterFromEnemyQTE(QTEResult result, EnemyRuntimeData runtime)
    {
        if (meterDraining)
            return;

        int baseDelta = 0;

        switch (result)
        {
            case QTEResult.Perfect: baseDelta = +5; break;
            case QTEResult.Good:    baseDelta = +2; break;
            case QTEResult.Ok:      baseDelta = -10; break;
            case QTEResult.Miss:    baseDelta = -30; break;
        }

        float mult = 1f;

        // Personality influence
        if (runtime != null && runtime.personality != null)
        {
            var p = runtime.personality;

            switch (result)
            {
                case QTEResult.Perfect: mult *= p.meterGainMultiplier; break;
                case QTEResult.Good:    mult *= p.meterNeutralMultiplier; break;
                case QTEResult.Ok:      mult *= p.meterLossMultiplier; break;
                case QTEResult.Miss:    mult *= p.meterLossMultiplier; break;
            }
        }

        // Stat influence
        if (runtime != null)
        {
            float hpPercent = (float)runtime.currentHP / runtime.info.maxHP;

            if (runtime.info.attack >= 20)
                mult *= 1.2f;

            if (runtime.info.defense >= 20 && baseDelta < 0)
                mult *= 0.8f;

            if (hpPercent <= 0.3f && baseDelta > 0)
                mult *= 1.3f;

            if (hpPercent >= 0.8f && baseDelta < 0)
                mult *= 1.2f;
        }

        float playerMult = PlayerStats.Instance.GetTurnControlMultiplier();

        if (baseDelta > 0)
            mult *= playerMult;
        else if (baseDelta < 0)
            mult /= playerMult;

        int delta = Mathf.RoundToInt(baseDelta * mult);

        float oldValue = (float)turnMeterCurrent / turnMeterMax;

        turnMeterCurrent += delta;
        turnMeterCurrent = Mathf.Clamp(turnMeterCurrent, 0, turnMeterMax);

        float newValue = (float)turnMeterCurrent / turnMeterMax;

        if (smoothMeterRoutine != null)
            StopCoroutine(smoothMeterRoutine);

        smoothMeterRoutine = StartCoroutine(SmoothTurnMeterChange(oldValue, newValue));

        if (oldValue < 1f && newValue >= 1f)
            StartCoroutine(InterruptEnemyTurnFromMeter());
    }

    public IEnumerator InterruptEnemyTurnFromMeter()
    {
        if (enemyTurnInterrupted) yield break;

        enemyTurnInterrupted = true;

        foreach (var enemy in activeEnemies)
        {
            enemy.ForceStopAllEnemyActions();
        }

        //UnityDebug.Log("[TURN METER] FULL! Interrupting enemy turn and giving player a turn.");

        turnMeterCurrent = 0;
        UpdateTurnMeterUI();

        if (qteManager != null)
        {
            qteManager.ForceStopQTE();
        }

        yield return StartCoroutine(ShowMessage("You broke their rhythm!"));

        // *** FIX: FORCE PLAYER TURN ***
        AdvanceTurnUnified(skipEnemy: true);

        enemyTurnInterrupted = false;
    }

    public void AddToTurnMeterSmooth(int amount)
    {
        int oldValue = turnMeterCurrent;
        int target = Mathf.Clamp(turnMeterCurrent + amount, 0, turnMeterMax);

        turnMeterCurrent = target; // update logical value immediately

        float startNorm = (float)oldValue / turnMeterMax;
        float endNorm = (float)target / turnMeterMax;

        if (turnMeterRoutine != null)
            StopCoroutine(turnMeterRoutine);

        turnMeterRoutine = StartCoroutine(SmoothTurnMeterChange(startNorm, endNorm));
    }

    private IEnumerator SmoothPlayerHP(float start, float end, float duration = 0.25f)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = t / duration;
            playerHealthBar.value = Mathf.Lerp(start, end, lerp);
            yield return null;
        }

        playerHealthBar.value = end;
    }

    private IEnumerator FlashPlayerHP()
    {
        var img = playerHealthBar.fillRect.GetComponent<Image>();
        Color original = img.color;

        img.color = new Color(1f, 0.2f, 0.2f); // red flash
        yield return WaitCache.Get(0.12f);

        img.color = original;
    }

    private IEnumerator ShakePlayerHP(float intensity = 6f, float duration = 0.15f)
    {
        RectTransform rt = playerHealthBar.GetComponent<RectTransform>();
        Vector3 original = rt.anchoredPosition;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float offset = Mathf.Sin(t * 60f) * intensity;
            rt.anchoredPosition = original + new Vector3(offset, 0f, 0f);
            yield return null;
        }   

        rt.anchoredPosition = original;
    }

    private IEnumerator HealGlow()
    {
        var img = playerHealthBar.fillRect.GetComponent<Image>();
        Color original = img.color;

        img.color = new Color(0.2f, 1f, 0.2f); // green glow
        yield return WaitCache.Get(0.15f);

        img.color = original;
    }

    /// <summary>
    /// Searches the overworld scene for an EnemyScript with the specified ID.
    /// Used to apply cooldowns after fleeing or defeating an enemy.
    /// </summary>
    private EnemyScript FindEnemyInScene(string id)
    {
        var enemies = FindObjectsByType<EnemyScript>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            var enemy = enemies[i];
            if (enemy.enemyID == id) return enemy;
        }

        return null;
    }

    /// <summary>
    /// Called when player finishes QTE sequence.
    /// Legacy method; primary logic handled by OnQTEFinished().
    /// </summary>
    public void OnPlayerQTEComplete()
    {
        //UnityEngine.Debug.Log("[BattleManager] Player finished QTE combo!");
    }

    /// <summary>
    /// Checks if all enemies are defeated and triggers victory condition.
    /// </summary>
    public void CheckForBattleEnd()
    {
        if (battleOver)
            return;

        // Remove dead or inactive enemies from the active list
        activeEnemies.RemoveAll(e => e == null || e.IsDead() || !e.gameObject.activeInHierarchy);

        // If no enemies remain, battle is over
        if (activeEnemies.Count == 0)
        {
            EndBattle(true);
            return;
        }
    }

    public void CheckTurnMeterOverflow()
    {
        if (turnMeterCurrent >= turnMeterMax)
        {
            turnMeterCurrent = 0;

            // Apply meter punish damage
            int dmg = Mathf.RoundToInt(PlayerStats.Instance.MaxHealth * 0.10f);
            PlayerStats.Instance.TakeDamage(dmg);

            ShowBattleMessage($"You lost control! Took {dmg} damage!");
            UpdateHealthBars();

            // Skip enemy turn
            enemyTurnInterrupted = true;
        }
    }

    /// <summary>
    /// Displays a message in the battle UI text and waits before clearing.
    /// Uses cached WaitForSeconds to minimize GC allocations.
    /// </summary>
    public IEnumerator ShowMessage(string msg)
    {
        battleText.text = msg;
        UpdateHealthBars();
        yield return messageDelay; // already cached
    }

    /// <summary> 
    /// Shows a battle message for the default duration. 
    /// </summary> 
    public void ShowBattleMessage(string msg) 
    { 
        if (messageRoutine != null) 
            StopCoroutine(messageRoutine); 
            
        messageRoutine = StartCoroutine(ShowMessageRoutine(msg)); 
    }

    /// <summary> 
    /// Shows a battle message for a custom duration. 
    /// </summary> 
    public void ShowBattleMessage(string msg, float duration) 
    { 
        if (messageRoutine != null) 
            StopCoroutine(messageRoutine); 
            
        messageRoutine = StartCoroutine(ShowMessageRoutine(msg, duration)); 
    }

    /// <summary> 
    /// Coroutine for default-duration messages. 
    /// </summary> 
    private IEnumerator ShowMessageRoutine(string msg) 
    { 
        battleText.text = msg; yield return defaultMessageDelay; 
        battleText.text = ""; 
    }

    /// <summary> 
    /// Coroutine for custom-duration messages. 
    /// /// </summary> 
    private IEnumerator ShowMessageRoutine(string msg, float duration) 
    { 
        battleText.text = msg; yield return new WaitForSeconds(duration); 
        battleText.text = ""; 
    }

    /// <summary>
    /// Adds bounty earned to the current battle total.
    /// Accumulated bounty is applied to player on victory via PlayerStats.GainBounty().
    /// </summary>
    public void AddBattleBounty(int amount)
    {
        bountyGainedThisBattle += amount;
        //UnityDebug.Log("[BattleManager] Added bounty: " + amount);
    }

    // ====================================================
    // Level Up Sequence
    // ====================================================

    /// <summary>
    /// Smoothly pans battle camera from enemy position toward player.
    /// Used during level-up UI display for cinematic effect.
    /// References battleCamera set during FindBattleCameraDelayed().
    /// </summary>
    public IEnumerator LevelUpCameraShiftToPlayer()
    {
        if (battleCamera == null)
        {
            //UnityDebug.LogError("[LevelUpCameraShift] battleCamera is NULL!");
            yield break;
        }

        var playerTransform = GetCachedPlayer()?.transform;
        if (playerTransform == null)
        {
            //UnityDebug.LogError("[LevelUpCameraShift] Player not found!");
            yield break;
        }

        Vector3 startPos = battleCamera.transform.position;
        // Pan camera slightly above and behind player
        Vector3 targetPos = playerTransform.position + new Vector3(0, 1.5f, -2f);

        // Smooth interpolation over approximately 0.67 seconds
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            battleCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
    }

    /// <summary>
    /// Orchestrates the level-up display sequence and camera movement.
    /// Calls LevelUpUI.ShowLevelUp() to animate stat changes.
    /// References PlayerStats.cs for level-up data.
    /// </summary>
    public IEnumerator PlayLevelUpSequence()
    {
        //UnityDebug.Log("[LEVEL SEQ] PlayLevelUpSequence started. pendingRankUp=" + PlayerStats.Instance.pendingRankUp);
        var stats = PlayerStats.Instance;

        // Cache old stats before level-up is processed
        int oldHP = stats.oldMaxHealth;
        int oldATK = stats.oldAttack;
        int oldDEF = stats.oldDefense;
        int oldcounteratk = stats.oldCounteratk;
        int oldMaxslots = stats.oldMaxslots;
        int oldTurnControl = stats.oldTurnControl;
        int oldCriticalchance = stats.oldCriticalchance;

        // Pan camera to player and display level-up UI; calls LevelUpUI from this method
        yield return StartCoroutine(LevelUpCameraShiftToPlayer());
        yield return StartCoroutine(levelUpUI.ShowLevelUp(stats, oldHP, oldATK, oldDEF, oldcounteratk, oldMaxslots, oldTurnControl, oldCriticalchance));
    }

    public void RemoveEnemyFromActiveList(EnemyAttackController enemy)
    {
        if (activeEnemies.Contains(enemy))
            activeEnemies.Remove(enemy);
    }

    // ====================================================
    // Helper Methods
    // ====================================================
    /// <summary>
    /// Returns cached player GameObject or performs a fresh tag-based search.
    /// Minimizes expensive FindWithTag calls by caching the result.
    /// </summary>
    private GameObject GetCachedPlayer()
    {
        if (cachedPlayer == null)
            cachedPlayer = GameObject.FindWithTag("Player");
        return cachedPlayer;
    }

    [System.Serializable]
    public class SynergyChain
    {
        public bool defTargetedLastTurn;
        public bool hpOrAtkTargetedLastTurn;

        public void Reset()
        {
            defTargetedLastTurn = false;
            hpOrAtkTargetedLastTurn = false;
        }
    }

    public static class WaitCache
    {
        private static readonly Dictionary<float, WaitForSeconds> cache = new();

        public static WaitForSeconds Get(float t)
        {
            if (!cache.TryGetValue(t, out var w))
                cache[t] = w = new WaitForSeconds(t);
            return w;
        }
    }

    private void OnDestroy()
    {
        if(qteManager != null)
            qteManager.OnQTECompleted -= OnQTEFinished;
    }

}