using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
/// <summary>
/// Represents an enemy encounter in the overworld.
/// Manages enemy visibility based on quest state and defeat status.
/// Initiates battle transitions and applies cooldowns after fleeing.
/// Works with EnemyMovement.cs for patrol behavior and GameManager for persistent state.
/// </summary>
public class EnemyScript : MonoBehaviour
{
    [Header("Identity")]
    /// <summary>Unique identifier for this enemy instance; used for defeat tracking and cooldown management.</summary>
    public string enemyID;
    /// <summary>Display name of the enemy shown in UI; matched with EnemyInfo for party composition.</summary>
    public string enemyName = "Enemy";

    [Header("Party Composition")]
    /// <summary>List of EnemyInfo scriptable objects for this encounter; each represents a party member.</summary>
    public List<EnemyInfo> partyMembers = new List<EnemyInfo>();

    [Header("Detection")]
    /// <summary>Collider for physical enemy body; used to detect player collision in OnTriggerEnter().</summary>
    public Collider bodyTrigger;
    /// <summary>Capsule detection trigger around enemy; defines player approach distance for battle initiation.</summary>
    public Collider detectionTrigger;

    [Header("Battle Map")]
    /// <summary>Battle arena environment prefab; instantiated in battle scene via BattleManager.</summary>
    public GameObject battleMapPrefab;

    [Header("Battle / Cooldown")]
    /// <summary>Duration in seconds before enemy can be engaged after fleeing; prevents immediate re-encounter.</summary>
    public float battleCooldownDuration = 20f;
    [HideInInspector] 
    /// <summary>Timestamp when battle cooldown expires; set by StartBattleCooldown().</summary>
    public float battleCooldownEndTime = 0f;
    [HideInInspector] 
    /// <summary>Flag preventing battle for duration of run-away lock; set by player flee mechanics.</summary>
    public bool runAwayLockActive = false;
    /// <summary>Flag to disable run option for boss/scripted battles; passed to BattleData.</summary>
    public bool disableRunOption = false;

    public List<BattleManager.Turn> encounterTurnPattern;

    /// <summary>Movement component for patrol/wandering behavior; references EnemyMovement.cs.</summary>
    private EnemyMovement enemyMovement;

    /// <summary>
    /// Initializes enemy with unique ID if not set, caches movement component, and sets up detection collider.
    /// </summary>
    private void Awake()
    {
        if (string.IsNullOrEmpty(enemyID))
            enemyID = Guid.NewGuid().ToString();

        enemyMovement = GetComponent<EnemyMovement>();

        if (detectionTrigger == null)
            detectionTrigger = GetComponent<Collider>();

        if (bodyTrigger != null)
        {
            bodyTrigger.isTrigger = true;
        }
    }

