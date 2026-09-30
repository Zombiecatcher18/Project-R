using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;
using System; // for efficient combo logging

/// <summary>
/// Manages enemy turn execution, attacks, QTE interactions, and personality-driven behavior.
/// Instantiated by EnemyPartySpawner for each enemy in battle.
/// Works with EnemyBrain.cs for personality-based decision making.
/// Receives QTE results from BattleManager.OnQTEFinished().
/// </summary>
public class EnemyAttackController : MonoBehaviour
{
    /// <summary>Reference to QTE system; set in Awake() via singleton or type search.</summary>
    private QTEManager qteManager;
    /// <summary>Static enemy data (maxHP, moves, personality profile).</summary>
    private EnemyInfo data;
    /// <summary>Current HP of this enemy instance; separate from EnemyInfo for battle mutations.</summary>
    private int currentHealth;
    /// <summary>UI slider showing enemy health bar in battle.</summary>
    public Slider healthBar;

    /// <summary>Runtime instance data for this specific battle; includes stats, memory, personality adaptation.</summary>
    internal EnemyRuntimeData runtime;
    /// <summary>Result of the most recent QTE; used for damage calculation in QTE attacks.</summary>
    private QTEResult lastQTEResult = QTEResult.Miss;
    /// <summary>Flag indicating if current QTE attack sequence has completed.</summary>
    private bool qteCompleted = false;
    /// <summary>Brain component for personality-based attack decisions; references EnemyBrain.cs.</summary>
    private EnemyBrain brain;

    private PersonalityBrain personalityBrain;

    // ===================================================
    // Fakeout System (Cinematic Enemy Feints)
    // ===================================================
    
    /// <summary>Timestamp of last fakeout execution; used for cooldown management.</summary>
    private float lastFakeoutTime = -999f;
    /// <summary>Cooldown duration between fakeout attacks in seconds.</summary>
    private float fakeoutCooldown = 2f;
    /// <summary>Count of fakeouts performed this turn; capped by maxFakeoutsPerTurn.</summary>
    private int fakeoutsThisTurn = 0;
    /// <summary>Maximum number of fakeouts allowed per enemy turn.</summary>
    private int maxFakeoutsPerTurn = 3;
    /// <summary>Result type for fakeout QTE (usually Miss for cinematic effect).</summary>
    private QTEResult fakeoutResult = QTEResult.None;


    // ===================================================
    // Performance Optimization
    // ===================================================
    
    /// <summary>Reusable AttackMove instance for QTE attacks; avoids allocation per attack.</summary>
    private AttackMove tempQteMove;
    /// <summary>Reusable AttackMove instance for fakeout attacks; avoids allocation per attack.</summary>
    private AttackMove tempFakeoutMove;

    private EnemyMove currentActingMove;

    private Coroutine hpRoutine;

    /// <summary>
    /// Caches QTEManager reference; called by Unity before Start().
    /// </summary>
    private void Awake()
    {
        qteManager = QTEManager.Instance;
        if (qteManager == null)
            qteManager = FindObjectOfType<QTEManager>(); // Fallback once, not per use
    }

    // ===================================================
    // Initialization & Setup
    // ===================================================
    
    /// <summary>
    /// Initializes enemy with runtime data from battle spawner.
    /// Caches health, personality, and creates reusable AttackMove instances.
    /// Called by EnemyPartySpawner.cs after instantiation.
    /// References EnemyRuntimeData, EnemyBrain, and EnemyInfo.
    /// </summary>
    public void Initialize(EnemyRuntimeData data)
    {
        runtime = data;
        this.data = runtime.info;
        healthBar = GetComponentInChildren<Slider>();
        if (brain == null)
            brain = GetComponent<EnemyBrain>();

        runtime.personality = runtime.info.personality;
        runtime.brain = brain;
        runtime.currentHP = runtime.info.maxHP;
        runtime.maxComboSlots = runtime.info.GetComboSlotsForLevel(runtime.enemyLevel);

        personalityBrain = new PersonalityBrain(runtime);

        if (healthBar != null)
        {
            healthBar.minValue = 0;
            healthBar.maxValue = runtime.info.maxHP;
            healthBar.value = runtime.currentHP;
        }

        // Create reusable AttackMove instances for QTE/fakeout to avoid allocation per attack
        if (tempQteMove == null)
            tempQteMove = ScriptableObject.CreateInstance<AttackMove>();
        if (tempFakeoutMove == null)
            tempFakeoutMove = ScriptableObject.CreateInstance<AttackMove>();

        /*Debug.Log(
            $"[PersonalityInit] EnemyInfo = {runtime.info.name}, " +
            $"Personality = {runtime.personality?.name ?? "NULL"}, " +
            $"Type = {runtime.personality?.personalityType}"
        ); */

        //Debug.Log($"[EnemyInit] {GetEnemyName()} active={gameObject.activeSelf}, HP={runtime.currentHP}");
    }

    /// <summary>
    /// Checks if this enemy has been defeated (HP <= 0).
    /// </summary>
    public bool IsDead() => runtime.currentHP <= 0;

