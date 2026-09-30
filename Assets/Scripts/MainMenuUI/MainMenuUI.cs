using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using TMPro;
using Unity.VisualScripting;

public class MainMenuUI : MonoBehaviour
{
    public int _sceneBuildingIndex;
    public GameObject saveSlotPanel;
    public GameObject creditPanel;
    public GameObject infoPanel;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void StartButton()
    {
        // Load gameplay scene
        if (saveSlotPanel != null)
        {
            saveSlotPanel.SetActive(true);
            //Debug.Log("[MainMenuUI] Save slot panel opened.");
        }
        else
        {
            //Debug.LogWarning("[MainMenuUI] Save slot panel not assigned.");
        }

        // Reset time scale
        Time.timeScale = 1f;

        // Cursor should be hidden in gameplay by default
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        //Debug.Log("Start button clicked");
    }

    public void CreditButton()
    {
        if (creditPanel != null)
        {
            creditPanel.SetActive(true);
        }
    }

    public void InfoButton()
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
        }
    }

    public void InfoCloseButton()
    {
        infoPanel.SetActive(false);
    }

    public void CloseButton()
    {
        creditPanel.SetActive(false);
    }

    public void ExitButton()
    {
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
        //Debug.Log("Exit button clicked");
    }
}