    /// <summary>
    /// Checks visibility based on quest state and defeat status.
    /// Enemies are hidden if: already defeated, or quest-locked but quest not started.
    /// Visibility logic queries GameManager for persistent state.
    /// </summary>
    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "BattleScene") return;

        // Delay visibility logic until Start — Unity allows SetActive here
        if (GameManager.Instance.defeatedEnemyIDs.Contains(enemyID))
        {
            gameObject.SetActive(false);
            return;
        }

        if (GameManager.Instance.questActiveEnemyIDs.Contains(enemyID) &&
            !QuestManager.Instance.questAccepted)
        {
            gameObject.SetActive(false);
            return;
        }

        if (GameManager.Instance.questActiveEnemyIDs.Contains(enemyID) &&
            QuestManager.Instance.questAccepted)
        {
            gameObject.SetActive(true);
        }

        Debug.Log($"[EnemyScript] {enemyID} Start. questAccepted={QuestManager.Instance.questAccepted}, activeList={string.Join(",", GameManager.Instance.questActiveEnemyIDs)} defeated={string.Join(",", GameManager.Instance.defeatedEnemyIDs)}");
    }

    /// <summary>
    /// Triggered when player enters detection collider.
    /// Only initiates battle if collision is with bodyTrigger (not generic detection).
    /// Calls EngageBattle() with player position for battle start.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Only start battle if THIS collider is the body collider
        if (other.CompareTag("Player") && other == bodyTrigger)
        {
            Debug.Log("Body touched player — starting battle");
            EngageBattle(other.transform.position);
        }
    }

    /// <summary>
    /// Validates battle conditions: checks cooldown, defeat status, and run-away lock.
    /// Returns early if conditions prevent battle.
    /// Called by OnTriggerEnter() and player interact mechanics.
    /// </summary>
    public void EngageBattle(Vector3 playerPosition)
    {
        if (runAwayLockActive || IsBattleCooldownActive())
        {
            Debug.Log($"[EnemyScript] {enemyID} cannot battle due to cooldown.");
            return;
        }

        // Bounty restriction
        if (GameManager.Instance != null &&
            GameManager.Instance.defeatedEnemyIDs.Contains(enemyID))
        {
            Debug.Log($"[EnemyScript] {enemyID} already defeated — no battle.");
            return;
        }

        StartBattle(playerPosition);
    }

    private void StartBattle(Vector3 playerPosition)
    {
        Debug.Log($"[EnemyScript] Starting battle with {partyMembers.Count} enemies.");

        BattleData.Clear();

        GameManager.Instance.SaveGame();

        // Build party
        BattleData.enemyParty = new EnemyParty(partyMembers);
        BattleData.enemyPartyRuntime = new List<EnemyRuntimeData>();

        foreach (var info in partyMembers)
            BattleData.enemyPartyRuntime.Add(new EnemyRuntimeData(info));

        BattleData.enemyID = enemyID;
        BattleData.enemyName = enemyName;
        BattleData.disableRunOption = disableRunOption;
        BattleData.battleMapPrefab = battleMapPrefab;
        
        // Always reset and overwrite turn pattern from this enemy
        BattleData.turnPattern = new List<BattleManager.Turn>();

        // If the enemy has a custom pattern, use it
        if (encounterTurnPattern != null && encounterTurnPattern.Count > 0)
        {
            BattleData.turnPattern.AddRange(encounterTurnPattern);
        }
        else
        {
            // Default fallback
            BattleData.turnPattern.Add(BattleManager.Turn.Player);
            BattleData.turnPattern.Add(BattleManager.Turn.Enemy);
        }

        /* // Ensure Player always starts
        if (BattleData.turnPattern[0] != BattleManager.Turn.Player)
        {
            BattleData.turnPattern.Insert(0, BattleManager.Turn.Player);
        } */

        if (GameManager.Instance != null)
        {
            GameManager.Instance.sceneBeforeBattle = SceneManager.GetActiveScene().name;
            GameManager.Instance.SaveBattleStartPosition(playerPosition);
        }

        SceneManager.LoadScene("BattleScene");
    }

    public bool IsBattleCooldownActive()
    {
        return Time.time < battleCooldownEndTime;
    }

    public void StartBattleCooldown(float duration = -1f)
    {
        if (duration <= 0f) duration = battleCooldownDuration;

        battleCooldownEndTime = Time.time + duration;
        runAwayLockActive = true;

        if (GameManager.Instance != null)
            GameManager.Instance.enemyCooldowns[enemyID] = battleCooldownEndTime;

        StartCoroutine(CooldownRoutine(duration));
        Debug.Log($"[EnemyScript] {enemyID} cooldown started for {duration:F1}s");
    }

    private IEnumerator CooldownRoutine(float duration)
    {
        bool wasEnabled = detectionTrigger.enabled;
        detectionTrigger.enabled = false;

        yield return new WaitForSeconds(duration);

        detectionTrigger.enabled = wasEnabled;
        runAwayLockActive = false;

        Debug.Log($"[EnemyScript] {enemyID} cooldown ended.");
    }

    public void HideAfterDefeat()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.defeatedEnemyIDs.Add(enemyID);

        if (QuestManager.Instance != null)
            QuestManager.Instance.MarkEnemyDefeated(enemyID);

        gameObject.SetActive(false);
    }

    public void ActiveRunAwayLock()
    {
        runAwayLockActive = true;
    }

    private void OnEnable()
    {
        if (SceneManager.GetActiveScene().name == "BattleScene") return;
        
        if (GameManager.Instance == null || QuestManager.Instance == null)
            return;

        if (GameManager.Instance.defeatedEnemyIDs.Contains(enemyID))
        {
            gameObject.SetActive(false);
            return;
        }

        if (GameManager.Instance.questActiveEnemyIDs.Contains(enemyID) &&
            !QuestManager.Instance.questAccepted)
        {
            gameObject.SetActive(false);
        }
    }
}

public class EnemyParty
{
    public List<EnemyInfo> enemies = new List<EnemyInfo>();

    public EnemyParty() { }
    public EnemyParty(List<EnemyInfo> infos)
    {
        enemies = infos;
    }
}
