using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class SavePointUI : MonoBehaviour
{
    public static SavePointUI Instance;

    public GameObject panel;
    public ChairInteraction currentChair;

    private void Awake()
    {
        Instance = this;   // IMPORTANT: ensures LevelUpUI can access it if needed
    }

    private void Start()
    {
        panel.SetActive(false);
    }

    public void Show()
    {
        StartCoroutine(ShowNextFrame());
    }

    private IEnumerator ShowNextFrame()
    {
        yield return null; // wait 1 frame

        panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // SAVE ONLY (stay in game)
    public void SaveOnly()
    {
        SaveData data = SaveManager.BuildSaveData();
        data.wasSitting = false;

        SaveManager.Save(GameManager.Instance.currentSlot, data);

        Debug.Log("[SavePointUI] Game saved.");

        if (currentChair != null)
            currentChair.StandUp();

        Hide();
    }

    // SAVE + QUIT TO MAIN MENU
    public void SaveAndQuit()
    {
        SaveData data = SaveManager.BuildSaveData();
        data.wasSitting = true;

        SaveManager.Save(GameManager.Instance.currentSlot, data);

        Debug.Log("[SavePointUI] Game saved. Returning to Main Menu.");

        SceneManager.LoadScene("Main Menu");
    }
}
