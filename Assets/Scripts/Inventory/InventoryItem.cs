#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Callbacks;
using UnityEngine;
#endif


[System.Serializable]
public class InventoryItem
{
    public InventoryItemData data;
    public System.DateTime pickedUpTime;

    public int quanity;

    public InventoryItem(InventoryItemData data, int quantity)
    {
        this.data = data;
        this.quanity = quantity;
        this.pickedUpTime = System.DateTime.Now;
        //Debug.Log($"[InventoryItem] Created {data.itemID} x{quantity}");
    }
}