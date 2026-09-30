using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ToolTipUI : MonoBehaviour
{
    [Header("References")]
    public GameObject root;          // The tooltip panel root (set inactive by default)
    public Image iconImage;          // <-- renamed for clarity
    public TMP_Text nameText;
    public TMP_Text descriptionText;     // optional (add to InventoryItemData)

    private void Awake()
    {
        if (root != null)
            root.SetActive(false);
    }

    public void Show(InventoryItem item)
    {
        if (item == null || item.data == null)
        {
            Hide();
            return;
        }

        if (root != null)
            root.SetActive(true);

        // Display the item icon in the tooltip
        if (iconImage != null)
        {
            if (item.data.icon != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = item.data.icon;
            }
            else
            {
                iconImage.enabled = false;
            }
        }

        // Set the item name text in the tooltip
        if (nameText != null)
            nameText.text = item.data.itemName;

        if (descriptionText != null)
        {
            descriptionText.text = item.data.description;
            descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(item.data.description));
        }
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }
}