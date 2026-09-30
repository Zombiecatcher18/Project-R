using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Manages NPC dialogue display and input progression throughout conversation.
/// Orchestrates dialogue UI visibility, text/portrait updates, and quest integration.
/// Works with DialogueTrigger (proximity detection) and NPCController (NPC state).
/// 
/// Dialogue Flow:
/// 1. Player presses E/Interact near NPC → DialogueTrigger fires
/// 2. DialogueTrigger calls OpenDialogue()
/// 3. DialogueManager activates UI, loads DialogueSet from QuestNPC
/// 4. Player presses Space/Enter to cycle through dialogue lines
/// 5. At end, triggers QuestNPC.OnDialogueClosed() for quest progression
/// 6. DialogueManager closes UI, resumes NPC/player movement
/// 
/// Input System:
/// - Uses InputSystem (new input system) with Interact and Submit actions
/// - Keyboard fallback: E key for interact, Space/Enter for advance
/// - Respects game input locks (movementLocked during dialogue)
/// 
/// Integration:
/// - DialogueTrigger: Detects player proximity, calls TryInteract()
/// - NPCController: Referenced for state locks (hardLocked, StopMovement)
/// - QuestNPC: Provides DialogueSet, receives OnDialogueClosed() callback
/// - ExamplePlayerController: Locks/unlocks movement during dialogue
/// </summary>
public class DialogueManager : MonoBehaviour
{
    // ===== UI REFERENCES =====
    /// <summary>
    /// Root canvas GameObject for dialogue UI (activated/deactivated for visibility).
    /// Parent container for all dialogue UI elements (text, buttons, portraits).
    /// </summary>
    [Header("UI References")]
    [SerializeField] private GameObject DialogueCanvas;

    /// <summary>
    /// TextMeshPro text for displaying NPC/speaker name.
    /// Updates each dialogue line via DisplayCurrentDialogue().
    /// </summary>
    [SerializeField] private TMP_Text speakerText;

    /// <summary>
    /// TextMeshPro text for displaying dialogue content.
    /// Main dialogue text shown to player, updated per line.
    /// </summary>
    [SerializeField] private TMP_Text dialogueText;

    /// <summary>
    /// Image component for NPC portrait sprite display.
    /// Updates per dialogue line to show expression changes or character variations.
    /// </summary>
    [SerializeField] private Image portraitImage;

    /// <summary>
    /// Button for ending dialogue (usually "Continue" or "Close").
    /// Set as EventSystem selected object when dialogue opens (for UI navigation).
    /// </summary>
    [SerializeField] private Button endDialogueButton;

    // ===== DIALOGUE CONTENT =====
    /// <summary>
    /// Array of speaker names for current dialogue sequence.
    /// Indexed by currentIndex to display current speaker name.
    /// Populated by LoadDialogueSet() from QuestNPC.DialogueSet.
    /// </summary>
    [Header("Dialogue Content")]
    private string[] speaker;

    /// <summary>
    /// Array of dialogue text lines for current sequence.
    /// Indexed by currentIndex to display current dialogue.
    /// Populated by LoadDialogueSet() from QuestNPC.DialogueSet.
    /// </summary>
    private string[] dialogue;

    /// <summary>
    /// Array of portrait sprites for current sequence.
    /// Indexed by currentIndex to display NPC expression/appearance.
    /// Populated by LoadDialogueSet() from QuestNPC.DialogueSet.
    /// Can contain nulls (skips portrait display if null).
    /// </summary>
    private Sprite[] portrait;

    /// <summary>
    /// Current position in dialogue sequence (0 to dialogue.Length-1).
    /// Incremented by EndDialogue() until reaching end, then triggers close.
    /// Initialized randomly in LoadDialogueSet() if multiple lines exist.
    /// </summary>
    private int currentIndex = 0;

    // ===== SCENE REFERENCES =====
    /// <summary>
    /// NPC controller for this dialogue (assigned in inspector).
    /// References NPCController for movement state during dialogue.
    /// Also used to get QuestNPC component for quest progression.
    /// </summary>
    [Header("References")]
    [SerializeField] public NPCController npc;

