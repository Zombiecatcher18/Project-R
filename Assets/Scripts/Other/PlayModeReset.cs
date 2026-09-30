//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEngine;
//using UnityEditor.Callbacks;
//#endif

//[InitializeOnLoad]
//public class PlayModeReset : MonoBehaviour
//{
    //static PlayModeReset()
    //{
        //EditorApplication.playModeStateChanged += OnPlayModeChanged;
    //}

    //private static void OnPlayModeChanged(PlayModeStateChange state)
    //{
        //Debug.Log("[PlayModeReset] Resetting BattleData and EnemyManager for fresh play session.");
        //BattleData.enemies.Clear();

        //if (EnemyManager.Instance != null)
            //{
                //UnityEngine.Debug.Log("[PlayModeReset] Clearing EnemyManager defeatedEnemies.");
                //EnemyManager.Instance.ClearDefeatedEnemies();
            //}
    //}
//}