    /// <summary>Gets current health value.</summary>
    public int GetCurrentHealth() => currentHealth;
    /// <summary>Gets maximum health value from EnemyInfo.</summary>
    public int GetMaxHealth() => data.maxHP;

    // ---------------------------------------------------------
    // DAMAGE HANDLING
    // ---------------------------------------------------------
    public void TakeDamage(int amount)
    {
        float reduction = 100f / (100f + runtime.info.defense);
        int effectiveDamage = Mathf.RoundToInt(amount * reduction);

        runtime.currentHP -= effectiveDamage;
        if (runtime.currentHP < 0) runtime.currentHP = 0;

        if (healthBar != null)
        {
            float start = healthBar.value;
            float end = runtime.currentHP;

            if (hpRoutine != null)
                StopCoroutine(hpRoutine);

            hpRoutine = StartCoroutine(SmoothEnemyHP(start, end));
        }   

        //Debug.Log($"[EnemyAttackController] {GetEnemyName()} took {effectiveDamage} damage.");
        if (IsDead())
        {
            // Notify overworld systems
            EnemyManager.Instance.MarkDefeated(runtime.info.enemyID, 0f);
            QuestRuntime.Instance.MarkEnemyDefeated(runtime.info.enemyID);
            Debug.Log("[Battle] Marked defeated: " + BattleData.enemyID);

            foreach (var ally in BattleManager.Instances.GetActiveEnemies())
            {
                if (ally != null && ally.runtime != null && ally != this)
                {
                    ally.runtime.memory.lastAllyDied = true;
                    ally.runtime.memory.lastAllyDiedID = runtime.info.enemyID;
                }
            }

            BattleManager.Instances.AddBattleBounty(runtime.info.bountyReward);
            StopAllCoroutines();
            if (qteManager != null)
                qteManager.OnQTECompleted -= OnQTEComplete;

            gameObject.SetActive(false);
            BattleManager.Instances.RemoveEnemyFromActiveList(this);
            BattleManager.Instances.CheckForBattleEnd();
        }
    }

