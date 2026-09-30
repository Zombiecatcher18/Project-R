using UnityEngine;

public class CanvasSingleton : MonoBehaviour
{
    public static CanvasSingleton Instance;

    private void Awake()
    {
        // If another instance exists, destroy this one
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Register this instance
        Instance = this;

        // Persist across scenes
        DontDestroyOnLoad(gameObject);
    }
}
