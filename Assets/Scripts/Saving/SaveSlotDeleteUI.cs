using UnityEngine;

public class SaveSlotDeleteUI : MonoBehaviour
{
    public GameObject deleteButton;
    private int selectedSlot = -1;

    private void Start()
    {
        deleteButton.SetActive(false);
    }

    public void ShowDeleteForSlot(int slot)
    {
        selectedSlot = slot;
        deleteButton.SetActive(true);
    }

    public void HideDelete()
    {
        deleteButton.SetActive(false);
        selectedSlot = -1;
    }

    public void DeleteSelectedSlot()
    {
        if (selectedSlot < 1) return;

        SaveManager.Delete(selectedSlot);
        Debug.Log("[SaveSlotDeleteUI] Deleted slot " + selectedSlot);

        HideDelete();

        // Refresh all slot buttons
        FindObjectOfType<SaveSlotPanel>().RefreshAllSlots();
    }
}
