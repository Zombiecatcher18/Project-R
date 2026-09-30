using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ItemEntryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // -------------------------
    // MODES
    // -------------------------
    public enum ItemEntryMode
    {
        Inventory,
        Battle
    }

    private ItemEntryMode mode;

    // -------------------------
    // UI References
    // -------------------------
    [Header("UI References")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text quantityText;

    // -------------------------
    // References
    // -------------------------
    private InventoryItem item;
    private InventoryUI parentUI;          // for overworld inventory
    private BattleManager battleManager;   // for battle inventory
    private BattleInventoryUI battleInventoryUI;

    // ============================================================
    // SETUP METHODS
    // ============================================================

    // Overworld inventory setup
    public void Setup(InventoryUI ui, InventoryItem newItem)
    {
        mode = ItemEntryMode.Inventory;
        parentUI = ui;
        item = newItem;

        ApplyVisuals();
    }

    // Battle inventory setup
    public void SetupForBattle(BattleManager manager, InventoryItem newItem)
    {
        mode = ItemEntryMode.Battle;
        battleManager = manager;
        battleInventoryUI = manager.battleInventoryUI;
        item = newItem;

        ApplyVisuals();
    }

    // Shared visual setup
    private void ApplyVisuals()
    {
        if (item == null || item.data == null)
        {
            Clear();
            return;
        }

        iconImage.enabled = true;
        iconImage.sprite = item.data.icon;
        nameText.text = item.data.itemName;

        if (item.data.isStackable)
        {
            quantityText.text = item.quanity.ToString();
            quantityText.gameObject.SetActive(true);
        }
        else
        {
            quantityText.gameObject.SetActive(false);
        }
    }

    // ============================================================
    // CLEAR
    // ============================================================
    public void Clear()
    {
        iconImage.enabled = false;
        iconImage.sprite = null;
        nameText.text = "";
        quantityText.gameObject.SetActive(false);
    }

    // ============================================================
    // TOOLTIP HANDLING
    // ============================================================
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (mode == ItemEntryMode.Inventory && item != null)
            parentUI.ShowTooltip(item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (mode == ItemEntryMode.Inventory)
            parentUI.HideTooltip();
    }

    // ============================================================
    // CLICK HANDLING
    // ============================================================
    public void OnPointerClick(PointerEventData eventData)
    {
        if (item == null)
            return;

        switch (mode)
        {
            case ItemEntryMode.Inventory:
                parentUI.OnItemClicked(item);
                break;

            case ItemEntryMode.Battle:
                battleInventoryUI.SelectItem(item);
                break;
        }
    }
}
