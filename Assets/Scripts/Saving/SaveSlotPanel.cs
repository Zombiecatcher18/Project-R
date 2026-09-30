using UnityEngine;

public class SaveSlotPanel : MonoBehaviour
{
    public SaveSlotButton[] slotButtons;

    private void OnEnable()
    {
        RefreshAllSlots();
    }

    public void RefreshAllSlots()
    {
        foreach (var button in slotButtons)
        {
            button.Refresh();
        }
    }
}
