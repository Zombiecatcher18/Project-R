using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class SaveTester : MonoBehaviour
{
    public Transform player;
    public int testSlot = 1;

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Press F5 to Save
        if (Keyboard.current.f5Key.wasPressedThisFrame)
        {
            SaveGame();
        }

        // Press F9 to Load
        if (Keyboard.current.f9Key.wasPressedThisFrame)
        {
            LoadGame();
        }
    }

    private void SaveGame()
    {
        if (player == null)
        {
            Debug.LogError("[SaveTester] Player reference not set!");
            return;
        }

        SaveData data = new SaveData();

        // Scene
        data.sceneName = SceneManager.GetActiveScene().name;

        // Position
        Vector3 pos = player.position;
        data.playerPosition = new float[] { pos.x, pos.y, pos.z };

        // Timestamp
        data.timestamp = System.DateTime.Now.ToString();

        SaveManager.Save(GameManager.Instance.currentSlot, data);
        Debug.Log($"[SaveTester] Saved position: {pos}");
    }

    private void LoadGame()
    {
        SaveData data = SaveManager.Load(testSlot);
        if (data == null)
        {
            Debug.LogWarning("[SaveTester] No save data to load.");
            return;
        }

        if (player == null)
        {
            Debug.LogError("[SaveTester] Player reference not set!");
            return;
        }

        if (data.playerPosition != null && data.playerPosition.Length == 3)
        {
            Vector3 newPos = new Vector3(
                data.playerPosition[0],
                data.playerPosition[1],
                data.playerPosition[2]
            );

            // Disable CharacterController before teleporting
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                player.position = newPos;
                cc.enabled = true;
                Debug.Log("[SaveTester] Teleported player using CharacterController.");
            }
            else
            {
                player.position = newPos;
                Debug.Log("[SaveTester] Teleported player without CharacterController.");
            }
        }
    }
}
