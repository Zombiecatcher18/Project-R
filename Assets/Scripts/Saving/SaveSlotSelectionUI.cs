using UnityEngine;

public class SaveSlotSelectionUI : MonoBehaviour
{
    public static SaveSlotSelectionUI Instance;

    public GameObject startButton;
    public GameObject deleteButton;
    public GameObject deleteConfirmPanel;

    private int selectedSlot = -1;

    private void Awake()
    {
        Instance = this;
        Debug.Log("[SaveSlotSelectionUI] Awake ran. Instance set.");
    }

    private void Start()
    {
        startButton.SetActive(false);
        deleteButton.SetActive(false);
        deleteConfirmPanel.SetActive(false);
    }

    public void SelectSlot(int slot)
    {
        selectedSlot = slot;
        startButton.SetActive(true);
        deleteButton.SetActive(true);
        Debug.Log("[SaveSlotSelectionUI] Selected slot: " + slot);
    }

    public void StartGame()
    {
        if (selectedSlot < 1) return;

        SaveData data = SaveManager.Load(selectedSlot);

        GameManager.Instance.currentSlot = selectedSlot;

        // If the save file does NOT exist → start new game
        if (data == null)
        {
            Debug.Log("[SaveSlotSelectionUI] No save found. Starting NEW GAME.");
        
            // Only clear inventory for a true NEW GAME
            GameManager.Instance.ClearWorldState();
            InventorySystem.current?.ClearAllInventory();
            QuestManager.Instance?.ClearQuestState();
            PlayerStats.Instance?.ResetStats();
            if (BountyManager.Instance != null)
                BountyManager.Instance.currentBounty = 0;
            // ===============================
            // STARTER DECK SETUP
            // ===============================
            var deck = PlayerMoveDeck.Instance;

            // Clear any leftover data
            deck.masterPool.Clear();
            deck.unlockedMoves.Clear();

            // Add starter moves to unlocked list
            //deck.unlockedMoves.Add(deck.punchMove);
            //deck.unlockedMoves.Add(deck.kickMove);
            //deck.unlockedMoves.Add(deck.hookMove);

            // Add starter moves to the active deck
            deck.masterPool.Add(deck.punchMove);
            deck.masterPool.Add(deck.kickMove);
            deck.masterPool.Add(deck.hookMove);

            Debug.Log("[SaveSlotSelectionUI] Starter deck initialized.");

            MainMenuLoader.StartNewGame(selectedSlot);
        }
        else
        {
            Debug.Log("[SaveSlotSelectionUI] Save found. LOADING GAME.");
        
            // DO NOT CLEAR INVENTORY HERE
            MainMenuLoader.LoadSlot(selectedSlot);
        }
    }

    public void OnDeletePressed()
    {
        deleteConfirmPanel.SetActive(true);
    }

    public void ConfirmDeleteYes()
    {
        SaveManager.Delete(selectedSlot);

        startButton.SetActive(false);
        deleteButton.SetActive(false);
        deleteConfirmPanel.SetActive(false);

        FindObjectOfType<SaveSlotPanel>().RefreshAllSlots();
    }

    public void ConfirmDeleteNo()
    {
        deleteConfirmPanel.SetActive(false);
    }
}