    /// <summary>
    /// Collider trigger for proximity detection (child of NPC).
    /// Validated in Awake(); logs warning if not assigned.
    /// Used by DialogueTrigger for player range checking.
    /// </summary>
    [SerializeField] private Collider dialogueTrigger;

    /// <summary>
    /// UI GameObject shown when player is in range to interact.
    /// Typically "Press E to Interact" prompt.
    /// Shown by DialogueTrigger, hidden by OpenDialogue().
    /// </summary>
    [SerializeField] public GameObject interactPrompt;

    // ===== RUNTIME STATE =====
    /// <summary>
    /// Cached PlayerInput for subscribing to InputSystem actions.
    /// Retrieved in Awake() via FindObjectOfType() (ONCE, not per-frame).
    /// OPTIMIZATION: FindObjectOfType is O(n), cached to avoid repeated lookups.
    /// Used to listen for Interact and Submit input actions.
    /// </summary>
    private PlayerInput inputSystem;

    /// <summary>
    /// Cached ExamplePlayerController for movement locking during dialogue.
    /// Retrieved in Awake() via FindObjectOfType() (ONCE, not per-frame).
    /// OPTIMIZATION: Avoids per-frame FindObjectOfType call in OpenDialogue/CloseDialogue.
    /// Used to lock/unlock player movement during dialogue sequences.
    /// </summary>
    private ExamplePlayerController cachedPlayerController;

    /// <summary>
    /// Quest NPC component if this dialogue is quest-related.
    /// Retrieved in OpenDialogue() via npc.GetComponent().
    /// Used to call OnDialogueClosed() for quest progression.
    /// Can be null if NPC has no quest component.
    /// </summary>
    public QuestNPC activeQuestNPC;

    /// <summary>
    /// Flag: Player is currently in dialogue trigger collider.
    /// Set by DialogueTrigger.OnTriggerEnter/Exit.
    /// Controls whether "Press E" prompt is visible.
    /// </summary>
    public bool playerInRange = false;

    /// <summary>
    /// Flag: Dialogue UI currently active and progressing.
    /// Set by OpenDialogue(), cleared by CloseDialogue().
    /// Prevents duplicate dialogue opens, controls input handling.
    /// </summary>
    public bool dialogueActivated = false;

    // ===== QUEST STATE ENUM =====
    /// <summary>
    /// Quest dialogue state tracking for quest NPCs.
    /// Allows different dialogue based on quest progression stage.
    /// Used by QuestNPC to determine which DialogueSet to display.
    /// </summary>
    public enum QuestDialogueState
    {
        BeforeQuest,    // Initial quest dialogue, hasn't been accepted
        QuestAccepted,  // Player accepted quest, dialogue updates
        QuestComplete,  // Quest objectives complete, dialogue reflects completion
        QuestRewarded   // Quest finished, reward given, closure dialogue
    }

    // ===== INITIALIZATION =====
    /// <summary>
    /// Unity lifecycle: Cache components, hide UI, validate trigger setup.
    /// Caches PlayerInput and ExamplePlayerController to avoid per-frame FindObjectOfType calls.
    /// This is a critical optimization: FindObjectOfType is O(n) and called multiple times per frame.
    /// 
    /// Actions:
    /// 1. Find PlayerInput component via FindObjectOfType (ONCE, not per frame)
    /// 2. Cache ExamplePlayerController for movement locking (found once)
    /// 3. Deactivate DialogueCanvas (hidden until dialogue opens)
    /// 4. Deactivate interactPrompt (hidden until player in range)
    /// 5. Log warning if dialogueTrigger not assigned (manual setup needed)
    /// </summary>
    private void Awake()
    {
        // OPTIMIZATION: Cache these components ONCE instead of finding them per frame
        inputSystem = FindFirstObjectByType<PlayerInput>();
        cachedPlayerController = FindFirstObjectByType<ExamplePlayerController>();

        if (DialogueCanvas != null)
            DialogueCanvas.SetActive(false);

        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        if (dialogueTrigger == null)
            Debug.LogWarning("Dialogue Trigger not assigned! Assign the child collider manually.");
    }

