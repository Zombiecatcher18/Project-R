using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem current;

    public InventoryUI persistentUI;

    [Header("Inventory Categories")]
    public List<InventoryItem> consumables = new List<InventoryItem>();
    public List<InventoryItem> keyItems = new List<InventoryItem>();
    public List<InventoryItem> specialItems = new List<InventoryItem>();
    public List<InventoryItem> battleItems = new List<InventoryItem>();
    public List<InventoryItem> gear = new List<InventoryItem>(); // rename if needed

    public InventoryItem equippedAccessory; // optional
    public bool isLoadingFromSave = false;


    public int maxConsumables = 10;

    public event Action OnInventoryChanged;

    private void Awake()
    {
        Debug.Log("[InventorySystem] Awake called on: " + gameObject.name);
        Debug.Log("[InventorySystem] Hash: " + GetHashCode());

        if (current != null && current != this)
        {
            Debug.LogWarning("[InventorySystem] Duplicate detected. Destroying this instance.");
            Destroy(gameObject);
            return;
        }

        current = this;
        Debug.Log("[InventorySystem] Instance set to: " + gameObject.name);
        DontDestroyOnLoad(gameObject);

        if (persistentUI == null)
            persistentUI = FindObjectOfType<InventoryUI>();
    }

    public void Notify() => OnInventoryChanged?.Invoke();

    // ============================
    // ADD ITEM
    // ============================
    public bool AddItem(InventoryItemData itemData, int quantity = 1)
    {
        Debug.Log($"[InventorySystem] AddItem called for {itemData.itemID} x{quantity}, category={itemData.category}");

        if (itemData == null)
        {
            Debug.LogWarning("[InventorySystem] Tried to add null itemData.");
            return false;
        }

        Debug.Log($"[InventorySystem] AddItem called for {itemData.itemID} x{quantity}, category={itemData.category}");

        switch (itemData.category)
        {
            case ItemCategory.Consumable:
                return AddToList(consumables, itemData, quantity, maxConsumables);
            case ItemCategory.KeyItems:
                return AddToList(keyItems, itemData, quantity, int.MaxValue);
            case ItemCategory.SpecialItems:
                return AddToList(specialItems, itemData, quantity, int.MaxValue);
            case ItemCategory.BattleItems:
                return AddToList(battleItems, itemData, quantity, int.MaxValue);
            case ItemCategory.Gear:
                return AddToList(gear, itemData, quantity, int.MaxValue);
            case ItemCategory.Currency:
                equippedAccessory = new InventoryItem(itemData, 1);
                Notify();
                return true;
            default:
                Debug.LogWarning($"[InventorySystem] Unknown category for item {itemData.itemID}: {itemData.category}");
                return false;
        }
    }

    private bool AddToList(List<InventoryItem> list, InventoryItemData data, int quantity, int maxSlots)
    {
        Debug.Log($"[InventorySystem] AddToList called for {data.itemID} x{quantity}, stackable={data.isStackable}, maxStackSize={data.maxStackSize}");

        if (data == null)
        {
            Debug.LogWarning("[InventorySystem] Tried to add null itemData.");
            return false;
        }

        if (data.isStackable && data.maxStackSize <= 0)
        {
            Debug.LogWarning($"[InventorySystem] Stackable item {data.itemID} has maxStackSize <= 0. Defaulting to 1.");
            data.maxStackSize = 1;
        }

        // Try to stack onto existing items
        if (data.isStackable)
        {
            foreach (var item in list)
            {
                if (item.data.itemID == data.itemID && item.quanity < data.maxStackSize)
                {
                    int space = data.maxStackSize - item.quanity;
                    int add = Mathf.Min(space, quantity);
                    item.quanity += add;
                    quantity -= add;

                    Debug.Log($"[InventorySystem] Stacked {add} onto existing {data.itemID}. New quantity: {item.quanity}");

                    if (quantity <= 0)
                    {
                        Notify();
                        return true;
                    }
                }
            }
        }

        // Add new item entries
        while (quantity > 0 && list.Count < maxSlots)
        {
            int add = data.isStackable ? Mathf.Min(quantity, data.maxStackSize) : 1;

            if (add <= 0)
            {
                Debug.LogWarning($"[InventorySystem] Skipping {data.itemID} due to zero quantity.");
                break;
            }

            list.Add(new InventoryItem(data, add));
            Debug.Log($"[InventorySystem] Added new {data.itemID} x{add} to {data.category}");

            quantity -= add;
        }

        Notify();
        return quantity <= 0;
    }   

    // ============================
    // REMOVE ITEM
    // ============================
    public void RemoveItem(InventoryItemData itemData)
    {
        List<List<InventoryItem>> lists = new()
        {
            consumables,
            keyItems,
            specialItems,
            battleItems,
            gear
        };

        foreach (var list in lists)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item.data.itemID == itemData.itemID)
                {
                    item.quanity--;

                    if (item.quanity <= 0)
                        list.RemoveAt(i);

                    Notify();
                    return;
                }
            }
        }

        Debug.LogWarning("Tried to remove item not found: " + itemData.itemName);
    }

    // ============================
    // CHECK ITEM
    // ============================
    public bool HasItem(InventoryItemData itemData)
    {
        foreach (var list in new[] { consumables, keyItems, specialItems, battleItems, gear })
        {
            foreach (var item in list)
            {
                if (item.data.itemID == itemData.itemID)
                    return true;
            }
        }
        return false;
    }

    // ============================
    // SAVE / LOAD
    // ============================
    public void SaveInventory()
    {
        GameManager.Instance.savedInventory.Clear();

        foreach (var list in new[] { consumables, keyItems, specialItems, battleItems, gear })
        {
            foreach (var item in list)
            {
                GameManager.Instance.savedInventory.Add(new GameManager.SavedInventoryItem
                {
                    itemID = item.data.itemID,
                    quantity = item.quanity
                });
            }
        }

        Debug.Log("[InventorySystem] Saved inventory.");
    }

    public void LoadInventoryFromSave(List<SaveData.InventoryItemSave> savedItems)
    {
        isLoadingFromSave = true;

        Debug.Log("[InventorySystem] LoadInventoryFromSave STARTED");
        Debug.Log($"[InventorySystem] Current instance: {gameObject.name}");

        consumables.Clear();
        keyItems.Clear();
        specialItems.Clear();
        battleItems.Clear();
        gear.Clear();

        Debug.Log("[InventorySystem] Cleared all inventory lists");

        foreach (var saved in savedItems)
        {
            Debug.Log($"[InventorySystem] Attempting to restore {saved.itemID} x{saved.quantity}");

            var data = Resources.Load<InventoryItemData>($"Items/{saved.itemID}");
            if (data == null)
            {
                Debug.LogWarning($"[InventorySystem] Could not find InventoryItemData for id '{saved.itemID}'");
                continue;
            }

            bool success = AddItem(data, saved.quantity);

            if (success)
            {
                var list = GetListForCategory(data.category);
                if (list != null && list.Count > 0)
                {
                    var restoredItem = list[list.Count - 1]; // last added
                    restoredItem.pickedUpTime = System.DateTime.Parse(saved.pickedUpTime);
                }
            }
        }

        Debug.Log($"[InventorySystem] After restore: consumables={consumables.Count}, keyItems={keyItems.Count}, special={specialItems.Count}, battle={battleItems.Count}, gear={gear.Count}");

        isLoadingFromSave = false;
        Notify();
    }

    public void ClearAllInventory()
    {
        if (isLoadingFromSave)
        {
            Debug.LogWarning("[InventorySystem] Prevented ClearAllInventory during load.");
            return;
        }

        Debug.Log("[InventorySystem] ClearAllInventory called!");

        consumables.Clear();
        keyItems.Clear();
        specialItems.Clear();
        battleItems.Clear();
        gear.Clear();
        equippedAccessory = null;
        Notify();
    }

    private void OnEnable()
    {
        Debug.Log("[InventorySystem] OnEnable called for: " + gameObject.name);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (persistentUI == null)
        {
            persistentUI = FindObjectOfType<InventoryUI>();
            if (persistentUI != null)
                Notify();
        }
    }

    private List<InventoryItem> GetListForCategory(ItemCategory cat)
    {
        switch (cat)
        {
            case ItemCategory.Consumable: return consumables;
            case ItemCategory.KeyItems: return keyItems;
            case ItemCategory.SpecialItems: return specialItems;
            case ItemCategory.BattleItems: return battleItems;
            case ItemCategory.Gear: return gear;
        }
        return null;
    }   
}
