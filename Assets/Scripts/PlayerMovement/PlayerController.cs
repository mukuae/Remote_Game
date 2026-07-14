using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;

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

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    public float gravityMultiplier = 3f;
    public float externalVelocityFadeSpeed = 8f;

    private Vector2 moveInput;
    private Quaternion targetRotation;
    private bool isMoving;
    private Vector3 velocity;
    private Vector3 currentMoveVelocity;
    private Vector3 externalVelocity;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (characterController == null)
            Debug.LogError($"{nameof(PlayerController)} on '{name}' is missing a CharacterController reference.", this);

        if (cameraTransform == null)
            Debug.LogError($"{nameof(PlayerController)} on '{name}' has no camera assigned and no MainCamera was found in the scene.", this);
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
        Move();
        Animate();
        Rotate();
    }

    private void HandleInput()
    {
        Vector2 rawMoveInput = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

        // ClampMagnitude (instead of .normalized) preserves partial gamepad stick input for analog
        // speed control, while still capping any input (including keyboard diagonals) at magnitude 1.
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

    private void Move()
    {
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        // Zero out when there's no input so deceleration eases all the way to zero velocity,
        // instead of easing toward a tiny leftover vector from the input deadzone.
        Vector3 moveDirection = isMoving
            ? cameraForward * moveInput.y + cameraRight * moveInput.x
            : Vector3.zero;

        if (moveDirection.magnitude > 0.1f)
        {
            targetRotation = Quaternion.LookRotation(moveDirection);
        }

        // --- Ground check + coyote time ---
        if (characterController.isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
            if (velocity.y < 0f)
            {
                velocity.y = -2f;
            }
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // --- Jump (buffered input + coyote time) ---
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            // Multiplying by gravityMultiplier here matches the gravity actually applied below,
            // so the character's real apex height matches jumpHeight.
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y * gravityMultiplier);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        // --- Horizontal acceleration / deceleration ---
        Vector3 targetMoveVelocity = moveDirection * speed;
        float currentAcceleration = isMoving ? acceleration : deceleration;
        currentMoveVelocity = Vector3.MoveTowards(
            currentMoveVelocity,
            targetMoveVelocity,
            currentAcceleration * Time.deltaTime
        );
        velocity.x = currentMoveVelocity.x;
        velocity.z = currentMoveVelocity.z;

        // --- Gravity ---
        velocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);

        Vector3 finalVelocity = velocity + externalVelocity;
        characterController.Move(finalVelocity * Time.deltaTime);

        externalVelocity = Vector3.Lerp(
            externalVelocity,
            Vector3.zero,
            externalVelocityFadeSpeed * Time.deltaTime
        );
    }

    public void PogoJump(Vector3 direction, float force)
    {
        direction.Normalize();
        externalVelocity = direction * force;
        velocity.y = force;

        // Clear any buffered jump so it can't immediately overwrite this velocity.y next frame.
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
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }
}