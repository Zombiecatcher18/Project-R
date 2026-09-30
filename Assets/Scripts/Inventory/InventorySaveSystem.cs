//using System;
//using System.Collections.Generic;
//using System.IO;
//using UnityEngine;

//public class InventorySaveSystem : MonoBehaviour
//{
    //private string savePath;

    //[System.Serializable]
    //private class SaveData
    //{
        //public List<string> itemIDs = new List<string>();
        //public List<int> stackSizes = new List<int>();
    //}

    //private void Awake()
    //{
        //savePath = Path.Combine(Application.persistentDataPath, "inventory.json");
        //LoadInventory();
    //}

    //public void SaveInventory()
    //{
        //if (InventorySystem.current == null) return;

        //SaveData data = new SaveData();

        //oreach (var item in InventorySystem.current.inventory)
        //{
            //data.itemIDs.Add(item.data.id);
            //data.stackSizes.Add(item.stackSize);
        //}

        //string json = JsonUtility.ToJson(data, true)
        //File.WriteAllText(savePath, json);

        //Debug.Log($"Invetory saved to: {savePath}");
    //}

    //public void LoadInventory()
    //{

    //}
    
    //private InventoryItemData FindItemDataByID(string id)
    //{
        
    //}
//}
