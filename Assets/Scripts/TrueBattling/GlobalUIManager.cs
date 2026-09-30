using UnityEngine;

public class GlobalUIManager : MonoBehaviour
{
    public static GlobalUIManager Instance;

    public DeckBuilderUI deckUI;
    public InventoryUI inventoryUI;
    public PauseMenuController pauseUI;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool IsDeckOpen()
    {
        return deckUI != null && deckUI.deckPanel != null && deckUI.deckPanel.activeSelf;
    }

    public bool IsInventoryOpen()
    {
        return inventoryUI != null && inventoryUI.gameObject.activeSelf;
    }

    public bool IsPauseOpen()
    {
        return pauseUI != null && pauseUI.pausePanel != null && pauseUI.pausePanel.activeSelf;
    }

    public bool AnyUIOpen()
    {
        return IsDeckOpen() || IsInventoryOpen() || IsPauseOpen();
    }

    public string GetOpenUIName()
    {
        if (IsDeckOpen()) return "Deck Builder";
        if (IsInventoryOpen()) return "Inventory";
        if (IsPauseOpen()) return "Pause Menu";
        return "None";
    }   

    public bool OtherUIOpen(string self)
    {
        bool deck = IsDeckOpen() && self != "Deck";
        bool inv  = IsInventoryOpen() && self != "Inventory";
        bool pause = IsPauseOpen() && self != "Pause";

        return deck || inv || pause;
    }   

    public void CloseAll()
    {
        if (deckUI != null && deckUI.deckPanel != null)
            deckUI.deckPanel.SetActive(false);
        if (inventoryUI != null) inventoryUI.gameObject.SetActive(false);
        if (pauseUI != null && pauseUI.pausePanel != null)
            pauseUI.pausePanel.SetActive(false);
    }
}
