using UnityEngine;

[System.Serializable]
public class EnemyPartySlot
{
    public GameObject prefab;   // The prefab to spawn
    public EnemyInfo info;      // ScriptableObject with stats
    public int count = 1;       // How many of this prefab to spawn
}