using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance;

    [Header("UI References")]
    public GameObject pausePanel;
    public Button resumeButton;
    public Button exitButton;

    private GameInputManager input;
    private ExamplePlayerController playerMovementScript;
    private bool isOpen = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        HookInput();
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void HookInput()
    {
        input = GameInputManager.Instance;
        if (input != null)
        {
            input.OnPauseEvent -= OnPause;
            input.OnPauseEvent += OnPause;
        }
    }

    public void OnPause()
    {
        string scene = SceneManager.GetActiveScene().name;

        if (scene == "Main Menu" || scene == "BattleScene")
            return;

        // BLOCK if another UI is open (unless it's the pause menu itself)
        if (!isOpen && GlobalUIManager.Instance.OtherUIOpen("Pause"))
        {
            Debug.Log($"[UI BLOCKED] Pause Menu blocked because {GlobalUIManager.Instance.GetOpenUIName()} is open.");
            return;
        }

        if (!isOpen)
            GlobalUIManager.Instance.CloseAll();

        isOpen = !isOpen;
        GameStateManager.SetPause(isOpen, pausePanel, playerMovementScript);
    }

    public void ResumeButtonClicked()
    {
        isOpen = false;
        GameStateManager.SetPause(false, pausePanel, playerMovementScript);
    }

    public void ExitButtonClicked()
    {
        isOpen = false;
        GameStateManager.SetPause(false, pausePanel, playerMovementScript);
        SceneManager.LoadScene("Main Menu");
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Main Menu" || scene.name == "BattleScene")
        {
            // Hide only the UI panel, NOT the controller
            pausePanel?.SetActive(false);
            isOpen = false;

            if (scene.name == "Main Menu")
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            return;
        }

        // Gameplay scenes
        pausePanel?.SetActive(false);
        GameStateManager.ResetGameplayState();

        if (pausePanel == null)
            pausePanel = GameObject.Find("PauseMenu");

        HookInput();
        isOpen = false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (input != null)
            input.OnPauseEvent -= OnPause;
    }
}
