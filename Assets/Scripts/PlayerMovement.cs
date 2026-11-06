using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    public Transform cameraTransform;

    public float walkSpeed = 6f;
    public float jumpForce = 6f;
    public float rollSpeed = 14f;
    public float rollDuration = 0.35f;
    public float mouseSensitivity = 2f;

    private Rigidbody rb;
    private float pitch;
    private Vector3 moveInput;
    private bool jumpPressed;
    private bool rollPressed;
    private float rollTimer;
    private Vector3 rollDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;  // stop the capsule from tipping over
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Look
        transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * mouseSensitivity);
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -85f, 85f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Read input here, use it in FixedUpdate
        moveInput = transform.right * Input.GetAxisRaw("Horizontal")
                  + transform.forward * Input.GetAxisRaw("Vertical");
        moveInput = Vector3.ClampMagnitude(moveInput, 1f);

        if (Input.GetKeyDown(KeyCode.Space)) jumpPressed = true;
        if (Input.GetKeyDown(KeyCode.LeftControl)) rollPressed = true;
    }

    void FixedUpdate()
    {
        bool grounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);
        Vector3 velocity = rb.linearVelocity;

        // Start a roll in the move direction (or forward if standing still)
        if (rollPressed && grounded && rollTimer <= 0f)
        {
            rollDirection = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : transform.forward;
            rollTimer = rollDuration;
        }

        if (rollTimer > 0f)
        {
            rollTimer -= Time.fixedDeltaTime;
            velocity.x = rollDirection.x * rollSpeed;
            velocity.z = rollDirection.z * rollSpeed;
        }
        else
        {
            velocity.x = moveInput.x * walkSpeed;
            velocity.z = moveInput.z * walkSpeed;

            if (jumpPressed && grounded)
                velocity.y = jumpForce;
        }

        rb.linearVelocity = velocity;
        jumpPressed = false;
        rollPressed = false;
    }
}