using Character.ExamplePlayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

public class ExamplePlayerController : MonoBehaviour, ExamplePlayer.IPlayerLocomotionMapActions
{
    [Header("Components")]
    public CharacterController characterController;

    [Header("Movement")]
    public float walkSpeed = 3f;
    public float sprintSpeed = 6f;
    public float gravity = 25f;
    public float terminalVelocity = 50f;

    private ExamplePlayer input;
    private Vector2 movementInput;
    private float verticalVelocity;

    private bool inputEnabled = true;
    public bool movementLocked = false;
    private bool isSprinting = false;
    public Vector2 MovementInput => movementInput;

    private void Awake()
    {
        input = new ExamplePlayer();
        input.PlayerLocomotionMap.SetCallbacks(this);
        input.PlayerLocomotionMap.Enable();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Hidden until gameplay scene
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (input != null)
        {
            input.PlayerLocomotionMap.RemoveCallbacks(this);
            input.PlayerLocomotionMap.Disable();
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (!inputEnabled || movementLocked)
            return;

        HandleMovement();
    }

    public void EnableInput(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
            movementInput = Vector2.zero;
    }

    private void HandleMovement()
    {
        if (!characterController.enabled) return;

        bool grounded = characterController.isGrounded;

        Vector3 moveDir =
            transform.forward * movementInput.y +
            transform.right * movementInput.x;

        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        float speed = isSprinting ? sprintSpeed : walkSpeed;
        Vector3 lateral = moveDir * speed;

        if (grounded)
        {
            if (verticalVelocity < 0)
                verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
            if (verticalVelocity < -terminalVelocity)
                verticalVelocity = -terminalVelocity;
        }

        Vector3 velocity = lateral;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    public void OnMovement(InputAction.CallbackContext context)
    {
        movementInput = movementLocked ? Vector2.zero : context.ReadValue<Vector2>();
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        if (context.performed)
            isSprinting = true;
        if (context.canceled)
            isSprinting = false;
    }

    private void OnEnable()
    {
        if (input == null)
            input = new ExamplePlayer();

        input.PlayerLocomotionMap.SetCallbacks(this);
        input.PlayerLocomotionMap.Enable();
    }

    private void OnDisable()
    {
        if (input != null)
            input.PlayerLocomotionMap.Disable();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Main Menu")
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // Fresh scene spawn (new game or no save position yet)
        if (GameManager.Instance == null || GameManager.Instance.pendingLoadData == null)
        {
            movementLocked = false;

            GameObject spawn = GameObject.FindWithTag("PlayerSpawn");
            if (spawn != null)
            {
                characterController.enabled = false;
                transform.position = spawn.transform.position;
                transform.rotation = spawn.transform.rotation;
                characterController.enabled = true;
            }
            return;
        }

        // If we got here, GameManager will handle position via ApplySaveData()
        // We only care about sitting state for movement lock.
        SaveData data = GameManager.Instance.pendingLoadData;

        if (data.wasSitting)
        {
            movementLocked = true;
            StartCoroutine(AutoStandAfterLoad());
        }
        else
        {
            movementLocked = false;
        }
    }

    private IEnumerator AutoStandAfterLoad()
    {
        yield return new WaitForSeconds(1.5f);
        movementLocked = false;
    }
}
