using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] 
    private DialogueManager dialogueManager;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (dialogueManager != null && dialogueManager.playerInRange)
            {
                dialogueManager.OpenDialogue();
                return;
            }
        }
    }
}
