using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MakeTransparentScript : MonoBehaviour
{
    [SerializeField] private List<I_am_In_the_way> currentInTheWay;
    [SerializeField] private List<I_am_In_the_way> alreadyTransparent;
    [SerializeField] private Transform player;
    private Transform cameraTransform;

    private void Awake()
    {
        currentInTheWay = new List<I_am_In_the_way>();
        alreadyTransparent = new List<I_am_In_the_way>();

        cameraTransform = this.gameObject.transform;
        TryAssignPlayer();
    }

    private void Update()
    {
        // Ensure we have references; try to find them if null
        if (player == null || cameraTransform == null)
        {
            TryAssignPlayer();
            if (player == null || cameraTransform == null) return;
        }

        GetAllObjectsInTheWay();

        MakeObjectsSolid();
        MakeObjectsTransparent();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // When a new scene loads, try to find the player again (player may be recreated)
        TryAssignPlayer();
    }

    private void TryAssignPlayer()
    {
        if (player != null) return;
        var found = GameObject.FindWithTag("Player");
        if (found != null)
        {
            player = found.transform;
        }
    }

    private void GetAllObjectsInTheWay()
    {
        currentInTheWay.Clear();

        float cameraPlayerDistance = Vector3.Distance(cameraTransform.position, player.position);

        Ray ray1_Forward = new Ray(cameraTransform.position, player.position - cameraTransform.position);
        Ray ray1_Backward = new Ray(player.position, cameraTransform.position - player.position);

        var hits1_Forward = Physics.RaycastAll(ray1_Forward, cameraPlayerDistance);
        var hits1_Backward = Physics.RaycastAll(ray1_Backward, cameraPlayerDistance);

        foreach (var hit in hits1_Forward)
        {
            if (hit.collider.gameObject.TryGetComponent(out I_am_In_the_way inTheWay))
            {
                if (!currentInTheWay.Contains(inTheWay))
                {
                    currentInTheWay.Add(inTheWay);
                }
            }
        }

        foreach (var hit in hits1_Backward)
        {
            if (hit.collider.gameObject.TryGetComponent(out I_am_In_the_way inTheWay))
            {
                if (!currentInTheWay.Contains(inTheWay))
                {
                    currentInTheWay.Add(inTheWay);
                }
            }
        }
    }

    private void MakeObjectsTransparent()
    {
        for (int i = 0; i < currentInTheWay.Count; i++)
        {
            I_am_In_the_way inTheWay = currentInTheWay[i];

            if (!alreadyTransparent.Contains(inTheWay))
            {
                inTheWay.ShowTransparent();
                alreadyTransparent.Add(inTheWay);
            }
        }
    }
    
    private void MakeObjectsSolid()
    {
        for (int i = alreadyTransparent.Count-1; i >= 0; i--)
        {
            I_am_In_the_way wasInTheWay = alreadyTransparent[i];

            if(!currentInTheWay.Contains(wasInTheWay))
            {
                wasInTheWay.ShowSolid();
                alreadyTransparent.Remove(wasInTheWay);
            }
        }
    }
}
