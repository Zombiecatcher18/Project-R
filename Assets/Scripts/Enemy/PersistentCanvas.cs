using UnityEngine;

public class PersistentCanvas : MonoBehaviour
{
    private static PersistentCanvas instance;
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);     // Prevent duplicates
            return;
        }
            
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
