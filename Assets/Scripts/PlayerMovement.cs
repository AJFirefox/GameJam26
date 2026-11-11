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

    [Tooltip("Can't be hurt while rolling.")]
    public bool rollGivesInvulnerability = true;

    [Tooltip("1 = normal. Ares' Bloodlust raises this.")]
    public float speedMultiplier = 1f;

    /// <summary>True during a dodge roll.</summary>
    public bool IsRolling => dashTimer > 0f && dashIsRoll;
    /// <summary>True during a weapon lunge (Fortune's Edge hit 3). Hoplite shields check this later.</summary>
    public bool IsLunging => dashTimer > 0f && !dashIsRoll;

    private Rigidbody rb;
    private Health health;
    private PlayerStamina stamina;
    private float pitch;
    private Vector3 moveInput;
    private bool jumpPressed;
    private bool rollPressed;

    // Rolls and lunges share one "dash" system
    private float dashTimer;
    private float dashSpeed;
    private bool dashIsRoll;
    private Vector3 dashDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        stamina = GetComponent<PlayerStamina>();   // optional: without it, rolling is free
        rb.freezeRotation = true;  // stop the capsule from tipping over
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;   // paused (altar menu, game over)

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

    /// <summary>
    /// Pushes the player in a direction for a short time. Used by weapon lunges.
    /// </summary>
    public void Dash(Vector3 direction, float speed, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
        dashDirection = direction.normalized;
        dashSpeed = speed;
        dashTimer = duration;
        dashIsRoll = false;
    }

    void FixedUpdate()
    {
        bool grounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);
        Vector3 velocity = rb.linearVelocity;

        // Start a roll in the move direction (or forward if standing still). Costs stamina.
        if (rollPressed && grounded && dashTimer <= 0f && (stamina == null || stamina.TrySpend(stamina.rollCost)))
        {
            dashDirection = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : transform.forward;
            dashSpeed = rollSpeed;
            dashTimer = rollDuration;
            dashIsRoll = true;
        }

        if (health != null && rollGivesInvulnerability) health.IsInvulnerable = IsRolling;

        if (dashTimer > 0f)
        {
            dashTimer -= Time.fixedDeltaTime;
            velocity.x = dashDirection.x * dashSpeed;
            velocity.z = dashDirection.z * dashSpeed;
        }
        else
        {
            velocity.x = moveInput.x * walkSpeed * speedMultiplier;
            velocity.z = moveInput.z * walkSpeed * speedMultiplier;

            if (jumpPressed && grounded)
                velocity.y = jumpForce;
        }

        rb.linearVelocity = velocity;
        jumpPressed = false;
        rollPressed = false;
    }
}
