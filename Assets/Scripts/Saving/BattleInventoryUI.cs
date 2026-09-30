using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class BattleInventoryUI : MonoBehaviour
{
    public Transform itemContainer;
    public GameObject itemButtonPrefab;

    public Button useButton;

    private InventoryItem selectedItem;
    private BattleManager battleManager;

    private void Start()
    {
        useButton.onClick.AddListener(OnUseButton);
    }

    public void OnUseButton()
    {
        if (selectedItem != null)
        {
            battleManager.UseBattleItem(selectedItem);
            ClearSelection();
        }
    }

    public void Init(BattleManager manager)
    {
        battleManager = manager;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        ClearSelection(); // NEW
        Refresh();
    }

    public void SelectItem(InventoryItem item)
    {
        selectedItem = item;
        useButton.interactable = true;
    }

    public void ClearSelection()
    {
        selectedItem = null;
        useButton.interactable = false;
    }

    public void Hide()
    {
        ClearSelection(); // NEW
        gameObject.SetActive(false);
    }

    public void OnCloseButton()
    {
        battleManager.CloseBattleInventory();
    }

    private void Refresh()
    {
        foreach (Transform child in itemContainer)
            Destroy(child.gameObject);

        var inv = InventorySystem.current;

        // Only show usable items
        var usable = new List<InventoryItem>();
        usable.AddRange(inv.consumables);
        usable.AddRange(inv.battleItems);

        foreach (var item in usable)
        {
            var go = Instantiate(itemButtonPrefab, itemContainer);
            var ui = go.GetComponent<ItemEntryUI>();
            ui.SetupForBattle(battleManager, item);
        }
    }
}

