/* using UnityEditor.Callbacks;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.EventSystems;

public class Player : MonoBehaviour
{

    [Header("Movement")]
    public float moveSpeed;
    public float walkSpeed;
    public float sprintSpeed;

    [Header("Keybinds")]
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Ground")]
    public float playerHeight;
    public bool isGrounded;
    public float groundCheckDistance = 0.3f;
    private Vector3 groundNormal;
    private float gravityMultiplier = 3.0f;

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopehit;

    public Rigidbody body;

    Vector3 Direction;
    private float velocity;

    //Store the current state the player is in
    public MovementState state;

    public enum MovementState
    {
        walking,
        sprinting,
        air,
    }

    void Update()
    {
        //Calls Function
        GroundCheck();
        StateHandler();
        SnapToGround();

        RaycastHit slopeHit;

        if (OnSlope(out slopeHit))
        {
            body.useGravity = false;
        }
        else
        {
            body.useGravity = true;
        }
    }

    void FixedUpdate()
    {
        //Input
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized;

        //Turn Off Gravity while on slope
        body.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);

        //Movement direction releative to slope
        Direction = transform.TransformDirection(input);

        if (OnSlope(out slopehit) && isGrounded)
        {
            Direction = Vector3.ProjectOnPlane(Direction, slopehit.normal).normalized;
        }

        // Apply movement (preserve vertical velocity unless grounded snap applies)
        Vector3 targetVelocity = Direction * moveSpeed;
        Vector3 currentVelocity = body.linearVelocity;
        body.linearVelocity = new Vector3(targetVelocity.x, currentVelocity.y, targetVelocity.z);

        //Extra Gravity 
        body.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);

        //Stick to the ground better
        if (isGrounded && OnSlope(out slopehit))
        {
            // Apply small downward force to stick to slopes
            body.AddForce(Vector3.down * 10f, ForceMode.Acceleration);

            // Prevent slow sliding when standing still on slopes
            if (input.magnitude < 0.05f)
            {
                body.linearVelocity = new Vector3(0f, 0f, 0f);
            }
        }
    }

    void GroundCheck()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, groundCheckDistance))
        {
            isGrounded = true;
            groundNormal = hit.normal;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    void SnapToGround()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, playerHeight / 2f + 0.2f))
        {
            float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);

            // Only snap if it's a valid walabele slope
            if (slopeAngle < maxSlopeAngle)
            {
                // If not moving, stick player to ground
                if (Direction.magnitude < 0.1f)
                {
                    // Kill any residual Y velocity 
                    body.linearVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);

                    // Move the Player down to match the ground
                    body.position = new Vector3(body.position.x, hit.point.y + playerHeight / 2f, body.position.z);
                }
            }
        }
    }

    private void StateHandler()
    {
        //Mode - Sprinting
        if (isGrounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
            moveSpeed = sprintSpeed;
        }

        //Mode - Walking
        else if (isGrounded)
        {
            state = MovementState.walking;
            moveSpeed = walkSpeed;
        }

        //Mode - Air
        else
        {
            state = MovementState.air;
        }

    }

    private bool OnSlope(out RaycastHit slopehit)
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopehit, playerHeight / 2f + 0.5f))
        {
            // If the normal is not perfectly up, it's a slope
            if (slopehit.normal != Vector3.up)
            {
                return true;
            }
        }
        return false;
    }
} */



