using UnityEngine;

public class NPCInteractionPrompt : MonoBehaviour
{
    public GameObject promptCanvas;
    private bool playerInside;

    private void Start()
    {
        promptCanvas.SetActive(false);
    }

    private void Update()
    {
        if (playerInside)
        {
            FaceCamera();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
            promptCanvas.SetActive(true);
        }
    }

    private void OnTriggeExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
            promptCanvas.SetActive(false);
        }
    }

    private void FaceCamera()
    {
        var cam = Camera.main.transform;
        promptCanvas.transform.LookAt(promptCanvas.transform.position + cam.forward);
    }
}
