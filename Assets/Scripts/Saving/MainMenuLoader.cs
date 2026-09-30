using UnityEngine;
using UnityEngine.SceneManagement;

public static class MainMenuLoader
{
    public static void LoadSlot(int slot)
    {
        SaveData data = SaveManager.Load(slot);
        if (data == null)
        {
            //Debug.LogError("[MainMenuLoader] Tried to load empty slot.");
            return;
        }
        
        //Debug.Log($"[MainMenuLoader] Slot {slot} loaded. inventoryItems.Count = {data.inventoryItems.Count}");

        GameManager.Instance.currentSlot = slot;
        GameManager.Instance.loadingFromSave = true;
        GameManager.Instance.pendingLoadData = data;

        //GameManager.Instance.ClearWorldState();
        //InventorySystem.current?.ClearAllInventory();
        //QuestManager.Instance?.ClearQuestState();
        
        /* if (InventorySystem.current != null) 
        { 
            Debug.Log("[MainMenuLoader] InventorySystem exists, loading inventory now."); 
            InventorySystem.current.LoadInventoryFromSave(data.inventoryItems); 
        } 
        else 
        {  
            Debug.Log("[MainMenuLoader] InventorySystem is NULL, will rely on GameManager.ApplySaveData later."); 
        } */
            
        // Load world state BEFORE scene loads 
        GameManager.Instance.LoadWorldState(data);
    
        SceneManager.LoadScene(data.sceneName);
    }

    public static void StartNewGame(int slot)
    {
        GameManager.Instance.currentSlot = slot;
        GameManager.Instance.pendingLoadData = null;
        GameManager.Instance.loadingFromSave = false;

        SceneManager.LoadScene("TestScene");
    }
}
