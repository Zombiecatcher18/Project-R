using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class LockedTransport : MonoBehaviour
{
    [Header("Persistence")]
    public string doorID;

    [Header("Scene Setting")]
    public int sceneBuildingIndex;
    public string spawnIDOnNextScene;

    [Header("Door Key Settings")]
    public InventoryItemData requiredKey;

    private GameInputManager inputActions;
    private bool playerNearby = false;
    private SpriteRenderer spriteRenderer;
    private GameObject playerObj;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        StartCoroutine(WaitForInputManager());

        if (GameManager.Instance.IsDoorOpen(doorID))
        {
            OpenDoorVisual();
        }
    }

    private IEnumerator WaitForInputManager()
    {
        yield return new WaitUntil(() => GameInputManager.Instance != null);
        inputActions = GameInputManager.Instance;
        inputActions.OnInteractEvent += OnPlayerInteract;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = true;
            playerObj = other.gameObject;
            Debug.Log("Player near locked door. Press E to interact.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;
            playerObj = null;
        }
    }

    private void TryOpenDoor()
    {
        if (GameManager.Instance.IsDoorOpen(doorID))
        {
            LoadNextScene();
            return;
        }

        InventorySystem playerInventory = InventorySystem.current;

        if (playerInventory == null)
        {
            Debug.LogWarning("No InventorySystem found!");
            return;
        }

        if (requiredKey != null)
        {
            if (playerInventory.HasItem(requiredKey))
            {
                Debug.Log("Door unlocked using " + requiredKey.name);
                playerInventory.RemoveItem(requiredKey);
                GameManager.Instance.SetDoorOpen(doorID);
                OpenDoorVisual();
                LoadNextScene();
            }
            else
            {
                Debug.Log("Door is locked. You need the " + requiredKey.name + ".");
            }
        }
        else
        {
            GameManager.Instance.SetDoorOpen(doorID);
            OpenDoorVisual();
            LoadNextScene();
        }
    }

    public void OnPlayerInteract()
    {
        if (playerNearby)
        {
            TryOpenDoor();
        }
    }

    private void OnDestroy()
    {
        if (inputActions != null)
            inputActions.OnInteractEvent -= OnPlayerInteract;
    }

    private void OpenDoorVisual()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
    }

    private void LoadNextScene()
    {
        GameManager.Instance.lastSpawnID = spawnIDOnNextScene;
        SceneManager.LoadScene(sceneBuildingIndex);
    }
}
