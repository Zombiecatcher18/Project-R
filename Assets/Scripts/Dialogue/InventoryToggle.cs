using UnityEngine;
using UnityEngine.SceneManagement;

public class InventoryToggle : MonoBehaviour
{
    private static InventoryToggle instance;

    [Header("Reference")]
    public GameObject inventoryUIRoot;
    private ExamplePlayerController playerMovementScript;

    private GameInputManager inputActions;
    private bool isOpen = false;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        DontDestroyOnLoad(gameObject);
        
        // Cache GameInputManager once to avoid per-use FindObjectOfType calls
        inputActions = FindObjectOfType<GameInputManager>();
    }

    void Start()
    {
        HookInput();
        if (inventoryUIRoot != null)
            inventoryUIRoot.SetActive(false);
    }

    private void HookInput()
    {
        if (inputActions == null)
            inputActions = FindObjectOfType<GameInputManager>();
        
        if (inputActions != null)
        {
            inputActions.OnInventoryEvent -= OnPlayerInventory; // avoid duplicates
            inputActions.OnInventoryEvent += OnPlayerInventory;
        }
    }

    public void OnPlayerInventory()
    {
        if (IsSceneBlocked())
            return;

        isOpen = !isOpen;
        GameStateManager.SetInventory(isOpen, inventoryUIRoot, playerMovementScript);

        if (isOpen && inventoryUIRoot != null)
        {
            var ui = inventoryUIRoot.GetComponent<InventoryUI>();
            if (ui != null)
                ui.ResetToStateA();
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsSceneBlocked())
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        GameStateManager.ResetGameplayState();

        if (inventoryUIRoot == null)
            inventoryUIRoot = GameObject.Find("InventoryUI");

        if (inventoryUIRoot != null)
            inventoryUIRoot.SetActive(false);

        HookInput();
        isOpen = false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (inputActions != null)
            inputActions.OnInventoryEvent -= OnPlayerInventory;
    }

    private bool IsSceneBlocked()
    {
        string scene = SceneManager.GetActiveScene().name;
        return scene == "Main Menu" || scene == "BattleScene";
    }
}
