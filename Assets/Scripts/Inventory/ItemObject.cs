using System.Collections;
using Character.ExamplePlayer;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Represents a single item instance in the world that player can pick up.
/// Manages pickup detection, inventory addition, and persistence state.
/// Coordinates with InventorySystem.cs for item storage and GameManager for pickup tracking.
/// </summary>
public class ItemObject : MonoBehaviour
{
    /// <summary>Reference to GameInputManager for listening to interact events; cached during Start().</summary>
    private GameInputManager inputActions;
    /// <summary>Scriptable object definition of this item; specifies properties like stack size and usage.</summary>
    public InventoryItemData referenceItem;
    /// <summary>Flag indicating if player is within trigger range for pickup.</summary>
    private bool playerNearby = false;

    /// <summary>
    /// Initializes item detection by waiting for system managers to be ready.
    /// Launches coroutines to find GameInputManager and check if item already picked up.
    /// </summary>
    private void Start()
    {
        StartCoroutine(WaitForInputManager());
        StartCoroutine(InitializeItem());
    }

    /// <summary>
    /// Waits for GameInputManager to become available, then registers for interact events.
    /// Called from Start() as a coroutine.
    /// References GameInputManager.cs for interaction callback system.
    /// </summary>
    private IEnumerator WaitForInputManager()
    {
        yield return new WaitUntil(() => GameInputManager.Instance != null);
        inputActions = GameInputManager.Instance;
        inputActions.OnInteractEvent += OnPlayerInteract;
    }

    /// <summary>
    /// Waits for GameManager and InventorySystem to initialize, then checks if item was already picked up.
    /// If item ID exists in GameManager.pickedUpItemIDs, destroys this instance to prevent duplicate spawning.
    /// Called from Start() as a coroutine.
    /// References GameManager for persistent state and InventorySystem for item storage.
    /// </summary>
    private IEnumerator InitializeItem()
    {
        Debug.Log($"[ItemObject] Initializing {referenceItem.itemName}...");
        yield return new WaitUntil(() => GameManager.Instance != null && InventorySystem.current != null);
        Debug.Log($"[ItemObject] GameManager and InventorySystem ready for {referenceItem.itemName}");

        // Check if this item was already picked up in a previous save
        if (GameManager.Instance.pickedUpItemIDs.Contains(referenceItem.itemID))
        {
            Debug.Log($"[ItemObject] {referenceItem.itemName} already picked up, destroying.");
            Destroy(gameObject);
            yield break;
        }
        Debug.Log($"[ItemObject] {referenceItem.itemName} is available for pickup.");
    }

    /// <summary>
    /// Called when player enters item trigger collider.
    /// Sets playerNearby flag for Update() to detect interact input.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = true;
            Debug.Log($"[ItemObject] Player entered trigger for {referenceItem.itemName}");
        }
    }

    /// <summary>
    /// Called when player exits item trigger collider.
    /// Clears playerNearby flag, stopping pickup detection.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;
            Debug.Log($"[ItemObject] Player exited trigger for {referenceItem.itemName}");
        }
    }

    private void Update()
    {
        if (InventorySystem.current == null)
        {
            Debug.Log($"[ItemObject] InventorySystem.current is null for {referenceItem.itemName}");
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.Log($"[ItemObject] GameManager.Instance is null for {referenceItem.itemName}");
            return;
        }
    }

    private void TryPickUp()
    {
        if (InventorySystem.current == null)
        {
            Debug.LogWarning($"[ItemObject] Cannot pick up {referenceItem.itemName} — InventorySystem.current is null. Starting WaitAndPickUp.");
            StartCoroutine(WaitAndPickUp());
            return;
        }

        Debug.Log($"[ItemObject] Attempting to add {referenceItem.itemName} to inventory...");
        AddToInventory();
    }

    private void AddToInventory()
    {
        bool added = InventorySystem.current.AddItem(referenceItem);
        if (added)
        {
            if (!GameManager.Instance.pickedUpItemIDs.Contains(referenceItem.itemID))
                GameManager.Instance.pickedUpItemIDs.Add(referenceItem.itemID);

            InventorySystem.current.Notify();
            Debug.Log($"[ItemObject] {referenceItem.itemName} successfully picked up and added to inventory.");
            Destroy(gameObject);
        }
        else
        {
            Debug.Log($"[ItemObject] Inventory full! Cannot pick up {referenceItem.itemName}");
        }
    }

    private IEnumerator WaitAndPickUp()
    {
        Debug.Log($"[ItemObject] Waiting for InventorySystem to initialize for {referenceItem.itemName}...");
        while (InventorySystem.current == null || GameManager.Instance == null)
        {
            yield return null;
        }
        Debug.Log($"[ItemObject] InventorySystem and GameManager ready, picking up {referenceItem.itemName}.");
        AddToInventory();
    }

    private void OnPlayerInteract()
    {
        if (playerNearby)
        {
            TryPickUp();
        }
    }

    private void OnDestroy()
    {
        if (inputActions != null)
            inputActions.OnInteractEvent -= OnPlayerInteract;
    }
}
