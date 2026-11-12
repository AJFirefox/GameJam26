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

    [Header("Falling safety net")]
    [Tooltip("If you drop this far below the last floor you stood on, you're put back on it.")]
    public float fallResetDistance = 12f;
    [Tooltip("Paper-thin floors (mesh planes) can let a standing player sink through. " +
             "This adds a solid invisible slab right under the floor you start on.")]
    public bool addSolidSlabUnderThinFloor = true;

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

    // Last safe floor, for the safety net
    private Vector3 lastSafePosition;
    private Collider lastFloor;
    private bool hasSafePosition;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();

        // Physics settings that stop a fast or resting player slipping through floors
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.sleepThreshold = 0f;   // never "sleep" while standing still

        SnapToFloor();
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

    /// <summary>If the player starts inside or above the floor, put their feet on top of it.</summary>
    private void SnapToFloor()
    {
        Collider body = GetComponent<Collider>();
        float feetOffset = body != null ? transform.position.y - body.bounds.min.y : 1f;

        // Highest floor below the start point (not counting roofs/arches more than 2 m above our head)
        Vector3 from = transform.position + Vector3.up * 2f;
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        RaycastHit floor = default;
        bool found = false;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.root == transform) continue;
            if (h.distance < best) { best = h.distance; floor = h; found = true; }
        }
        if (!found)
        {
            Debug.LogWarning($"PlayerMovement: no floor collider under the player's start ({transform.position}). " +
                             "Check the arena has colliders (model import > Generate Colliders, or a Mesh/Box Collider).");
            return;
        }

        if (addSolidSlabUnderThinFloor && floor.collider is MeshCollider) AddFloorSlab(floor.collider, floor.point.y);

        transform.position = new Vector3(transform.position.x, floor.point.y + feetOffset + 0.05f, transform.position.z);
        rb.position = transform.position;
        lastSafePosition = transform.position;
        lastFloor = floor.collider;
        hasSafePosition = true;
    }

    /// <summary>A solid 2 m thick box whose top sits exactly at the floor's surface, covering the whole floor.</summary>
    private static void AddFloorSlab(Collider thinFloor, float surfaceY)
    {
        const float thickness = 2f;
        Bounds b = thinFloor.bounds;
        GameObject slab = new GameObject($"Solid floor under {thinFloor.name} (auto)");
        slab.transform.position = new Vector3(b.center.x, surfaceY - thickness * 0.5f, b.center.z);
        BoxCollider box = slab.AddComponent<BoxCollider>();
        box.size = new Vector3(b.size.x, thickness, b.size.z);
        slab.isStatic = true;
        Debug.Log($"PlayerMovement: added a solid floor under '{thinFloor.name}' ({b.size.x:0} x {b.size.z:0} m) so the player can't sink through it.");
    }

    void FixedUpdate()
    {
        bool grounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, 1.1f, ~0, QueryTriggerInteraction.Ignore)
                        && groundHit.collider.transform.root != transform;

        // Safety net: remember solid ground, and catch the player if they drop through the world
        if (grounded && dashTimer <= 0f)
        {
            lastSafePosition = transform.position;
            lastFloor = groundHit.collider;
            hasSafePosition = true;
        }
        else if (hasSafePosition && transform.position.y < lastSafePosition.y - fallResetDistance)
        {
            Debug.LogWarning($"PlayerMovement: fell through the floor near {lastSafePosition} " +
                             $"(was standing on '{(lastFloor != null ? lastFloor.name : "?")}'). Put back on the floor.");
            rb.linearVelocity = Vector3.zero;
            rb.position = lastSafePosition + Vector3.up * 0.5f;
            transform.position = rb.position;
            return;
        }
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