    // ---------------------------------------------------------
    // ENEMY TURN
    // ---------------------------------------------------------
    public IEnumerator DoEnemyTurn(BattleManager battleManager)
    {
        Debug.Log("[ENEMY TURN] Enemy turn started.");

        if (battleManager.enemyTurnInterrupted)
            yield break;

        if (IsInvalid())
            yield break;

        if (IsDead())
            yield break;

        if (!gameObject.activeInHierarchy)
            yield break;

        personalityBrain.ApplySynergyForThisTurn(battleManager.GetActiveEnemies());
        personalityBrain.ApplySynergyChainBiases(battleManager.synergyChain);
        PersonalityTurnLogic.OnTurnStart(runtime);
        personalityBrain.ApplyPatternAwareness();

        // PATTERN COUNTER CHECK (roll ONCE)
        // Prevent double counters in the same cycle
        Debug.Log("[ENEMY TURN] Checking for enemy-turn counter...");

        // Enemy-turn counters are DISABLED.
        // Counters should ONLY happen during the player's turn.

        /* if (battleManager.playerWasCountered)
        {
            battleManager.playerWasCountered = false; // reset for next turn
        }
        else
        {
            bool didCounter = TryPatternCounter(battleManager);
            if (didCounter)
                yield break;
        } */


        Debug.Log("[ENEMY TURN] No counter. Proceeding with normal attack.");

        // Enemy failed to counter → prediction confidence drops
        float decay = 10f * runtime.personality.predictionDecayMultiplier;
        float before = runtime.pattern.predictionMeter;

        runtime.pattern.predictionMeter = Mathf.Max(0f, before - decay);

        Debug.Log($"[PREDICTION-DECAY] {runtime.personality.personalityName} {before:F1} → {runtime.pattern.predictionMeter:F1}");

        fakeoutsThisTurn = 0;

        if (BattleManager.Instances.playerWasCountered)
        {
            //Debug.Log("[EnemyTurn] Skipping enemy attack because player was already countered.");
            BattleManager.Instances.playerWasCountered = false;
            yield break;
        }

        if (data == null || data.moves == null || data.moves.Count == 0)
        {
            //Debug.LogError("[EnemyAttackController] No moves found.");
            yield break;
        }

        var combo = AIComboBuilder.BuildCombo(runtime, PlayerStats.Instance);
        Debug.Log("[ENEMY TURN] Building combo...");

        // Avoid LINQ + string.Join allocations: manual logging
        if (combo != null && combo.Count > 0)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < combo.Count; i++)
            {
                sb.Append(combo[i].moveName);
                if (i < combo.Count - 1)
                    sb.Append(", ");
            }
            //Debug.Log($"[EnemyCombo] {GetEnemyName()} combo: {sb}");
        }

        Debug.Log("[ENEMY TURN] Executing combo...");

        yield return ExecuteEnemyCombo(combo, battleManager);
    }

    private IEnumerator ExecuteEnemyCombo(List<EnemyMove> combo, BattleManager battleManager)
    {
        if (combo == null)
            yield break;

        if (IsInvalid())
            yield break;

        for (int i = 0; i < combo.Count; i++)
        {
            var move = combo[i];
            if (IsDead())
                yield break;

            if (battleManager.enemyTurnInterrupted)
                yield break;

            //Debug.Log($"[EnemyCombo] Executing move: {move.moveName}");

            yield return DoEnemyAttack(move, battleManager);

            if (battleManager.enemyTurnInterrupted)
                yield break;

            personalityBrain.MarkSynergyChain(move, battleManager.synergyChain);
            SynergyEventSystem.EvaluateEvent(this, move, battleManager);
            SynergyJudgmentSystem.EvaluateJudgment(this, move, battleManager);
        }

        battleManager.EndEnemyTurn();
        PlayerSkillTracker.DecaySkill();
    }

    // ---------------------------------------------------------
    // ATTACK LOGIC
    // ---------------------------------------------------------
    public IEnumerator DoEnemyAttack(EnemyMove move, BattleManager battleManager)
{
    yield return new WaitForSeconds(0.25f);

    Debug.Log($"[FAKEOUT FLOW] === DoEnemyAttack START for {move.moveName} ===");

    if (battleManager.enemyTurnInterrupted)
    {
        Debug.Log("[FAKEOUT FLOW] Aborting: enemyTurnInterrupted BEFORE fakeout check.");
        yield break;
    }

    if (IsInvalid())
    {
        Debug.Log("[FAKEOUT FLOW] Aborting: IsInvalid() returned TRUE BEFORE fakeout check.");
        yield break;
    }

    Debug.Log($"[FAKEOUT FLOW] Checking ShouldFakeout for {move.moveName}");

    bool shouldFakeout = personalityBrain != null &&
                        personalityBrain.ShouldFakeout(
                            move,
                            lastFakeoutTime,
                            fakeoutsThisTurn,
                            maxFakeoutsPerTurn
                        );

    Debug.Log($"[FAKEOUT FLOW] ShouldFakeout() returned: {shouldFakeout} " +
              $"(lastFakeoutTime={lastFakeoutTime}, fakeoutsThisTurn={fakeoutsThisTurn}, maxFakeoutsPerTurn={maxFakeoutsPerTurn})");

    if (move.moveName == "Vine Whip" && move.hasFakeout)
    {
        Debug.Log("[FAKEOUT FLOW] Forcing fakeout for Vine Whip (override).");
        shouldFakeout = true;
    }

    Debug.Log($"[FAKEOUT FLOW] shouldFakeout AFTER overrides = {shouldFakeout}");

    if (PlayerSkillTracker.justDidPerfectCombo)
    {
        Debug.Log("[FAKEOUT FLOW] Skipping fakeout due to justDidPerfectCombo grace turn.");
        PlayerSkillTracker.justDidPerfectCombo = false;

        yield return DoRealEnemyAttack(move, battleManager);
        Debug.Log("[FAKEOUT FLOW] === DoEnemyAttack END (grace turn, no fakeout) ===");
        yield break;
    }

    if (move.fakeoutRequiresQTE && !move.hasFakeout)
    {
        Debug.LogWarning($"[FAKEOUT FLOW] {move.moveName} has fakeoutRequiresQTE but hasFakeout is FALSE.");
    }

    Debug.Log($"[FAKEOUT FLOW] Entering fakeout decision IF with shouldFakeout={shouldFakeout}");

    if (shouldFakeout)
    {
        Debug.Log($"[FAKEOUT FLOW] ENTERING FAKEOUT BRANCH for {move.moveName}");

        fakeoutsThisTurn++;
        lastFakeoutTime = Time.time;
        Debug.Log($"[FAKEOUT FLOW] fakeoutsThisTurn now = {fakeoutsThisTurn}, lastFakeoutTime = {lastFakeoutTime}");

        bool fakeoutPunished = false;
        yield return DoFakeout(move, battleManager, result =>
        {
            fakeoutPunished = result;
            Debug.Log($"[FAKEOUT FLOW] DoFakeout callback → fakeoutPunished={fakeoutPunished}");
        });

        Debug.Log($"[FAKEOUT FLOW] Returned from DoFakeout for {move.moveName}, fakeoutPunished={fakeoutPunished}");

        if (fakeoutPunished)
        {
            Debug.Log($"[FAKEOUT FLOW] Player punished by {move.moveName}, skipping real attack.");
            Debug.Log($"[FAKEOUT FLOW] === DoEnemyAttack END (fakeout punished, no real attack) ===");
            yield break;
        }
    }
    else
    {
        Debug.Log($"[FAKEOUT FLOW] Fakeout SKIPPED for {move.moveName} (shouldFakeout == false).");
    }

    Debug.Log("[FAKEOUT FLOW] Proceeding to DoRealEnemyAttack...");
    yield return DoRealEnemyAttack(move, battleManager);

    Debug.Log($"[Skill] FakeoutSkillFactor = {PlayerSkillTracker.FakeoutSkillFactor}");
    Debug.Log($"[FAKEOUT FLOW] === DoEnemyAttack END for {move.moveName} ===");
}


    // ---------------------------------------------------------
    // REAL ATTACK (NON-QTE)
    // ---------------------------------------------------------
    private IEnumerator DoRealEnemyAttack(EnemyMove move, BattleManager battleManager)
    {
        // 1. Get Tag Modifiers (Includes HitCount, Execute flags, etc.)
        var tags = move.GetTagModifiers();

        // 2. Determine how many hits are allowed based on enemy stats
        int maxHits = Mathf.Clamp(runtime.maxComboSlots, 1, 5);
        int allowedHits = Mathf.Min(tags.hitCount, maxHits);

        for (int hit = 0; hit < allowedHits; hit++)
        {
            if (battleManager.enemyTurnInterrupted) yield break;
            if (hit > 0) yield return new WaitForSeconds(0.2f); // Visual stagger

            // 3. QTE Branch
            if (move.requiresQTE)
            {
                yield return DoEnemyQTEAttack(move, battleManager);
                if (battleManager.enemyTurnInterrupted) yield break;
                continue;
            }

            // 4. Base damage
            float atkScale = 1f + (runtime.info.attack * 0.04f);
            float dmgMult = runtime.personality != null ? runtime.personality.damageMultiplier : 1f;

            if (move.isHeavy)
                dmgMult *= 1.25f;

            // 5. Diminishing returns
            float hitMult = 1f;
            if (hit == 1) hitMult = 0.7f;
            else if (hit == 2) hitMult = 0.5f;
            else if (hit >= 3) hitMult = 0.4f;

            int damage = Mathf.RoundToInt(move.baseDamage * atkScale * dmgMult * hitMult);

            // 6. Execute / AntiTank
            if (tags.isExecute)
            {
                float hpPct = (float)PlayerStats.Instance.currentHealth / PlayerStats.Instance.MaxHealth;
                if (hpPct <= 0.30f) damage = Mathf.RoundToInt(damage * 1.5f);
            }

            if (tags.isAntiTank && PlayerStats.Instance.defense >= 10)
                damage = Mathf.RoundToInt(damage * 1.3f);

            // 7. Apply damage
            if (move.ignoresDefense || move.HasTag(MoveTag.ArmorPiercing))
            {
                PlayerStats.Instance.currentHealth = Mathf.Max(0, PlayerStats.Instance.currentHealth - damage);
                yield return battleManager.ShowMessage($"ARMOR PIERCED! {move.moveName} deals {damage} true damage!");
            }   
            else
            {
                PlayerStats.Instance.TakeDamage(damage);
                yield return battleManager.ShowMessage($"{GetEnemyName()} deals {damage} damage!");
            }

            // 8. Turn meter
            if (move.isHeavy) battleManager.turnMeterCurrent += 15;
            else if (move.HasTag(MoveTag.Fast)) battleManager.turnMeterCurrent += 5;

            battleManager.UpdateHealthBars();
        }

    }

    // ============================================================
    // PATTERN-BASED COUNTER EXECUTION
    // ============================================================
    private bool TryPatternCounter(BattleManager battleManager)
    {
        var p = runtime.pattern;
        Debug.Log($"[COUNTER-ENEMY] TryPatternCounter fired during ENEMY turn. pred={p.predictionMeter}");

        // Only counter if in prediction mode
        if (p.predictionMeter < 100f)
            return false;

        // Base chance grows with prediction meter
        float baseChance = 0.15f + (p.predictionMeter - 100f) * 0.01f;

        // Add personality bias
        baseChance += runtime.counterBias;

        // Clamp
        baseChance = Mathf.Clamp01(baseChance);

        // Roll
        if (UnityEngine.Random.value > baseChance)
            return false;

        // Find a counter move
        EnemyMove counterMove = null;
        foreach (var m in runtime.info.moves)
        {
            if (m != null && m.isCounter)
            {
                counterMove = m;
                break;
            }
        }

        if (counterMove == null)
            return false;

        // Execute counter
        int dmg = personalityBrain.GetCounterDamage(counterMove);

        PlayerStats.Instance.TakeDamage(dmg);
        battleManager.ShowBattleMessage($"{GetEnemyName()} predicted your move and countered for {dmg} damage!", 3f);

        // Interrupt player turn
        battleManager.playerWasCountered = true;
        battleManager.enemyTurnInterrupted = true;
        Debug.Log($"[COUNTER-ENEMY] SUCCESS! dmg={dmg}");

        return true;
    }

    // ============================================================
    // PLAYER-TURN COUNTER (Pattern-Based)
    // ============================================================
    public bool TryPlayerTurnCounter(BattleManager battleManager)
    {
        var pattern = runtime.pattern;

        Debug.Log(
            $"[COUNTER-ROLL] {GetEnemyName()} | " +
            $"Prediction={pattern.predictionMeter:F1} | " +
            $"CounterPressure={runtime.counterPressure:F2}"
        );

        // Only consider counter if prediction is maxed
        if (pattern.predictionMeter < 100f)
            return false;

        // Increase counter pressure each player turn
        // (BattleManager should do this at StartPlayerTurn)
        float chance = runtime.counterPressure;

        // Roll
        float roll = UnityEngine.Random.value;
        if (roll > chance)
            return false;

        Debug.Log(
            $"[COUNTER-ROLL] {GetEnemyName()} | " +
            $"Prediction={pattern.predictionMeter:F1} | " +
            $"CounterPressure={runtime.counterPressure:F2}"
        );

        // SUCCESS — lock out further counters this turn
        battleManager.counterUsedThisPlayerTurn = true;
        Debug.Log($"[COUNTER-SUCCESS] {GetEnemyName()} countered the player!");

        EnemyMove counterMove = null;
        foreach (var m in runtime.info.moves)
        {
            if (m != null && m.isCounter)
            {
                counterMove = m;
                break;
            }
        }

        if (counterMove == null)
            return false;

        int dmg = personalityBrain.GetCounterDamage(counterMove);
        
        runtime.pattern.AddAwareness(
            QTEResult.Perfect,              // counters only happen on perfect reads
            runtime.synergyScore,
            fakeoutHit: false,
            counterTriggered: true,
            runtime.personality,
            GetEnemyName()
        );

        PlayerStats.Instance.TakeDamage(dmg);

        battleManager.qteManager.ForceStopQTE();
        battleManager.qteManager.OnQTECompleted -= battleManager.OnQTEFinished;

        battleManager.ShowBattleMessage($"{GetEnemyName()} predicted your move and countered for {dmg} damage!", 3f);

        battleManager.playerWasCountered = true;
        battleManager.enemyTurnInterrupted = true;

        battleManager.currentCombo = null;
        battleManager.qteResults.Clear();
        battleManager.currentMoveIndex = 0;

        // Reset prediction after counter
        pattern.predictionMeter = 0f;
        pattern.awarenessMeter = 0f;
        Debug.Log(
            $"[PREDICTION-RESET] {GetEnemyName()} reset prediction + awareness after counter."
        );

        return true;
    }

    // ---------------------------------------------------------
    // REAL QTE ATTACK
    // ---------------------------------------------------------
    private IEnumerator DoEnemyQTEAttack(EnemyMove move, BattleManager battleManager)
    {
        if (tempQteMove == null)
            tempQteMove = ScriptableObject.CreateInstance<AttackMove>();

        if (IsInvalid())
            yield break;

        // Copy enemy move data into AttackMove clone
        tempQteMove.moveName = move.moveName;
        tempQteMove.baseDamage = move.baseDamage;
        tempQteMove.qteType = move.qteType;

        /* float qteMult = runtime.personality != null ? runtime.personality.qteSpeedMultiplier : 1f;
        tempQteMove.qteSpeed = move.qteSpeed * qteMult; */
        tempQteMove.qteSpeed = personalityBrain.GetQTESpeed(move);
        tempQteMove.preDelay = personalityBrain.GetQTEPreDelay(move);

        tempQteMove.possibleButtons = move.possibleButtons;

        // THIS IS THE IMPORTANT PART 
        //tempQteMove.preDelay = move.GetEnemyButtonPreDelay();

        //Debug.Log($"[QTE] {move.moveName} QTE Speed = {tempQteMove.qteSpeed:F2}, PreDelay = {tempQteMove.preDelay:F2}");

        lastQTEResult = QTEResult.None;
        qteCompleted = false;

        qteManager.OnQTECompleted += OnQTEComplete;

        yield return battleManager.ShowMessage("Dodge!");
        qteManager.StartQTEForMove(tempQteMove, QTEManager.QTEOwner.Enemy);

        float timeout = 5f;
        while (!qteCompleted && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        qteCompleted = true; // fail-safe

        qteManager.OnQTECompleted -= OnQTEComplete;

        // stop immediately if interrupted
        if (battleManager.enemyTurnInterrupted)
            yield break;

        yield return HandleEnemyAttackResult(move, lastQTEResult, battleManager);
    }

    private void OnQTEComplete(QTEResult result)
    {
        lastQTEResult = result;
        qteCompleted = true;
    }

    // ---------------------------------------------------------
    // FAKEOUT LOGIC
    // ---------------------------------------------------------
    private IEnumerator DoFakeout(EnemyMove move, BattleManager battleManager, System.Action<bool> onFakeoutResult)
{
    Debug.Log($"[FAKEOUT FLOW] >>> DoFakeout START for {move.moveName}");

    if (battleManager.enemyTurnInterrupted)
    {
        Debug.Log("[FAKEOUT FLOW] DoFakeout abort: enemyTurnInterrupted at entry.");
        yield break;
    }

    if (IsInvalid())
    {
        Debug.Log("[FAKEOUT FLOW] DoFakeout abort: IsInvalid() at entry.");
        yield break;
    }

    if (!move.hasFakeout)
    {
        Debug.LogWarning($"[FAKEOUT FLOW] DoFakeout abort: {move.moveName} hasFakeout == FALSE.");
        onFakeoutResult(false);
        yield break;
    }

    Debug.Log($"[FAKEOUT FLOW] DoFakeout: move.fakeoutRequiresQTE={move.fakeoutRequiresQTE}");

    bool playerFellForFakeout = false;

    if (move.fakeoutRequiresQTE)
    {
        Debug.Log("[FAKEOUT FLOW] DoFakeout: Setting up tempFakeoutMove for QTE.");

        if (tempFakeoutMove == null)
        {
            tempFakeoutMove = ScriptableObject.CreateInstance<AttackMove>();
            Debug.Log("[FAKEOUT FLOW] Created new tempFakeoutMove ScriptableObject.");
        }

        tempFakeoutMove.moveName = move.moveName + " (Fake)";
        tempFakeoutMove.baseDamage = 0;

        tempFakeoutMove.qteType = move.qteType;
        tempFakeoutMove.qteSpeed = move.qteSpeed;
        tempFakeoutMove.possibleButtons = move.possibleButtons;

        tempFakeoutMove.preDelay = move.GetEnemyButtonPreDelay();
        tempFakeoutMove.buttonPreDelayMin = move.enemyButtonPreDelayMin;
        tempFakeoutMove.buttonPreDelayMax = move.enemyButtonPreDelayMax;

        Debug.Log($"[FAKEOUT FLOW] tempFakeoutMove configured: name={tempFakeoutMove.moveName}, " +
                  $"qteType={tempFakeoutMove.qteType}, qteSpeed={tempFakeoutMove.qteSpeed}, " +
                  $"preDelay={tempFakeoutMove.preDelay}, btnMin={tempFakeoutMove.buttonPreDelayMin}, " +
                  $"btnMax={tempFakeoutMove.buttonPreDelayMax}, buttons={tempFakeoutMove.possibleButtons?.Length}");

        fakeoutResult = QTEResult.None;
        bool fakeoutDone = false;

        void LocalFakeoutHandler(QTEResult r)
        {
            fakeoutResult = r;
            fakeoutDone = true;
            Debug.Log($"[FAKEOUT FLOW] LocalFakeoutHandler invoked → fakeoutResult={fakeoutResult}");
        }

        Debug.Log("[FAKEOUT FLOW] Subscribing LocalFakeoutHandler and starting StartFakeoutQTE...");
        qteManager.OnQTECompleted += LocalFakeoutHandler;
        qteManager.StartFakeoutQTE(tempFakeoutMove);

        float timeout = 5f;
        Debug.Log("[FAKEOUT FLOW] Waiting for fakeout QTE to complete...");

        while (!fakeoutDone && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!fakeoutDone)
        {
            Debug.LogWarning("[FAKEOUT FLOW] Fakeout QTE timed out without result.");
        }

        qteManager.OnQTECompleted -= LocalFakeoutHandler;
        Debug.Log($"[FAKEOUT FLOW] Fakeout QTE finished with result={fakeoutResult}, timeoutRemaining={timeout}");

        if (fakeoutResult == QTEResult.FakeoutHit)
            playerFellForFakeout = true;

        runtime.pattern.AddAwareness(
            fakeoutResult,
            runtime.synergyScore,
            fakeoutHit: fakeoutResult == QTEResult.FakeoutHit,
            counterTriggered: false,
            runtime.personality,
            GetEnemyName()
        );

        if (fakeoutResult == QTEResult.FakeoutHit)
        {
            Debug.Log("[FAKEOUT FLOW] Player FELL for fakeout → applying Miss turn meter.");
            battleManager.AdjustTurnMeterFromEnemyQTE(QTEResult.Miss, runtime);
        }
        else
        {
            Debug.Log("[FAKEOUT FLOW] Player AVOIDED fakeout → (Good) turn meter path (currently commented).");
            //battleManager.AdjustTurnMeterFromEnemyQTE(QTEResult.Good, runtime);
        }

        if (battleManager.enemyTurnInterrupted)
        {
            Debug.Log("[FAKEOUT FLOW] DoFakeout abort: enemyTurnInterrupted AFTER fakeout QTE.");
            yield break;
        }
    }
    else
    {
        Debug.Log("[FAKEOUT FLOW] DoFakeout: fakeoutRequiresQTE == FALSE (no QTE path).");
    }

    if (playerFellForFakeout)
    {
        Debug.Log("[FAKEOUT FLOW] PlayerFellForFakeout == TRUE → applying punish damage.");

        float enemyATKScale = 1f + (runtime.info.attack * 0.04f);
        float punishScale = 0.25f;
        int punishDamage = Mathf.RoundToInt(move.baseDamage * enemyATKScale * punishScale);

        Debug.Log($"[FakeoutPunish] Damage = {punishDamage}");

        PlayerStats.Instance.TakeDamage(punishDamage);
        yield return battleManager.ShowMessage($"{move.moveName} punishes you for {punishDamage} damage!");

        if (PlayerStats.Instance.currentHealth <= 0)
        {
            Debug.Log("[FAKEOUT FLOW] Player died from fakeout punish.");
            yield return battleManager.ShowMessage("You were defeated...");
            battleManager.EndBattle(false);
        }

        onFakeoutResult(true);
        Debug.Log("[FAKEOUT FLOW] >>> DoFakeout END (punished = TRUE) <<<");
        yield break;
    }

    Debug.Log($"[Fakeout] Player avoided fakeout → Result:{fakeoutResult} (FAKEOUT RESULT)");
    onFakeoutResult(false);
    Debug.Log("[FAKEOUT FLOW] >>> DoFakeout END (punished = FALSE) <<<");
    yield break;
}


    private void OnFakeoutQTEComplete(QTEResult result)
    {
        fakeoutResult = result;
        qteCompleted = true;
    }

    // ---------------------------------------------------------
    // QTE RESULT HANDLING
    // ---------------------------------------------------------
    private IEnumerator HandleEnemyAttackResult(EnemyMove move, QTEResult result, BattleManager battleManager)
    {
        PlayerSkillTracker.RegisterResult(result);

        if (battleManager.enemyTurnInterrupted)
            yield break;

        if (IsInvalid())
            yield break;

        // --- DYNAMIC TAG LOGIC (Execute/AntiTank re-check) ---
        /* var tags = move.GetTagModifiers();
        float dynamicMult = 1f;

        // Execute Check (Dynamic re-calc for QTE damage)
        if (tags.isExecute)
        {
            float hpPct = (float)PlayerStats.Instance.currentHealth / PlayerStats.Instance.MaxHealth;
            if (hpPct <= 0.3f) dynamicMult *= 1.5f;
        }
        // AntiTank Check (Dynamic re-calc for QTE damage)
        if (tags.isAntiTank)
        {
            if (PlayerStats.Instance.defense >= 10) dynamicMult *= 1.3f;
        } */
        // -----------------------------------------------------

        switch (result)
        {
            case QTEResult.Perfect:
                /* float counteratkScale = 1f + (PlayerStats.Instance.counteratk * 0.04f);
                int counterDamage = Mathf.RoundToInt(move.counterAttackDamage * counteratkScale); */
                int counterDamage = personalityBrain.GetCounterDamage(move);
                yield return battleManager.ShowMessage($"Perfect dodge! You counter for {counterDamage} damage!");
                //Debug.Log($"[COUNTER DEBUG] base={move.counterAttackDamage}, stat={PlayerStats.Instance.counteratk}, scale={counteratkScale}, final={counterDamage}");
                TakeDamage(counterDamage);
                if (healthBar != null)
                    healthBar.value = runtime.currentHP;
                battleManager.UpdateHealthBars();
                battleManager.AdjustTurnMeterFromEnemyQTE(result, runtime);
                if (battleManager.enemyTurnInterrupted)
                    yield break;
                yield break;

            case QTEResult.Good:
                yield return battleManager.ShowMessage($"You dodged {move.moveName}!");
                battleManager.AdjustTurnMeterFromEnemyQTE(result, runtime);
                if (battleManager.enemyTurnInterrupted)
                    yield break;
                yield break;

            case QTEResult.Ok:
                /* float enemyATKScale = 1f + (runtime.info.attack * 0.04f);
                // Apply dynamic mult to OK damage
                int baseDamage = Mathf.RoundToInt(move.damageOnMiss * enemyATKScale * dynamicMult); 
                float reduction = 0.25f;
                int reducedDamage = Mathf.RoundToInt(baseDamage * reduction);
                PlayerStats.Instance.TakeDamage(reducedDamage); */
                int dmg = personalityBrain.ApplyQTEResult(move, result);
                PlayerStats.Instance.TakeDamage(dmg);
                yield return battleManager.ShowMessage($"{move.moveName} hits you for {dmg} damage!");
                //yield return battleManager.ShowMessage($"You barely dodged {move.moveName}! You take {reducedDamage} damage.");
                battleManager.AdjustTurnMeterFromEnemyQTE(result, runtime);
                if (battleManager.enemyTurnInterrupted)
                    yield break;
                yield break;

            case QTEResult.Miss:
            default:
                /* float enemyAtkScale = 1f + (runtime.info.attack * 0.04f);
                // Apply dynamic mult to Miss damage
                int damageTaken = Mathf.RoundToInt(move.damageOnMiss * enemyAtkScale * dynamicMult);
                PlayerStats.Instance.TakeDamage(damageTaken); */
                dmg = personalityBrain.ApplyQTEResult(move, result);
                PlayerStats.Instance.TakeDamage(dmg);
                yield return battleManager.ShowMessage($"{move.moveName} hits you for {dmg} damage!");
                //yield return battleManager.ShowMessage($"{move.moveName} connects! You take {damageTaken} damage!");
                battleManager.AdjustTurnMeterFromEnemyQTE(result, runtime);
                if (battleManager.enemyTurnInterrupted)
                    yield break;
                break;
        }

        if (PlayerStats.Instance.currentHealth <= 0)
        {
            yield return battleManager.ShowMessage("You were defeated...");
            battleManager.EndBattle(false);
        }
    }

    // ---------------------------------------------------------
    // SYNERGY / CHAIN
    // ---------------------------------------------------------
    /* private void ApplySynergyForThisTurn(BattleManager battleManager)
    {
        runtime.synergyScore = 0f;
        runtime.forceSabotageThisTurn = false;

        if (runtime.personality == null)
            return;

        var myType = runtime.personality.personalityType;
        var allEnemies = battleManager.GetActiveEnemies();
        if (allEnemies == null || allEnemies.Count == 0)
            return;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            var ally = allEnemies[i];
            if (ally == null || ally == this) continue;
            if (ally.runtime == null || ally.runtime.personality == null) continue;

            var allyType = ally.runtime.personality.personalityType;
            float aff = EnemySynergyMatrix.GetAffinity(myType, allyType);

            runtime.synergyScore += aff;

            if (aff <= -1.5f)
                runtime.forceSabotageThisTurn = true;
        }

        Debug.Log($"[ENEMY AI Synergy] {GetEnemyName()} synergyScore = {runtime.synergyScore:F2}, sabotage={runtime.forceSabotageThisTurn}");

        if (runtime.synergyScore > 0.25f)
        {
            float factor = Mathf.Clamp(runtime.synergyScore, 0.25f, 2f);

            runtime.aggressiveBias     += 0.1f * factor;
            runtime.executeBias        += 0.1f * factor;
            runtime.armorPiercingBias  += 0.05f * factor;
            runtime.info.fakeoutBias   += 0.05f * factor;

            Debug.Log($"[ENEMY AI Synergy] {GetEnemyName()} got POSITIVE synergy boost (factor={factor:F2}).");
        }

        if (runtime.synergyScore < -0.25f)
        {
            float factor = Mathf.Clamp(-runtime.synergyScore, 0.25f, 2f);

            runtime.aggressiveBias     *= 1f - 0.2f * factor;
            runtime.executeBias        *= 1f - 0.2f * factor;
            runtime.armorPiercingBias  *= 1f - 0.1f * factor;
            runtime.info.fakeoutBias   *= 1f - 0.1f * factor;

            Debug.Log($"[ENEMY AI  Synergy] {GetEnemyName()} got NEGATIVE synergy penalty (factor={factor:F2}).");
        }
    } */

    /* private void MarkSynergyChainForMove(EnemyMove move, BattleManager battleManager)
    {
        if (battleManager == null || move == null) return;

        bool defTarget =
            move.ignoresDefense ||
            move.HasTag(MoveTag.ArmorPiercing) ||
            move.HasTag(MoveTag.AntiTank);

        bool hpOrAtkTarget =
            move.HasTag(MoveTag.Execute) ||
            move.HasTag(MoveTag.MultiHit) ||
            move.baseDamage >= 18 ||  
            move.damageOnMiss >= 18;

        if (defTarget)
        {
            battleManager.synergyChain.defTargetedLastTurn = true;
            Debug.Log($"[SynergyChain] {GetEnemyName()} marked DEF-target chain for move {move.moveName}");
        }

        if (hpOrAtkTarget)
        {
            battleManager.synergyChain.hpOrAtkTargetedLastTurn = true;
            Debug.Log($"[SynergyChain] {GetEnemyName()} marked HP/ATK-target chain for move {move.moveName}");
        }
    } */

    /* private void ApplySynergyChainBiases(BattleManager battleManager)
    {
        if (battleManager == null || runtime.personality == null) return;

        var chain = battleManager.synergyChain;

        if (!chain.defTargetedLastTurn && !chain.hpOrAtkTargetedLastTurn)
            return;

        float synergyFactor = Mathf.Clamp(1f + runtime.synergyScore * 0.5f, 0.5f, 2f);

        if (chain.defTargetedLastTurn)
        {
            runtime.executeBias += 0.25f * synergyFactor;
            runtime.aggressiveBias += 0.1f * synergyFactor;

            Debug.Log(
                $"[SynergyChain] DEF was targeted last enemy turn → " +
                $"{GetEnemyName()} gets +ExecuteBias, +AggressiveBias (factor={synergyFactor:F2})"
            );
        }

        if (chain.hpOrAtkTargetedLastTurn)
        {
            runtime.armorPiercingBias += 0.2f * synergyFactor;

            Debug.Log(
                $"[SynergyChain] HP/ATK was targeted last enemy turn → " +
                $"{GetEnemyName()} gets +ArmorPiercingBias (factor={synergyFactor:F2})"
            );
        }
    } */

    private IEnumerator SmoothEnemyHP(float start, float end, float duration = 0.25f)
    {
        if (Mathf.Approximately(start, end))
        {
            healthBar.value = end;
            yield break;
        }

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = t / duration;
            healthBar.value = Mathf.Lerp(start, end, lerp);
            yield return null;
        }

        healthBar.value = end;
    }

    public void ForceStopAllEnemyActions()
    {
        // Stop only this enemy’s coroutines
        StopAllCoroutines();

        // Stop QTE safely
        if (qteManager != null)
            qteManager.ForceStopQTE();

        // Ensure loops exit
        qteCompleted = true;
        fakeoutResult = QTEResult.None;
    }

    public void ReceiveQTEResultFromBattleManager(QTEResult result)
    {
        //Debug.Log($"[Counter] QTE result received: {result}");

        if (result == QTEResult.Miss)
        {
            //Debug.Log("[Counter] Attempting counter trigger...");
            bool didCounter = CounterSystem.TryTriggerCounter(this, result);
            if (didCounter)
            {
                //Debug.Log("[Counter] Enemy triggered a counter-attack.");
                return;
            }
            else
            {
                //Debug.Log("[Counter] CounterSystem returned FALSE — no counter this time.");
            }
        }

        // Future: pattern awareness reactions can go here
    }

    public string GetEnemyName()
    {
        return runtime != null && runtime.info != null
            ? runtime.info.enemyName
            : gameObject.name;
    }

    private bool IsInvalid()
    {
        return !gameObject.activeInHierarchy || IsDead();
    }
}