using UnityEngine;
using UnityEngine.UI;

public class BattleInventoryButton : MonoBehaviour
{
    public Button inventoryButton;

    private InventoryToggle inventoryToggle;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    /* void Start()
    {
        inventoryToggle = FindObjectOfType<InventoryToggle>();

        if (inventoryToggle != null && inventoryButton != null)
        {
            inventoryButton.onClick.AddListener(ToggleInventory);
        }
        else
        {
            Debug.LogWarning("InventoryToggle or Button not found in BattleScene.");
        }
    } */

    // Update is called once per frame
    //void ToggleInventory()
    //{
        //if (inventoryToggle != null)
        //{
            //inventoryToggle.ToggleInventory();
        //}
    //}
}
