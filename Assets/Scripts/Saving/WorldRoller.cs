using UnityEngine;

public class WorldRoller : MonoBehaviour
{
    public ExamplePlayerController player;
    public Transform playerVisual;

    public float rotationSpeed = 40f;
    public float maxRotation = 25f;

    private float currentRotation = 0f;
    private float targetRotation = 0f;

    void Start()
    {
        if (player == null)
        {
            var obj = GameObject.FindWithTag("Player");
            if (obj != null)
                player = obj.GetComponent<ExamplePlayerController>();
        }

        if (playerVisual == null && player != null)
        {
            playerVisual = player.transform.Find("PlayerVisual");
        }
    }

    void Update()
    {
        if (player == null) return;

        float vertical = player.MovementInput.y;

        if (vertical > 0.1f)
            targetRotation = maxRotation;
        else if (vertical < -0.1f)
            targetRotation = -maxRotation;
        else
            targetRotation = 0f;

        currentRotation = Mathf.MoveTowards(
            currentRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );

        // Rotate the world
        transform.localRotation = Quaternion.Euler(currentRotation, 0f, 0f);

        // Counter-rotate the player's visual sprite
        if (playerVisual != null)
            playerVisual.localRotation = Quaternion.Euler(-currentRotation, 0f, 0f);
    }
}
