using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Camzone : MonoBehaviour
{
    [SerializeField]
    private CinemachineCamera virtualCamera = null;

    private void Start()
    {
        virtualCamera.enabled = false;
    }

    private void OnTiggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            virtualCamera.enabled = false;
        print("Contact");
    }

    private void OnTiggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            virtualCamera.enabled = true;
    }

    private void Oalidate()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}
