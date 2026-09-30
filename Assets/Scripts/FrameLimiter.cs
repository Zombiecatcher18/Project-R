using UnityEngine;

public class FrameLimiter : MonoBehaviour
{
    void Awake()
    {
        QualitySettings.vSyncCount = 1;   // Use VSync
        Application.targetFrameRate = 30; // Backup cap
    }
}