    // ===== INPUT SUBSCRIPTION =====
    /// <summary>
    /// Unity lifecycle: Subscribe to InputSystem actions for dialogue input.
    /// 
    /// Actions:
    /// 1. Find Interact action from InputSystem (E key or gamepad button)
    /// 2. Register OnInteractPerformed callback
    /// 3. Find Submit action (Space/Enter or gamepad confirm)
    /// 4. Register OnSubmitPerformed callback
    /// 
    /// Called by: Physics system when component enabled
    /// </summary>
    private void OnEnable()
    {
        if (inputSystem != null)
        {
            var act = inputSystem.actions.FindAction("Interact");
            if (act != null)
                act.performed += OnInteractPerformed;

            var submit = inputSystem.actions.FindAction("Submit");
            if (submit != null)
                submit.performed += OnSubmitPerformed;
        }
    }

    /// <summary>
    /// Unity lifecycle: Unsubscribe from InputSystem actions.
    /// Called when component disabled (usually at scene unload).
    /// </summary>
    private void OnDisable()
    {
        if (inputSystem != null)
        {
            var act = inputSystem.actions.FindAction("Interact");
            if (act != null)
                act.performed -= OnInteractPerformed;

            var submit = inputSystem.actions.FindAction("Submit");
            if (submit != null)
                submit.performed -= OnSubmitPerformed;
        }
    }

    /// <summary>
    /// Cleanup on destruction (unsubscribe from input).
    /// </summary>
    private void OnDestroy() => OnDisable();

    // ===== INPUT CALLBACKS =====
    /// <summary>
    /// External interface for dialogue interaction attempt.
    /// Called by DialogueTrigger.OnTriggerEnter() to initiate interaction.
    /// 
    /// Logic: Only opens dialogue if player in range and not already active
    /// Called by: DialogueTrigger when player enters proximity
    /// </summary>
    public void TryInteract()
    {
        if (playerInRange && !dialogueActivated)
            OpenDialogue();
    }

