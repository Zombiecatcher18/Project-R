using UnityEngine;

public static class CursorManager
{
    private static bool isVisible = false;

    public static void Show()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isVisible = true;
        Debug.Log("[CursorManager] Cursor shown");
    }

    public static void Hide()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isVisible = false;
        Debug.Log("[CursorManager] Cursor hidden");
    }

    public static void Toggle()
    {
        if (isVisible)
            Hide();
        else
            Show();
    }

    public static void ForceUnlock()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isVisible = true;
    }
}

