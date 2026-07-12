using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;

    [SerializeField] private float speed = 5f;
    [SerializeField] private float acceleration = 15f;
    [SerializeField] private float deceleration = 20f;
    [SerializeField] private float RotationSpeed = 10f;

    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float jumpBufferTime = 0.2f;

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

        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
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
        moveInput = moveAction.action.ReadValue<Vector2>().normalized;
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

        cameraForward.y = 0;
        cameraRight.y = 0;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;

        if (moveDirection.magnitude > 0.1f)
        {
            targetRotation = Quaternion.LookRotation(moveDirection);
        }

        if (characterController.isGrounded)
        {
            coyoteTimeCounter = coyoteTime;

            if (velocity.y < 0)
            {
                velocity.y = -2f;
            }
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        Vector3 targetMoveVelocity = moveDirection * speed;

        float currentAcceleration = isMoving ? acceleration : deceleration;

        currentMoveVelocity = Vector3.MoveTowards(
            currentMoveVelocity,
            targetMoveVelocity,
            currentAcceleration * Time.deltaTime
        );

        velocity.x = currentMoveVelocity.x;
        velocity.z = currentMoveVelocity.z;

        velocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;

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
    }

    private void Animate()
    {
        animator.SetBool("IsMoving", isMoving);
    }

    private void Rotate()
    {
        if (isMoving)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                RotationSpeed * Time.deltaTime
            );
        }
    }
}