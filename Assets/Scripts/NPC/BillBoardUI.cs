using UnityEngine;

public class BillBoardUI : MonoBehaviour
{
    private Camera mainCam;
    public bool allowBillBoard = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainCam = Camera.main;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (!allowBillBoard)
            return;

        if (mainCam == null)
        {
            mainCam = Camera.main;
            return;
        }

        transform.LookAt(transform.position + mainCam.transform.rotation * Vector3.forward, mainCam.transform.rotation * Vector3.up);
    }
}
