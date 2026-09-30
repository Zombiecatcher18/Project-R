using UnityEngine;

/// <summary>
/// Container for dialogue sets associated with quest stages.
/// Includes speaker names, dialogue lines, and portrait sprites for visual presentation.
/// </summary>
[System.Serializable]
public class DialogueSet
{
    /// <summary>Array of speaker names for dialogue lines.</summary>
    public string[] speaker;
    /// <summary>Array of dialogue text strings displayed to player.</summary>
    [TextArea]
    public string[] dialogue;
    /// <summary>Array of character portraits displayed during dialogue (order matches speaker/dialogue arrays).</summary>
    public Sprite[] portraits;
}

/// <summary>
/// Quest NPC orchestrator handling quest acceptance, progression, and completion.
/// Manages dialogue display at different quest stages and enemy activation for quest battles.
/// Coordinates with DialogueManager, QuestManager, and GameManager for state management.
/// Gives quest rewards when enemies are defeated.
/// </summary>
public class QuestNPC : MonoBehaviour
{
    /// <summary>Dialogue for initial NPC interaction before quest acceptance.</summary>
    [Header("Dialogue Sets")]
    public DialogueSet beforeQuestDialogue;
    /// <summary>Dialogue shown while quest is in progress (enemies not defeated).</summary>
    public DialogueSet questInProgressDialogue;
    /// <summary>Dialogue shown when all quest enemies are defeated but reward not given yet.</summary>
    public DialogueSet questCompleteDialogue;
    /// <summary>Dialogue shown after quest is fully completed and reward collected.</summary>
    public DialogueSet postQuestDialogue;

    /// <summary>Reference to DialogueManager for displaying quest dialogue.</summary>
    [Header("References")]
    public DialogueManager dialogueManager;

    /// <summary>Array of enemy IDs that must be defeated to complete quest; matched against GameManager.defeatedEnemyIDs.</summary>
    [Header("Quest Settings")]
    public string[] questEnemyIDs;
    /// <summary>Item reward given when all quest enemies defeated; added to InventorySystem.</summary>
    public InventoryItemData rewardItem;

    /// <summary>
    /// Initializes quest NPC on scene load.
    /// Registers this NPC with DialogueManager for dialogue display.
    /// Called by Unity on scene load before first frame.
    /// </summary>
    private void Start()
    {
        if (dialogueManager != null)
            dialogueManager.npc = GetComponent<NPCController>();
    }

    /// <summary>
    /// Handles quest state progression when dialogue closes.
    /// Routes to appropriate action based on quest status: accept, check completion, or post-quest.
    /// Called by DialogueManager.StopDialogue() after dialogue finishes.
    /// References QuestManager, GameManager for state management and enemy activation.
    /// </summary>
    public void OnDialogueClosed()
    {
        var qm = QuestManager.Instance;
        var gm = GameManager.Instance;

        if (qm == null || gm == null)
        {
            Debug.LogError("[QuestNPC] Missing QuestManager or GameManager!");
            return;
        }

        // ===== STAGE 1: QUEST NOT ACCEPTED YET =====
        if (!qm.questAccepted)
        {
            qm.questAccepted = true;

            // Store required enemy IDs in QuestManager for completion checking
            qm.requiredEnemyIDs = questEnemyIDs;

            // Activate these enemies globally via GameManager for scene-aware spawning
            Debug.Log("[QuestNPC] Quest accepted. Adding enemies to GameManager.questActiveEnemyIDs:");
            foreach (string id in questEnemyIDs)
            {
                gm.questActiveEnemyIDs.Add(id);
                Debug.Log($" -> Activated enemy: {id}");
            }
                

            Debug.Log("[QuestNPC] Quest enemies will spawn when player encounters them in scenes.");
            return;
        }

        // ===== STAGE 2: QUEST ACCEPTED → CHECK COMPLETION =====
        if (!qm.rewardGiven)
        {
            // Check if all required enemies have been defeated (called by QuestManager.AreAllEnemiesDefeated() with GameManager.defeatedEnemyIDs check)
            bool allDead = qm.AreAllEnemiesDefeated();

            if (allDead)
            {
                GiveReward();

                // Remove enemies from active quest list now that quest is complete
                foreach (string id in questEnemyIDs)
                    gm.questActiveEnemyIDs.Remove(id);

                Debug.Log("[QuestNPC] Quest complete. Enemies will now stay disabled.");
            }
            else
            {
                Debug.Log("[QuestNPC] Quest not complete. Enemies still alive.");
            }
        }
    }

    /// <summary>
    /// Gives reward item to player when quest completes.
    /// Adds rewardItem to InventorySystem and marks reward as given in QuestManager.
    /// Called by OnDialogueClosed() when all quest enemies defeated.
    /// References InventorySystem, QuestManager for item and state management.
    /// </summary>
    private void GiveReward()
    {
        var qm = QuestManager.Instance;

        if (qm.rewardGiven)
            return;

        if (InventorySystem.current == null)
        {
            Debug.LogError("[QuestNPC] InventorySystem is null!");
            return;
        }

        if (rewardItem == null)
        {
            Debug.LogError("[QuestNPC] Reward item not assigned!");
            return;
        }

        // Add item to inventory (called by InventorySystem.AddItem() with InventoryUI refresh)
        bool success = InventorySystem.current.AddItem(rewardItem, 1);

        if (success)
        {
            qm.rewardGiven = true;
            Debug.Log($"[QuestNPC] Reward given: {rewardItem.itemName}");
        }
        else
        {
            Debug.LogWarning("[QuestNPC] Inventory full. Could not give reward.");
        }
    }

    public DialogueSet GetDialogueSet()
    {
        var qm = QuestManager.Instance;

        if (!qm.questAccepted)
            return beforeQuestDialogue;

        bool allDead = qm.AreAllEnemiesDefeated();

        if (qm.questAccepted && !allDead)
            return questInProgressDialogue;

        if (qm.questAccepted && allDead && !qm.rewardGiven)
            return questCompleteDialogue;

        return postQuestDialogue;
    }
}
