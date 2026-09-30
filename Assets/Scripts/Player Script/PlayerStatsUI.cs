using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject statsPanel;
    public ExamplePlayerController playerController;

    public TMP_Text healthText;
    public TMP_Text levelText;
    public TMP_Text xpText;

    private bool isOpen = false;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Try to auto-find the player controller if not assigned in inspector
        if (playerController == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerController = playerObj.GetComponent<ExamplePlayerController>();
        }
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name == "BattleScene")
            return;

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            ToggleStatsPanel();
        }
    }

    private void ToggleStatsPanel()
    {
        isOpen = !isOpen;
        statsPanel.SetActive(isOpen);

        if (playerController != null)
        {
            // CORRECT BEHAVIOR: when the stats are open (isOpen == true),
            // movementLocked should be true so the player cannot move.
            playerController.movementLocked = isOpen;

            // Also disable generic input via the controller method if available
            // (EnableInput expects true when input should be allowed).
            playerController.EnableInput(!isOpen);
        }

        if (isOpen)
            RefreshStats();
    }

    public void RefreshStats()
    {
        PlayerStats p = PlayerStats.Instance;

        healthText.text = $"Health: {p.currentHealth}/{p.MaxHealth}";
        levelText.text = $"Level: {p.level}";
        xpText.text = $"XP: {p.currentBounty}/{p.bountyToNextLevel}";
    }
}
