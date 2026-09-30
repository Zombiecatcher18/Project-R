using UnityEngine;

public class FPSScript : MonoBehaviour
{
    void Awake()
    {
        Application.targetFrameRate = 30;
        QualitySettings.vSyncCount = 1;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
