using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerControllerPhysics : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Animator animator;
    [SerializeField] private CapsuleCollider capsuleCollider;

    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float acceleration = 15f;
    [SerializeField] private float deceleration = 20f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Jumping")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float jumpBufferTime = 0.2f;
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("Ground Check")]
    [Tooltip("Set this to whatever layer(s) your ground and platforms are on.")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Header("Moving Platform")]
    [SerializeField] private string platformTag = "Platform";

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    public float gravityMultiplier = 3f;
    public float externalVelocityFadeSpeed = 8f;

    private Vector2 moveInput;
    private Quaternion targetRotation;
    private bool isMoving;
    private bool isGrounded;
    private bool jumpQueued;
    private Vector3 currentMoveVelocity;
    private Vector3 externalVelocity;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private Transform currentPlatform;
    private Vector3 lastPlatformPosition;
    private Quaternion lastPlatformRotation;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (capsuleCollider == null)
            capsuleCollider = GetComponent<CapsuleCollider>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // We rotate the player manually (Rotate()) - physics shouldn't be allowed to tip it over.
        rb.freezeRotation = true;

        if (cameraTransform == null)
            Debug.LogError($"{nameof(PlayerControllerPhysics)} on '{name}' has no camera assigned and no MainCamera was found in the scene.", this);
    }

    private void OnEnable()
    {
        if (moveAction != null)
            moveAction.action.Enable();
        if (jumpAction != null)
            jumpAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null)
            moveAction.action.Disable();
        if (jumpAction != null)
            jumpAction.action.Disable();
    }

    private void Update()
    {
        HandleInput();
        Animate();
    }

    private void HandleInput()
    {
        Vector2 rawMoveInput = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        moveInput = Vector2.ClampMagnitude(rawMoveInput, 1f);
        isMoving = moveInput.magnitude > 0.1f;

        if (jumpAction != null && jumpAction.action.WasPressedThisFrame())
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    // Physics work belongs in FixedUpdate, not Update, so it stays in sync with the physics engine.
    private void FixedUpdate()
    {
        CheckGround();
        Move();
        Rotate();
    }

    private void CheckGround()
    {
        float radius = capsuleCollider != null ? capsuleCollider.radius * 0.9f : groundCheckRadius;
        Vector3 origin = transform.position + Vector3.up * radius;

        bool hitGround = Physics.SphereCast(
            origin, groundCheckRadius, Vector3.down,
            out RaycastHit hit, groundCheckDistance + radius, groundMask, QueryTriggerInteraction.Ignore);

        isGrounded = hitGround;

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;

            if (hit.collider.CompareTag(platformTag))
            {
                if (currentPlatform != hit.collider.transform)
                {
                    currentPlatform = hit.collider.transform;
                    lastPlatformPosition = currentPlatform.position;
                    lastPlatformRotation = currentPlatform.rotation;
                }
            }
            else
            {
                currentPlatform = null;
            }
        }
        else
        {
            coyoteTimeCounter -= Time.fixedDeltaTime;
            currentPlatform = null;
        }
    }

    private void Move()
    {
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = isMoving
            ? cameraForward * moveInput.y + cameraRight * moveInput.x
            : Vector3.zero;

        if (moveDirection.magnitude > 0.1f)
        {
            targetRotation = Quaternion.LookRotation(moveDirection);
        }

        // --- Jump (buffered input + coyote time) ---
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            jumpQueued = true;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            currentPlatform = null;
        }

        // --- Horizontal acceleration / deceleration ---
        Vector3 targetMoveVelocity = moveDirection * speed;
        float currentAcceleration = isMoving ? acceleration : deceleration;
        currentMoveVelocity = Vector3.MoveTowards(
            currentMoveVelocity, targetMoveVelocity, currentAcceleration * Time.fixedDeltaTime);

        // NOTE: Unity 6 renamed Rigidbody.velocity to Rigidbody.linearVelocity.
        // If you're on Unity 6+/2023.3+, replace "rb.velocity" below with "rb.linearVelocity".
        Vector3 velocity = rb.linearVelocity;
        velocity.x = currentMoveVelocity.x;
        velocity.z = currentMoveVelocity.z;

        if (jumpQueued)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y * gravityMultiplier);
            jumpQueued = false;
        }
        else if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        // Extra gravity on top of Unity's built-in gravity (Rigidbody already applies 1x by default).
        velocity.y += Physics.gravity.y * (gravityMultiplier - 1f) * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);

        rb.linearVelocity = velocity + externalVelocity;

        // --- Moving platform contribution ---
        // Applied as a direct position nudge (position + rotation delta), so it works even
        // though the player is a dynamic Rigidbody being driven by velocity above.
        if (currentPlatform != null)
        {
            Vector3 deltaPosition = currentPlatform.position - lastPlatformPosition;
            Quaternion deltaRotation = currentPlatform.rotation * Quaternion.Inverse(lastPlatformRotation);
            Vector3 offsetFromPlatform = rb.position - lastPlatformPosition;
            Vector3 rotatedOffset = deltaRotation * offsetFromPlatform;

            Vector3 platformMovement = deltaPosition + (rotatedOffset - offsetFromPlatform);
            rb.MovePosition(rb.position + platformMovement);

            lastPlatformPosition = currentPlatform.position;
            lastPlatformRotation = currentPlatform.rotation;
        }

        externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, externalVelocityFadeSpeed * Time.fixedDeltaTime);
    }

    public void PogoJump(Vector3 direction, float force)
    {
        direction.Normalize();
        externalVelocity = direction * force;

        Vector3 v = rb.linearVelocity;
        v.y = force;
        rb.linearVelocity = v;

        jumpBufferCounter = 0f;
    }

    private void Animate()
    {
        if (animator == null) return;
        animator.SetBool("IsMoving", isMoving);
    }

    private void Rotate()
    {
        if (isMoving)
        {
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }
    }
}