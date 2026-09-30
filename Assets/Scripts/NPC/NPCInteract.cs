using UnityEngine;

public class NPCInteract : MonoBehaviour
{
    private DialogueManager dialogueManager;

    [System.Obsolete]
    void Start()
    {
        dialogueManager = FindObjectOfType<DialogueManager>();
    }

    public void Interact()
    {
        if (dialogueManager == null)
        {
            Debug.LogWarning("DialogueManager not assigned!");
            return;
        }

        // Must be inside trigger to interact
        if (!dialogueManager.playerInRange)
            return;

        // Already open? Don't reopen.
        if (dialogueManager.dialogueActivated)
            return;

        dialogueManager.OpenDialogue();
    }
}
