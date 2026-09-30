using UnityEngine;
using UnityEngine.InputSystem;

namespace Maincharacter.PlayerController
{
    /// <summary>
    /// Handles player movement and interaction input from the new Input System.
    /// Implements IPlayerLocomotionMapActions to receive input callbacks.
    /// Movement input is consumed by ExamplePlayerController.cs for character motion.
    /// Interaction input is monitored by PlayerInteract.cs for world interactions.
    /// </summary>
    public class PlayerLocomotionInput : MonoBehaviour, Player.IPlayerLocomotionMapActions
    {
        /// <summary>Generated Input Action Map for player locomotion; created from InputSystem package.</summary>
        public Player Player { get; private set; }
        /// <summary>Current movement input vector [x, y]; consumed by ExamplePlayerController for velocity calculation.</summary>
        public Vector2 MovementInput { get; private set; }
        /// <summary>Flag indicating if interact button is currently pressed; monitored by PlayerInteract.cs.</summary>
        public bool InteractPressed { get; set; }
        public bool DeckPressed { get; private set; }
        /// <summary>Flag to disable movement input (e.g., during dialogue, battles); called by GameStateManager or BattleManager.</summary>
        public bool movementLocked = false;

        /// <summary>
        /// Enables Input Action Map and registers callbacks.
        /// Called by Unity when this component becomes enabled.
        /// </summary>
        private void OnEnable()
        {
            Player = new Player();
            Player.Enable();

            Player.PlayerLocomotionMap.Enable();
            Player.PlayerLocomotionMap.SetCallbacks(this);
        }

        /// <summary>
        /// Disables Input Action Map and removes callbacks.
        /// Called by Unity when this component becomes disabled.
        /// </summary>
        private void OnDisable()
        {
            Player.PlayerLocomotionMap.Disable();
            Player.PlayerLocomotionMap.RemoveCallbacks(this);
        }

        /// <summary>
        /// Input callback for movement input from WASD or analog stick.
        /// Clears input if movement is locked (by GameStateManager or BattleManager).
        /// Called by InputSystem whenever movement action value changes.
        /// </summary>
        public void OnMovement(InputAction.CallbackContext context)
        {
            if (movementLocked)
            {
                MovementInput = Vector2.zero;
                return;
            }
            MovementInput = context.ReadValue<Vector2>();
        }

        /// <summary>
        /// Input callback for interact button (typically E or gamepad button).
        /// Sets InteractPressed flag monitored by PlayerInteract.cs.
        /// Called by InputSystem when interact action is triggered or released.
        /// </summary>
        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                InteractPressed = true;
                Debug.Log("Interact button pressed!");
            }
            else if (context.canceled)
            {
                InteractPressed = false;
            }
        }

        public void OnDeck(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                DeckPressed = true;
            }
            else if (context.canceled)
            {
                DeckPressed = false;
            }
        }
    }
}

