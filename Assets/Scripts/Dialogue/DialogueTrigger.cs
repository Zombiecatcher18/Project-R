using UnityEngine;

/// <summary>
/// Proximity trigger for NPC dialogue initiation.
/// Detects when player enters range and activates interaction prompt.
/// Coordinates with DialogueManager for dialogue state management.
/// </summary>
public class DialogueTrigger : MonoBehaviour
{
    /// <summary>Reference to DialogueManager; found from parent hierarchy on startup.</summary>
    public DialogueManager manager;

    /// <summary>
    /// Initializes DialogueManager reference from parent on scene load.
    /// Called by Unity before first frame; caches manager for rapid checks.
    /// </summary>
    private void Awake()
    {
        manager = GetComponentInParent<DialogueManager>();
    }

    /// <summary>
    /// Detects player entering dialogue range trigger.
    /// Sets playerInRange flag and shows interaction prompt if dialogue not active.
    /// Called by physics system when collider enters this trigger.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            manager.playerInRange = true;
            
            // Show interaction prompt only if dialogue not already active
            if (!manager.dialogueActivated && manager.interactPrompt != null)
                manager.interactPrompt.SetActive(true);  
        }
         
    }

    /// <summary>
    /// Detects player exiting dialogue range trigger.
    /// Clears playerInRange flag and hides interaction prompt.
    /// Called by physics system when collider exits this trigger.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            manager.playerInRange = false;

            // Hide interaction prompt when out of range
            if (manager.interactPrompt != null)
                manager.interactPrompt.SetActive(false);
        }
    }
}
