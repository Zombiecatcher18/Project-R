using System.Data.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PerformGameRoot
{
    const string SceneName = "GameRoot";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Execute()
    {
        // traverse the currently loaded scenes
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; ++sceneIndex)
        {
            var candidate = SceneManager.GetSceneAt(sceneIndex);

            if (candidate.name == SceneName)
                return;
        }
        // Additively load the bootstrap scene
        SceneManager.LoadScene(SceneName, LoadSceneMode.Additive);
    }
}

public class GameRootData : MonoBehaviour
{
    public static GameRootData Instance { get; private set; } = null;
    void Awake()
    {
        // Check if instance already exist
        if (Instance != null)
        {
            Debug.LogError("Found another GameRootData on GameObject on " + gameObject.name);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // prevent the data from being unloaded
        DontDestroyOnLoad(gameObject);
    }
}
