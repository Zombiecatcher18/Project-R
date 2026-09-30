using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Character.ExamplePlayer;

public class GameInputManager : MonoBehaviour
{
    private ExamplePlayer inputActions;  // Input Action Asset
    
    public static GameInputManager Instance { get; private set; }
    public event Action OnInventoryEvent;
    public event Action OnInteractEvent;
    public event Action OnPauseEvent;
    public event Action OnDeckEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    
        inputActions = new ExamplePlayer();

        // --- SYSTEM INPUTS ---
        inputActions.System.Inventory.performed += ctx =>
        {
            OnInventoryEvent?.Invoke();
        };

        inputActions.System.Interact.performed += ctx =>
        {
            OnInteractEvent?.Invoke();
        };

        inputActions.System.Pause.performed += ctx =>
        {
            OnPauseEvent?.Invoke();
        };
        inputActions.System.Deck.performed += ctx =>
        {
            OnDeckEvent?.Invoke();
        };

        inputActions.System.Enable(); // Enable only this map!
    }

    private void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.System.Disable();
        }
    }
}
