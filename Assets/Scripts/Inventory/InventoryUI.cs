using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InventoryUI : MonoBehaviour
{
    [Header("Tab UI Roots (ScrollView + Content)")]
    public GameObject consumablesUI;
    public GameObject keyItemsUI;
    public GameObject specialItemsUI;
    public GameObject battleItemsUI;
    public GameObject gearUI;

    [Header("Category Containers (Content objects)")]
    public Transform consumablesContainer;
    public Transform keyItemsContainer;
    public Transform specialItemsContainer;
    public Transform battleItemsContainer;
    public Transform gearContainer;

    [Header("Item Details Panel")]
    public GameObject itemDetailsPanel;
    public UnityEngine.UI.Image detailsIcon;
    public TMPro.TextMeshProUGUI detailsName;
    public TMPro.TextMeshProUGUI detailsCategory;
    public TMPro.TextMeshProUGUI detailsDescription;

    [Header("Navigation")]
    public GameObject tabButtonsGroup;
    public GameObject backButton;

    [Header("History (All Items)")]
    public GameObject historyUI;
    public Transform historyContainer;

    [Header("Tab Buttons (in same order as tabs)")]
    public GameObject[] tabButtons; // 0 = Consumables, 1 = KeyItems, etc.

    [Header("Prefabs & Tooltip")]
    public GameObject itemEntryPrefab;
    public ToolTipUI tooltipUI;

    public static InventoryUI Instance;

    private int lastOpenedTab = -1; 


    private GameObject[] tabUIs;
    private Transform[] tabContainers;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (itemDetailsPanel != null) 
            itemDetailsPanel.SetActive(false);

        tabUIs = new[]
        {
            consumablesUI,
            keyItemsUI,
            specialItemsUI,
            battleItemsUI,
            gearUI
        };

        tabContainers = new[]
        {
            consumablesContainer,
            keyItemsContainer,
            specialItemsContainer,
            battleItemsContainer,
            gearContainer,
            historyContainer
        };
    }

    private void Start()
    {
        StartCoroutine(Initialize());
    }

    public void OpenInventory()
    {
        string scene = SceneManager.GetActiveScene().name;

        if (scene == "Main Menu" || scene == "BattleScene")
            return;

        // BLOCK if ANY other UI is open
        if (GlobalUIManager.Instance.OtherUIOpen("Inventory"))
        {
            Debug.Log($"[UI BLOCKED] Inventory blocked because {GlobalUIManager.Instance.GetOpenUIName()} is open.");
            return;
        }

        // Normal open
        GlobalUIManager.Instance.CloseAll();
        gameObject.SetActive(true);
        Refresh();
        ResetToStateA();
    }

    public void CloseInventory()
    {
        HideTooltip();
        gameObject.SetActive(false);
    }

    private IEnumerator Initialize()
    {
        yield return new WaitUntil(() => InventorySystem.current != null);

        InventorySystem.current.OnInventoryChanged -= Refresh;
        InventorySystem.current.OnInventoryChanged += Refresh;

        Refresh();
        ResetToStateA();
    }

    private void OnEnable()
    {
        if (InventorySystem.current != null)
        {
            InventorySystem.current.OnInventoryChanged -= Refresh;
            InventorySystem.current.OnInventoryChanged += Refresh;
            Refresh();
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (InventorySystem.current != null)
            InventorySystem.current.OnInventoryChanged -= Refresh;

        HideTooltip();
    }

    private void OnDestroy()
    {
        if (InventorySystem.current != null)
            InventorySystem.current.OnInventoryChanged -= Refresh;

        if (Instance == this)
            Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (gameObject.activeInHierarchy)
            Refresh();
    }

    // ============================
    // TAB HANDLING
    // ============================
    private void HideAllTabUIs()
    {
        foreach (var tab in tabUIs)
            if (tab) tab.SetActive(false);
    }

    private void ShowOnlyButton(int index)
    {
        if (tabButtons == null || tabButtons.Length == 0) return;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            tabButtons[i].SetActive(i == index);
        }
    }

    private void ShowAllButtons()
    {
        if (tabButtons == null) return;

        foreach (var btn in tabButtons)
            if (btn) btn.SetActive(true);
    }

    private void ShowTabInternal(int index)
    {
        lastOpenedTab = index; // remember which tab we came from

        HideAllTabUIs();
        historyUI.SetActive(false);

        if (index >= 0 && index < tabUIs.Length && tabUIs[index] != null)
            tabUIs[index].SetActive(true);

        ShowOnlyButton(index);
        backButton.SetActive(true);
    }

    public void ShowConsumablesTab()   => ShowTabInternal(0);
    public void ShowKeyItemsTab()      => ShowTabInternal(1);
    public void ShowSpecialItemsTab()  => ShowTabInternal(2);
    public void ShowBattleItemsTab()   => ShowTabInternal(3);
    public void ShowGearTab()          => ShowTabInternal(4);

    public void Back()
    {
        lastOpenedTab = -1; // back to history mode

        HideAllTabUIs();
        historyUI.SetActive(true);
        ShowAllButtons();
        backButton.SetActive(false);

        Canvas.ForceUpdateCanvases();
    }

    public void CloseItemDetails()
    {
        itemDetailsPanel.SetActive(false);

        if (lastOpenedTab == -1)
        {
            // We came from history
            ResetToStateA();
        }
        else
        {
            // We came from a tab
            ShowTabInternal(lastOpenedTab);
        }
    }

    public void ResetToStateA()
    {
        HideAllTabUIs();

        historyUI.SetActive(true);   // show history
        ShowAllButtons();            // show all tab buttons
        backButton.SetActive(false); // hide back button

        Canvas.ForceUpdateCanvases();
    }

    // ============================
    // REFRESH DISPLAY
    // ============================
    public void Refresh()
    {
        if (InventorySystem.current == null || !gameObject.activeInHierarchy)
            return;

        ClearAllContainers();
        PopulateAllTabs();
        PopulateHistory();
    }
    private void ClearAllContainers()
    {
        foreach (var container in tabContainers)
            Clear(container);
    }

    private void PopulateAllTabs()
    {
        var inv = InventorySystem.current;
        if (inv == null) return;

        PopulateTab(consumablesContainer, inv.consumables);
        PopulateTab(keyItemsContainer, inv.keyItems);
        PopulateTab(specialItemsContainer, inv.specialItems);
        PopulateTab(battleItemsContainer, inv.battleItems);
        PopulateTab(gearContainer, inv.gear);
    }

    private void PopulateTab(Transform container, System.Collections.IEnumerable items)
    {
        if (container == null || items == null) return;

        foreach (var obj in items)
        {
            var item = obj as InventoryItem;
            if (item != null)
                CreateEntry(container, item);
        }
    }

    private void PopulateHistory()
    {
        Clear(historyContainer);

        var inv = InventorySystem.current;
        if (inv == null) return;

        // Combine all items into one list
        var allItems = new System.Collections.Generic.List<InventoryItem>();

        allItems.AddRange(inv.consumables);
        allItems.AddRange(inv.keyItems);
        allItems.AddRange(inv.specialItems);
        allItems.AddRange(inv.battleItems);
        allItems.AddRange(inv.gear);

        // Newest items first
        // Sort newest → oldest
        allItems.Sort((a, b) => b.pickedUpTime.CompareTo(a.pickedUpTime));

        foreach (var item in allItems)
            CreateEntry(historyContainer, item);
    }

    public void ShowItemDetails(InventoryItem item)
    {
        if (item == null || item.data == null) return;

        // Fill UI
        detailsIcon.sprite = item.data.icon;
        detailsName.text = item.data.itemName;
        detailsCategory.text = item.data.category.ToString();
        detailsDescription.text = item.data.description;

        // Show panel
        itemDetailsPanel.SetActive(true);

        // Hide everything else
        HideAllTabUIs();
        historyUI.SetActive(false);
        ShowAllButtons(); // optional: hide buttons if you want
        backButton.SetActive(true);
    }

    private void Clear(Transform container)
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    private void CreateEntry(Transform container, InventoryItem item)
    {
        if (container == null || itemEntryPrefab == null || item == null) return;

        var go = Instantiate(itemEntryPrefab, container);
        var ui = go.GetComponent<ItemEntryUI>();
        if (ui != null)
            ui.Setup(this, item);
    }

    // ============================
    // TOOLTIP
    // ============================
    public void ShowTooltip(InventoryItem item)
    {
        if (tooltipUI == null || item == null) return;
        tooltipUI.Show(item);
    }

    public void HideTooltip()
    {
        tooltipUI?.Hide();
    }

    // ============================
    // CLICK HANDLING
    // ============================
    public void OnItemClicked(InventoryItem item)
    {
        if (item == null || item.data == null) return;

        // Fill UI
        detailsIcon.sprite = item.data.icon;
        detailsName.text = item.data.itemName;
        detailsCategory.text = item.data.category.ToString();
        detailsDescription.text = item.data.description;

        // Show details panel
        itemDetailsPanel.SetActive(true);

        // Hide everything else
        HideAllTabUIs();
        historyUI.SetActive(false);
        ShowAllButtons();

        // Hide tab back button while viewing details
        backButton.SetActive(false);
    }
}
