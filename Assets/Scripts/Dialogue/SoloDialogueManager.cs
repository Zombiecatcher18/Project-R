using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SoloDialogueManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject DialogueCanvas;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Button endDialogueButton;

    [Header("Dialogue Content")]
    public string[] speaker;
    public string[] dialogue;
    public Sprite[] portrait;

    private int currentIndex = 0;

    [Header("Trigger")]
    [SerializeField] private Collider dialogueTrigger;
    public GameObject interactPrompt;

    // runtime
    private PlayerInput inputSystem;
    public bool playerInRange = false;
    public bool dialogueActivated = false;

    private ExamplePlayerController player;
    private SoloNPCController npc;  // <-- CORRECT NPC CONTROLLER

    private void Awake()
    {
        inputSystem = FindObjectOfType<PlayerInput>();

        player = FindAnyObjectByType<ExamplePlayerController>();
        npc = GetComponentInParent<SoloNPCController>();   // <-- Get NPC ON THE SAME PREFAB

        if (DialogueCanvas != null)
            DialogueCanvas.SetActive(false);

        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        if (dialogueTrigger == null)
            Debug.LogWarning("Dialogue Trigger not assigned! Assign the child collider manually.");

        if (npc == null)
            Debug.LogWarning("No SoloNPCController found in parent! NPC freezing will not work.");
    }

    private void OnEnable()
    {
        if (inputSystem != null)
        {
            var interact = inputSystem.actions.FindAction("Interact");
            if (interact != null)
                interact.performed += OnInteractPerformed;

            var submit = inputSystem.actions.FindAction("Submit");
            if (submit != null)
                submit.performed += OnSubmitPerformed;
        }
    }

    private void OnDisable()
    {
        if (inputSystem != null)
        {
            var interact = inputSystem.actions.FindAction("Interact");
            if (interact != null)
                interact.performed -= OnInteractPerformed;

            var submit = inputSystem.actions.FindAction("Submit");
            if (submit != null)
                submit.performed -= OnSubmitPerformed;
        }
    }

    private void OnDestroy() => OnDisable();

    private void Update()
    {
        // Press E to open dialogue
        if (playerInRange && !dialogueActivated)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame)
                OpenDialogue();
        }
        // Press Enter or Space to close
        else if (dialogueActivated)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame)
                EndDialogue();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!dialogueActivated && playerInRange)
            OpenDialogue();
        else if (dialogueActivated)
            EndDialogue();
    }

    private void OnSubmitPerformed(InputAction.CallbackContext ctx)
    {
        if (dialogueActivated)
            EndDialogue();
    }

    // -----------------------------------------------------
    //                  OPEN DIALOGUE
    // -----------------------------------------------------
    public void OpenDialogue()
    {
        if (dialogueActivated) return;

        dialogueActivated = true;

        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        if (DialogueCanvas != null)
            DialogueCanvas.SetActive(true);

        SanitizeArrays();
        PickRandomDialogue();
        DisplayCurrentDialogue();

        // Lock player movement
        if (player != null)
            player.movementLocked = true;

        // Freeze NPC movement THE CORRECT WAY
        if (npc != null)
            npc.EnterDialogue(); //<-- important

        // Set button selected
        if (endDialogueButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(endDialogueButton.gameObject);
    }

    private void SanitizeArrays()
    {
        if (speaker == null || speaker.Length == 0) speaker = new[] { "" };
        if (dialogue == null || dialogue.Length == 0) dialogue = new[] { "" };
        if (portrait == null || portrait.Length == 0) portrait = new Sprite[] { null };

        int max = Mathf.Max(speaker.Length, dialogue.Length, portrait.Length);
        Array.Resize(ref speaker, max);
        Array.Resize(ref dialogue, max);
        Array.Resize(ref portrait, max);
    }

    private void PickRandomDialogue()
    {
        currentIndex = (dialogue.Length > 1)
            ? UnityEngine.Random.Range(0, dialogue.Length)
            : 0;
    }

    private void DisplayCurrentDialogue()
    {
        dialogueText.text = dialogue[currentIndex];
        speakerText.text = speaker[currentIndex];

        if (portraitImage != null)
            portraitImage.sprite = portrait[currentIndex];
    }

    // -----------------------------------------------------
    //                  END DIALOGUE
    // -----------------------------------------------------
    public void EndDialogue()
    {
        CloseDialogue();
    }

    private void CloseDialogue()
    {
        if (DialogueCanvas != null)
            DialogueCanvas.SetActive(false);

        if (playerInRange && interactPrompt != null)
            interactPrompt.SetActive(true);

        dialogueActivated = false;

        // Unlock player movement
        if (player != null)
            player.movementLocked = false;

        // Resume NPC movement CORRECTLY
        if (npc != null)
            npc.ExitDialogue();
    }

    // -----------------------------------------------------
    //          TRIGGER DETECTION (ENTER / EXIT)
    // -----------------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;

            if (!dialogueActivated && interactPrompt != null)
                interactPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            if (interactPrompt != null)
                interactPrompt.SetActive(false);

            if (dialogueActivated)
                EndDialogue();
        }
    }
}
