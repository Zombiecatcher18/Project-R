using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotButton : MonoBehaviour
{
    public int slotIndex; // 1, 2, or 3
    public TMP_Text label;

    private bool hasSave;
    private SaveSlotDeleteUI deleteUI;

    private void Start()
    {  
        deleteUI = FindObjectOfType<SaveSlotDeleteUI>();
        Refresh();
    }

    public void Refresh()
    {
        SaveData data = SaveManager.Load(slotIndex);

        if (data == null)
        {
            label.text = $"Slot {slotIndex}: Empty";
            hasSave = false;
        }
        else
        {
            label.text = $"Slot {slotIndex}: Continue\n{data.timestamp}";
            hasSave = true;
        }
    }

    public void OnClick()
    {
        deleteUI.ShowDeleteForSlot(slotIndex);

        GameManager.Instance.currentSlot = slotIndex;

        // Select this slot 
        SaveSlotSelectionUI.Instance.SelectSlot(slotIndex);
    }
}
