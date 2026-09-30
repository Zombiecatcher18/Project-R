using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton player statistics manager. Maintains player HP, attack, defense, bounty, and level progression.
/// Used by BattleManager.cs for damage calculations and by LevelUpUI.cs for level-up displays.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    /// <summary>Singleton instance for global access to player stats.</summary>
    public static PlayerStats Instance;

    [Header("Health")]
    /// <summary>Maximum health capacity; increases on level up via IncreaseMaxHealth().</summary>
    public int MaxHealth = 100;
    /// <summary>Current remaining health; decremented by TakeDamage() and incremented by Heal().</summary>
    public int currentHealth;

    [Header("STATS")]
    /// <summary>Attack stat; used in damage calculations in BattleManager.FinishAllQTEs() (0.04f multiplier).</summary>
    public int attack = 10;
    /// <summary>Defense stat; used in damage reduction formula: 100 / (100 + defense).</summary>
    public int defense = 5;
    public int counteratk = 1;
    public float criticalchance = 0.05f;
    public int turnControl = 0; // 0 = neutral, higher = better meter control
    public int deckSize = 4;
    public int maxslots = 4;

    [HideInInspector] 
    /// <summary>Previous MaxHealth before last level up; used by LevelUpUI.cs for display.</summary>
    public int oldMaxHealth;
    [HideInInspector] 
    /// <summary>Previous attack stat before last level up; used by LevelUpUI.cs for display.</summary>
    public int oldAttack;
    [HideInInspector] 
    /// <summary>Previous defense stat before last level up; used by LevelUpUI.cs for display.</summary>
    public int oldDefense;
    [HideInInspector]
    public int oldCounteratk;
    [HideInInspector]
    public int oldMaxslots;
    [HideInInspector]
    public int oldTurnControl;
    [HideInInspector]
    public int oldCriticalchance;

    [HideInInspector] 
    /// <summary>Amount to increase stats by on next level up; set by LevelUpUI progression system.</summary>
    public int statBonusAmount;

    [Header("Bounty & Level")]
    /// <summary>Current player level; increments when bountyToNextLevel threshold is reached.</summary>
    public int level = 0;
    /// <summary>Current accumulated bounty points; gained from battle victories via AddBattleBounty().</summary>
    public int currentBounty = 0;
    public int bountyRank = 0;
    public string bountyRankName = "Nobody";
    /// <summary>Bounty threshold for next level up; increases by bountyMultiplier each level.</summary>
    public int bountyToNextLevel = 100;
    /// <summary>Multiplier applied to bountyToNextLevel each level (e.g., 1.2x for 20% increase).</summary>
    public float bountyMultiplier = 1.2f;

    private readonly int[] rankThresholds = { 1, 10, 20, 30, 40, 50, 60, 70, 80, 90 };
    private readonly string[] rankNames = {
        "Nobody",
        "Rookie",
        "Street Punk",
        "Thug",
        "Outlaw",
        "Criminal",
        "Most Wanted",
        "Menace",
        "Threat to Society",
        "National Threat",
        "Global Threat"
    };
    /// <summary>Flag set to true when bounty gain reaches level threshold; triggers LevelUpUI display.</summary>
    public bool leveledUp = false;
    public bool pendingRankUp = false;

    [Header("Rank Bonuses")]
    public int rankBonusMin = 1;   // minimum bonus per rank
    public int rankBonusMax = 2;   // maximum bonus per rank
    [HideInInspector] public bool justRankedUp = false;
    [HideInInspector] public int lastRankSlotBonus = 0;
    [HideInInspector] public int lastRankDeckBonus = 0;

    [Header("UpgradeCheck")]
    /// <summary>Count of HP upgrades applied to player; used for achievement/stat tracking.</summary>
    public int hpUpgrades = 0;
    /// <summary>Count of attack upgrades applied to player; used for achievement/stat tracking.</summary>
    public int atkUpgrades = 0;
    /// <summary>Count of defense upgrades applied to player; used for achievement/stat tracking.</summary>
    public int defUpgrades = 0;
    public int counteratkUpgrades = 0;
    public int criticalchanceUpgrades = 0;
    public int turnControlUpgrades = 0;
    public int deckSizeUpgrades = 0;
    public int maxslotsUpgrades = 0;

    [HideInInspector]
    public AttackMove lastUnlockedMove;
    /// <summary>Cached player GameObject to avoid repeated FindWithTag calls.</summary>
    private GameObject cachedPlayer;
    /// <summary>Cached CharacterController component reference; used for respawn logic.</summary>
    private CharacterController cachedCC;
    public float tempDamageTakenMultiplier = 1f;

    /// <summary>
    /// Initializes singleton instance, sets current health to max if uninitialized.
    /// Marks instance as persistent across scene loads.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (currentHealth <= 0)
            currentHealth = MaxHealth;
    }

    // ===================================================
    // Health Management
    // ===================================================

    /// <summary>
    /// Applies damage to player with defense-based reduction.
    /// Defense formula: effective damage = raw damage * (100 / (100 + defense)).
    /// Updates battle UI via BattleManager.UpdateHealthBars().
    /// Triggers death if health drops to 0.
    /// Called by BattleManager.ApplyComboDamage() and EnemyAttackController.
    /// </summary>
    public void TakeDamage(int amount)
    {
        // Base defense reduction
        float reduction = 100f / (100f + defense);
        int reducedByDefense = Mathf.RoundToInt(amount * reduction);

        // Apply temporary defense buff (Option B)
        int finalDamage = Mathf.RoundToInt(reducedByDefense * tempDamageTakenMultiplier);

        currentHealth -= finalDamage;
        if (currentHealth < 0)
            currentHealth = 0;

        Debug.Log($"Player took {finalDamage} damage (raw={amount}). Current Health: {currentHealth}");

        BattleManager.Instances.UpdateHealthBars();

        if (currentHealth == 0)
            Die();
    }

    /// <summary>
    /// Restores player health, capped at MaxHealth.
    /// Called by healing items and recovery mechanics.
    /// </summary>
    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > MaxHealth)
            currentHealth = MaxHealth;

        Debug.Log($"Player healed {amount}. Current Health: {currentHealth}");
    }

    /// <summary>
    /// Handles player death: triggers respawn at checkpoint or calls BattleManager victory.
    /// References Checkpoint system via lastCheckpointPosition from GameManager.
    /// </summary>
    private void Die()
    {
        Debug.Log("Player Died!");

        // Use scene name comparison instead of buildIndex for clarity
        if (SceneManager.GetActiveScene().name == "BattleScene")
        {
            if (BattleManager.Instances != null)
            {
                // Trigger battle loss condition; handled by BattleManager.EndBattle()
                BattleManager.Instances.EndBattle(false);
                return;
            }
        }

        // Respawn at last checkpoint in overworld
        StartCoroutine(RespawnAtCheckpoint());
    }

    // ===================================================
    // BOUNTY / LEVELING
    // ---------------------------------------------------------
    public void GainBounty(int amount)
    {
        leveledUp = false;

        currentBounty += amount;
        Debug.Log($"Gained {amount} bounty! Current: {currentBounty}/{bountyToNextLevel}");

        while (currentBounty >= bountyToNextLevel)
        {
            oldMaxHealth = MaxHealth;
            oldAttack = attack;
            oldDefense = defense;
            oldCounteratk = counteratk;
            oldTurnControl = turnControl;
            oldMaxslots = maxslots;
            oldCriticalchance = Mathf.RoundToInt(criticalchance * 100);

            currentBounty -= bountyToNextLevel;
            LevelUp();
            leveledUp = true;

            CheckBountyRank();
        }
    }

    private void CheckBountyRank()
    {
        if (bountyRank < rankThresholds.Length &&
            level >= rankThresholds[bountyRank])
        {   
            pendingRankUp = true;
            justRankedUp = true;
            
            Debug.Log($"[RANK] Rank up triggered! level={level}, oldRank={bountyRank}, newRank={bountyRank + 1}");

            bountyRank++;
            bountyRankName = rankNames[bountyRank];

            // ===============================
            // RANK REWARD MOVE UNLOCK
            // ===============================
            var deck = PlayerMoveDeck.Instance;

            if (deck != null)
            {
                // Make sure we have a move for this rank
                if (bountyRank < deck.rankRewardMoves.Count)
                {
                    AttackMove reward = deck.rankRewardMoves[bountyRank];

                    if (reward != null)
                    {
                        // Add to unlocked moves if not already there
                        if (!deck.unlockedMoves.Contains(reward) &&
                            !deck.masterPool.Contains(reward))
                        {
                            deck.unlockedMoves.Add(reward);
                            lastUnlockedMove = reward;
                            Debug.Log($"[RANK REWARD] Unlocked new move: {reward.moveName}");
                        }
                    }
                }
            }

            // Random bonus between rankBonusMin and rankBonusMax
            int slotBonus = Random.Range(rankBonusMin, rankBonusMax + 1);
            int deckBonus = Random.Range(rankBonusMin, rankBonusMax + 1);

            maxslots += slotBonus;
            deckSize += deckBonus;

            lastRankSlotBonus = slotBonus;
            lastRankDeckBonus = deckBonus;

            maxslotsUpgrades += slotBonus;
            deckSizeUpgrades += deckBonus;

            Debug.Log($"[RANK UP] New Rank: {bountyRankName}! " +
                    $"MaxSlots +{slotBonus} → {maxslots}, " +
                    $"DeckSize +{deckBonus} → {deckSize}");
        }
    }

    private void LevelUp()
    {
        int oldLevel = level;
        int oldReq = bountyToNextLevel;

        level++;
        currentHealth = MaxHealth;
        bountyToNextLevel = Mathf.RoundToInt(bountyToNextLevel * bountyMultiplier);

        Debug.Log(
            $"[LEVEL UP] Level {oldLevel} → {level}\n" +
            $"[Bounty Requirement] {oldReq} → {bountyToNextLevel}\n" +
            $"[Current Health] Restored to {currentHealth}"
        );
    }

    // ---------------------------------------------------------
    // STAT UPGRADES
    // ---------------------------------------------------------
    public void ApplyStatBoost(string stat)
    {
        int bonus = statBonusAmount > 0 ? statBonusAmount : 1;

        switch (stat)
        {
            case "HP":
                MaxHealth += bonus;
                currentHealth = MaxHealth;
                Debug.Log($"Player HP increased by {bonus}.");
                break;

            case "Attack":
                attack += bonus;
                Debug.Log($"Player Attack increased by {bonus}.");
                break;

            case "Defense":
                defense += bonus;
                Debug.Log($"Player Defense increased by {bonus}.");
                break;

            case "Counteratk":
                counteratk += bonus;
                Debug.Log($"Player Counter Attack increased by {bonus}.");
                break;

            case "Maxslots":
                maxslots += bonus;
                Debug.Log($"Player Max Slots increased by {bonus}.");
                break;

            case "Criticalchance":
                criticalchance += bonus * 0.01f; 
                Debug.Log($"Player Critical Chance increased by {bonus}.");
                break;
        }

        Debug.Log($"[LEVEL UP] Boosted {stat}! New stats: HP={MaxHealth}, ATK={attack}, DEF={defense}");
    }

    public void UpgradeHP()
    {
        MaxHealth++;
        currentHealth = MaxHealth;
        hpUpgrades++;
    }

    public void UpgradeATK()
    {
        attack++;
        atkUpgrades++;
    }

    public void UpgradeDEF()
    {
        defense++;
        defUpgrades++;
    }

    public void UpgradeCounteratk()
    {
        counteratk++;
        counteratkUpgrades++;
    }

    public void UpgradeDecksize()
    {
        deckSize++;
        deckSizeUpgrades++;
        Debug.Log($"Player Deck Size increased to {deckSize}");
    }

    public void UpgradeMaxslots()
    {
        maxslots++;
        maxslotsUpgrades++;
        Debug.Log($"Player Max Slots increased to {maxslots}");
    }

    public void UpgradeCriticalchance()
    {
        criticalchance += 0.01f;
        criticalchanceUpgrades++;
    }

    public void UpgradeTurnControl()
    {
        turnControl++;
        turnControlUpgrades++;
        Debug.Log($"Turn Control increased to {turnControl}");
    }

    public float GetTurnControlMultiplier()
    {
        // Each point gives +2% meter gain and -2% meter loss 
        // // You can tune this later 
        return 1f + (turnControl * 0.02f);
    }

    // ---------------------------------------------------------
    // RESET
    // ---------------------------------------------------------
    private void Reset()
    {
        ResetStats();
    }

    public void ResetStats()
    {
        level = 0;
        currentBounty = 0;
        bountyToNextLevel = 100;

        MaxHealth = 30;
        currentHealth = MaxHealth;

        attack = 5;
        defense = 3;
        counteratk = 1;
        maxslots = 3;
        deckSize = 3;
        turnControl = 0;
        criticalchance = 0.05f;

        leveledUp = false;

        hpUpgrades = 0;
        atkUpgrades = 0;
        defUpgrades = 0;
    }

    // ---------------------------------------------------------
    // RESPAWN
    // ---------------------------------------------------------
    private IEnumerator RespawnAtCheckpoint()
    {
        yield return new WaitForSeconds(1f);

        currentHealth = Mathf.CeilToInt(MaxHealth * 0.5f);

        if (cachedPlayer == null)
        {
            cachedPlayer = GameObject.FindWithTag("Player");
            cachedCC = cachedPlayer.GetComponent<CharacterController>();
        }

        if (cachedCC != null)
            cachedCC.enabled = false;

        cachedPlayer.transform.position = GameManager.Instance.lastCheckpointPosition;

        if (cachedCC != null)
            cachedCC.enabled = true;

        Debug.Log("[Respawn] Player returned to checkpoint.");
    }
}

// ============================================================================
// PLAYER SKILL TRACKER — FULLY OPTIMIZED (NO LINQ, NO QUEUE ALLOCATIONS)
// ============================================================================
public static class PlayerSkillTracker
{
    private const int maxHistory = 20;
    private static readonly int[] history = new int[maxHistory];
    private static int index = 0;
    private static int count = 0;

    public static bool justDidPerfectCombo = false;

    public static float FakeoutSkillFactor
    {
        get
        {
            if (count == 0)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += history[i];

            float avg = sum / count;

            float normalized = Mathf.InverseLerp(-1f, 2f, avg);
            return Mathf.Clamp01(normalized);
        }
    }

    public static void RegisterResult(QTEResult result)
    {
        int score;

        switch (result)
        {
            case QTEResult.Perfect: score = 2; break;
            case QTEResult.Good: score = 1; break;
            case QTEResult.Miss: score = -1; break;
            default: return; // Fakeouts do not affect skill
        }

        history[index] = score;
        index = (index + 1) % maxHistory;

        if (count < maxHistory)
            count++;
    }

    public static void DecaySkill()
    {
        if (count > 0)
            count--;
    }
}
