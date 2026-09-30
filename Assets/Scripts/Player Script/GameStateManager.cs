using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameStateManager
{
    private static bool isPaused = false;
    private static bool isInventoryOpen = false;

    // --- Cursor control ---
    public static void SetCursorVisible(bool visible)
    {
        if (visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // --- Pause state ---
    public static void SetPause(bool open, GameObject pausePanel, ExamplePlayerController player)
    {
        isPaused = open;
        if (pausePanel != null)
            pausePanel.SetActive(open);

        if (player != null)
        {
            player.movementLocked = open;
            player.EnableInput(!open);
        }

        Time.timeScale = open ? 0f : 1f;
        SetCursorVisible(open);

        Debug.Log($"Pause {(open ? "opened" : "closed")}");
    }

    // --- Inventory state ---
    public static void SetInventory(bool open, GameObject inventoryPanel, ExamplePlayerController player)
    {
        bool inventoryIsOpen = inventoryPanel != null && inventoryPanel.activeSelf;

        // BLOCK if trying to open while another UI is open
        if (open && GlobalUIManager.Instance.OtherUIOpen("Inventory"))
        {
            Debug.Log($"[UI BLOCKED] Inventory blocked because {GlobalUIManager.Instance.GetOpenUIName()} is open.");
            return;
        }

        // If state isn't changing, do nothing
        if (open == inventoryIsOpen)
            return;

        // Actually open or close the UI
        if (open)
            InventoryUI.Instance.OpenInventory();
        else
            InventoryUI.Instance.CloseInventory();

        // Only change player + time if Inventory actually changed state
        if (player != null)
        {
            player.movementLocked = open;
            player.EnableInput(!open);
        }

        Time.timeScale = open ? 0f : 1f;
        SetCursorVisible(open);

        Debug.Log($"Inventory {(open ? "opened" : "closed")}");
    }
  
    // --- Reset when entering gameplay ---
    public static void ResetGameplayState()
    {
        isPaused = false;
        isInventoryOpen = false;
        Time.timeScale = 1f;
        SetCursorVisible(false);
    }
}
