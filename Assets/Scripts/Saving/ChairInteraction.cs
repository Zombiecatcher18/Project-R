using UnityEngine;
using UnityEngine.InputSystem;

public class ChairInteraction : MonoBehaviour
{
    public Transform seatPoint;
    public SavePointUI saveUI;

    private bool playerInRange = false;
    private GameObject playerObj;
    private ExamplePlayerController playerController;
    private CharacterController cc;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            playerObj = other.gameObject;
            playerController = playerObj.GetComponent<ExamplePlayerController>();
            cc = playerObj.GetComponent<CharacterController>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            playerObj = null;
        }
    }

    private void Update()
    {
        if (!playerInRange || playerObj == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            SitPlayer();
        }
    }

    private void SitPlayer()
    {
        // Lock movement
        playerController.movementLocked = true;

        // Teleport to seat
        cc.enabled = false;
        playerObj.transform.position = seatPoint.position;
        playerObj.transform.rotation = seatPoint.rotation;
        cc.enabled = true;

        // Tell Save UI which chair we're using
        saveUI.currentChair = this;

        // Show save panel
        saveUI.Show();
    }

    public void StandUp()
    {
        if (playerController != null)
            playerController.movementLocked = false;
    }

    public void AutoStand(float delay)
    {
        StartCoroutine(AutoStandRoutine(delay));
    }

    private System.Collections.IEnumerator AutoStandRoutine(float delay) 
    { 
        yield return new WaitForSeconds(delay); 
        StandUp(); 
    }
}
