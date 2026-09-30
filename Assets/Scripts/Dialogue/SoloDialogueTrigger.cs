using UnityEngine;

public class SoloDialogueTrigger : MonoBehaviour
{
    public SoloDialogueManager manager;
    public SoloNPCController npc;   // <-- ADD THIS

    private void Awake()
    {
        manager = GetComponentInParent<SoloDialogueManager>();
        npc = GetComponentInParent<SoloNPCController>();    // <-- automatically find NPC

        if (manager == null)
            Debug.LogError("SoloDialogueTrigger could NOT find SoloDialogueManager on parent object!");

        if (npc == null)
            Debug.LogError("SoloDialogueTrigger could NOT find SoloNPCController on parent object!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            manager.playerInRange = true;

            if (!manager.dialogueActivated && manager.interactPrompt != null)
                manager.interactPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            manager.playerInRange = false;

            if (manager.interactPrompt != null)
                manager.interactPrompt.SetActive(false);

            // If player walks away, ensure dialogue closes AND NPC resumes movement
            if (manager.dialogueActivated)
            {
                manager.EndDialogue();
                npc.ExitDialogue();   // <-- RESUME NPC
            }
        }
    }
}
