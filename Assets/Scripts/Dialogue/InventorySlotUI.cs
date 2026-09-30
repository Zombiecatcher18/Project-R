/* using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TMP_Text stackText;

    private InventoryUI parentUI;
    private int slotIndex = -1;

    // Drag icon
    private Canvas parentCanvas;
    private GameObject dragIcon;
    private RectTransform dragIconRect;

    // Store which slot is currently being dragged
    public static InventorySlotUI draggingSlot = null;

    private bool isPointerOver = false;

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        iconImage.preserveAspect = true;
    }

    // Called when inventory rebuilds or slot order changes
    public void Setup(InventoryUI parent, int index)
    {
        parentUI = parent;
        slotIndex = index;
        Clear();
    }

    // Assign or update item visuals
    public void SetItem(InventoryItem item)
    {
        if (item == null || item.data == null)
        {
            Clear();
            return;
        }

        iconImage.enabled = true;
        iconImage.sprite = item.data.icon;
        iconImage.rectTransform.sizeDelta = new Vector2(64, 64);

        if (item.data.isStackable && item.quanity > 1)
        {
            stackText.text = item.quanity.ToString();
            stackText.gameObject.SetActive(true);
        }
        else
        {
            stackText.gameObject.SetActive(false);
        }
    }

    public void Clear()
    {
        iconImage.enabled = false;
        iconImage.sprite = null;
        stackText.gameObject.SetActive(false);
    }

    public void RefreshItem(InventoryItem item)
    {
        SetItem(item);
        if (isPointerOver)
        {
            parentUI.ShowTooltip(slotIndex);
        }
    }

    // -------------------------
    // Tooltip Events
    // -------------------------

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        parentUI.ShowTooltip(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        parentUI.HideTooltip();
    }

    // -------------------------
    // Click Events
    // -------------------------

    public void OnPointerClick(PointerEventData eventData)
    {
        parentUI.OnSlotClicked(slotIndex);
    }

    // -------------------------
    // Drag Events
    // -------------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        var item = parentUI.GetItemAt(slotIndex);
        if (item == null || item.data == null)
            return;

        draggingSlot = this;

        // Create temporary drag icon
        dragIcon = new GameObject("DragIcon");
        dragIcon.transform.SetParent(parentCanvas.transform, false);

        var img = dragIcon.AddComponent<Image>();
        img.sprite = item.data.icon;
        img.raycastTarget = false;

        dragIconRect = dragIcon.GetComponent<RectTransform>();
        dragIconRect.sizeDelta = new Vector2(64, 64);

        parentUI.ShowTooltip(slotIndex);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIconRect != null)
            dragIconRect.position = eventData.position;

        parentUI.ShowTooltip(slotIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
            Destroy(dragIcon);

        draggingSlot = null;
        parentUI.HideTooltip();
    }

    // -------------------------
    // Drop Event
    // -------------------------

    public void OnDrop(PointerEventData eventData)
    {
        if (draggingSlot == null || draggingSlot == this)
            return;

        // Swap the two slots
        parentUI.SwapItems(draggingSlot.slotIndex, slotIndex);

        isPointerOver = true;
        parentUI.ShowTooltip(slotIndex);
    }

    private void OnDisable()
    {
        parentUI?.HideTooltip();
    }

    public void CancelDragIcon()
    {
        if (dragIcon != null)
        {
            Destroy(dragIcon);
            dragIcon = null;
        }
    }
} */