    /// <summary>
    /// InputSystem callback: Interact action performed (E key or gamepad button).
    /// 
    /// Behavior:
    /// - If dialogue not active AND player in range: open dialogue
    /// - If dialogue active: end current line and advance
    /// 
    /// Called by: InputSystem when Interact action performed
    /// </summary>
    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!dialogueActivated && playerInRange)
            OpenDialogue();
        else if (dialogueActivated)
            EndDialogue();
    }

    /// <summary>
    /// InputSystem callback: Submit action performed (Space/Enter or gamepad confirm).
    /// 
    /// Behavior:
    /// - If dialogue active: end current line
    /// - Invokes endDialogueButton.onClick (button click logic)
    /// 
    /// Called by: InputSystem when Submit action performed
    /// </summary>
    private void OnSubmitPerformed(InputAction.CallbackContext ctx)
    {
        if (dialogueActivated)
        {
            EndDialogue();
            if (endDialogueButton != null)
                endDialogueButton.onClick.Invoke();
        }
    }

    /// <summary>
    /// Per-frame input polling (fallback for keyboard without InputSystem).
    /// Also provides immediate keyboard responsiveness for dialogue progression.
    /// 
    /// Logic:
    /// - If not active AND in range: Check E key to open
    /// - If active: Check Space/Enter to advance
    /// 
    /// Called by: Physics system each frame
    /// </summary>
    private void Update()
    {
        if (!dialogueActivated && playerInRange)
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                OpenDialogue();
        }
        else if (dialogueActivated)
        {
            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.spaceKey.wasPressedThisFrame))
            {
                EndDialogue();
                if (endDialogueButton != null)
                    endDialogueButton.onClick.Invoke();
            }
        }
    }

    // ===== DIALOGUE LIFECYCLE =====
    /// <summary>
    /// Activates dialogue UI and initializes dialogue state.
    /// 
    /// Actions:
    /// 1. Guard: Return early if already active
    /// 2. Set dialogueActivated flag
    /// 3. Hide interactPrompt
    /// 4. Cache PlayerInput if not cached
    /// 5. Show DialogueCanvas
    /// 6. Retrieve QuestNPC component from npc (for quest callbacks)
    /// 7. Lock NPC movement: npc.hardLocked = true, npc.StopMovement()
    /// 8. Lock player movement: examplePlayerController.movementLocked = true
    /// 9. Prioritize dialogue UI: EventSystem.SetSelectedGameObject(endDialogueButton)
    /// 10. Call LoadDialogueSet() to populate dialogue arrays
    /// 11. Call DisplayCurrentDialogue() to show first line
    /// 
    /// Cross-references:
    /// - Calls ExamplePlayerController.cs (movement locking)
    /// - Calls NPCController.cs (movement control and hardLocked flag)
    /// - Calls LoadDialogueSet() (array population)
    /// - Calls DisplayCurrentDialogue() (UI update)
    /// </summary>
    public void OpenDialogue()
    {
        if (dialogueActivated) return;

        dialogueActivated = true;

        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        if (inputSystem == null)
            inputSystem = FindFirstObjectByType<PlayerInput>();

        if (DialogueCanvas != null)
            DialogueCanvas.SetActive(true);

        // Get quest NPC component if dialogue is quest-related
        activeQuestNPC = npc.GetComponent<QuestNPC>();

        // Lock NPC movement
        if (npc != null)
        {
            npc.hardLocked = true;
            npc.StopMovement();
        }

        // Lock player movement (use cached controller to avoid FindObjectOfType)
        if (cachedPlayerController != null)
            cachedPlayerController.movementLocked = true;

        // Focus UI to dialogue button
        var eventSystem = EventSystem.current;
        if (eventSystem != null && endDialogueButton != null)
            eventSystem.SetSelectedGameObject(endDialogueButton.gameObject);

        // Load and display dialogue
        LoadDialogueSet();
        DisplayCurrentDialogue();
    }

    /// <summary>
    /// Loads dialogue content from QuestNPC's DialogueSet into arrays.
    /// 
    /// Actions:
    /// 1. Get DialogueSet from activeQuestNPC (if quest-related)
    /// 2. Resize speaker[], dialogue[], portrait[] arrays to match DialogueSet line count
    /// 3. Copy content from DialogueSet to arrays
    /// 4. Validate arrays with SanitizeDialogueSet()
    /// 5. Randomize starting index if multiple dialogue lines exist
    /// 6. Reset currentIndex to 0 for playback
    /// 
    /// Dependencies:
    /// - Requires activeQuestNPC to be set (done in OpenDialogue())
    /// - Calls SanitizeDialogueSet() for validation
    /// 
    /// Called by: OpenDialogue() after locking movement
    /// </summary>
    private void LoadDialogueSet()
    {
        if (activeQuestNPC == null)
            return;

        DialogueSet dialogueSet = activeQuestNPC.GetDialogueSet();
        if (dialogueSet == null)
            return;

        // Copy arrays directly
        speaker = dialogueSet.speaker;
        dialogue = dialogueSet.dialogue;
        portrait = dialogueSet.portraits;

        // Validate
        SanitizeDialogueSet();

        // Start at index 0
        currentIndex = 0;

        // Validate and normalize arrays
        SanitizeDialogueSet();

        // Randomize starting point if multiple lines
        if (dialogue.Length > 1)
            currentIndex = UnityEngine.Random.Range(0, dialogue.Length);
        else
            currentIndex = 0;
    }

    /// <summary>
    /// Validates and normalizes dialogue arrays for safe access.
    /// 
    /// Actions:
    /// 1. Ensure speaker[] is not null (resize to dialogue length if null)
    /// 2. Ensure dialogue[] is not null (throw error if null)
    /// 3. Ensure portrait[] is not null (resize to dialogue length if null)
    /// 4. Equalize all array lengths to match dialogue.Length
    /// 5. Replace null entries with defaults:
    ///    - speaker null → "Unknown"
    ///    - dialogue null → "(no dialogue)"
    ///    - portrait null → null (portrait image disabled if null)
    /// 
    /// Called by: LoadDialogueSet() before playback
    /// </summary>
    private void SanitizeDialogueSet()
    {
        if (dialogue == null || dialogue.Length == 0)
        {
            Debug.LogError("Dialogue array is null or empty!");
            return;
        }

        int length = dialogue.Length;

        // Ensure all arrays exist and are correct length
        if (speaker == null || speaker.Length != length)
            System.Array.Resize(ref speaker, length);

        if (portrait == null || portrait.Length != length)
            System.Array.Resize(ref portrait, length);

        // Replace nulls with defaults
        for (int i = 0; i < length; i++)
        {
            if (string.IsNullOrEmpty(speaker[i]))
                speaker[i] = "Unknown";

            if (string.IsNullOrEmpty(dialogue[i]))
                dialogue[i] = "(no dialogue)";

            // portrait[i] can remain null - image component will handle it
        }
    }

    /// <summary>
    /// Updates dialogue UI with current line content.
    /// 
    /// Actions:
    /// 1. Clamp currentIndex to valid range [0, dialogue.Length-1]
    /// 2. Get current line data: speaker[index], dialogue[index], portrait[index]
    /// 3. Update speakerText with speaker name (TMPro text)
    /// 4. Update dialogueText with dialogue content (TMPro text)
    /// 5. Update portraitImage:
    ///    - If portrait[index] exists: show image and assign sprite
    ///    - If portrait[index] is null: hide image
    /// 6. Handle null UI references gracefully (no errors if UI missing)
    /// 
    /// Called by: OpenDialogue() on initial load, EndDialogue() when advancing lines
    /// </summary>
    private void DisplayCurrentDialogue()
    {
        // Clamp to valid range
        currentIndex = Mathf.Clamp(currentIndex, 0, dialogue.Length - 1);

        string currentSpeaker = speaker[currentIndex];
        string currentLine = dialogue[currentIndex];
        Sprite currentPortrait = portrait[currentIndex];

        // Update speaker name
        if (speakerText != null)
            speakerText.text = currentSpeaker;

        // Update dialogue text
        if (dialogueText != null)
            dialogueText.text = currentLine;

        // Update portrait
        if (portraitImage != null)
        {
            if (currentPortrait != null)
            {
                portraitImage.sprite = currentPortrait;
                portraitImage.gameObject.SetActive(true);
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
            }
        }
    }

    // ===== DIALOGUE TERMINATION =====
    /// <summary>
    /// Deactivates dialogue UI and restores NPC/player state.
    /// 
    /// Actions:
    /// 1. Hide DialogueCanvas
    /// 2. Show interactPrompt if player is still in range
    /// 3. Clear dialogueActivated flag
    /// 4. Unlock NPC movement: npc.hardLocked = false, npc.ResumeMovement()
    /// 5. Unlock player movement: examplePlayerController.movementLocked = false
    /// 
    /// Cross-references:
    /// - Reverses ExamplePlayerController.cs movement lock
    /// - Reverses NPCController.cs movement lock
    /// 
    /// Called by: EndDialogue() for cleanup
    /// </summary>
    private void CloseDialogue()
    {
        if (DialogueCanvas != null)
            DialogueCanvas.SetActive(false);

        if (playerInRange && interactPrompt != null)
            interactPrompt.SetActive(true);

        dialogueActivated = false;

        if (npc != null)
        {
            npc.hardLocked = false;
            npc.ResumeMovement();
        }

        // Unlock player movement (use cached controller to avoid FindObjectOfType)
        if (cachedPlayerController != null)
            cachedPlayerController.movementLocked = false;
    }

    /// <summary>
    /// Terminates dialogue and triggers quest progression callbacks.
    /// 
    /// Actions:
    /// 1. Show interactPrompt if player still in range
    /// 2. Call activeQuestNPC.OnDialogueClosed() for quest state progression
    /// 3. Log dialogue completion to console
    /// 4. Call CloseDialogue() for UI/movement cleanup
    /// 
    /// Cross-references:
    /// - Calls QuestNPC.OnDialogueClosed() in QuestNPC.cs
    /// - Calls CloseDialogue() for state restoration
    /// 
    /// Called by: EndDialogueButton.onClick, OnInteractPerformed, OnSubmitPerformed
    /// </summary>
    public void EndDialogue()
    {
        if (playerInRange && interactPrompt != null)
            interactPrompt.SetActive(true);

        if (activeQuestNPC != null)
            activeQuestNPC.OnDialogueClosed();

        Debug.Log("[DialogueManager] EndDialogue called.");
        CloseDialogue();
    }
}
