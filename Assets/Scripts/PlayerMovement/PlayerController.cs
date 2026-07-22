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
    [SerializeField] private bool twoDimensionalMovement = false;

    [Header("Jumping")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float jumpBufferTime = 0.2f;
    [SerializeField] private float maxFallSpeed = 20f;
    [SerializeField] private float gravityMultiplier = 3f;

    [Header("Moving Platforms")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float platformCheckDistance = 0.4f;
    [SerializeField] private float platformContactGraceTime = 0.1f;
    [SerializeField] private bool inheritPlatformVelocityOnJump = false;

    [Header("External Movement")]
    [SerializeField] private float externalVelocityFadeSpeed = 8f;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    private Vector2 moveInput;
    private Quaternion targetRotation;
    private bool isMoving;

    private Vector3 velocity;
    private Vector3 currentMoveVelocity;
    private Vector3 externalVelocity;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private Transform currentPlatform;
    private Vector3 previousPlatformPosition;
    private Quaternion previousPlatformRotation;
    private Vector3 platformVelocity;
    private float platformContactTimer;
    private float lockedZPosition;

    private void Awake()
    {
        lockedZPosition = transform.position.z;

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (characterController == null)
        {
            Debug.LogError(
                $"{nameof(PlayerController)} on '{name}' is missing a CharacterController.",
                this
            );

            enabled = false;
            return;
        }

        if (cameraTransform == null)
        {
            Debug.LogError(
                $"{nameof(PlayerController)} on '{name}' has no camera assigned.",
                this
            );

            enabled = false;
        }
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
        HandleMovingPlatform();
        Move();
        LockTo2DPlane();
        Animate();
        Rotate();
    }

    private void HandleInput()
    {
        Vector2 rawMoveInput = moveAction != null
            ? moveAction.action.ReadValue<Vector2>()
            : Vector2.zero;

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

    private void HandleMovingPlatform()
    {
        Transform detectedPlatform = DetectPlatform();

        if (detectedPlatform != null)
        {
            platformContactTimer = platformContactGraceTime;

            if (detectedPlatform != currentPlatform)
            {
                currentPlatform = detectedPlatform;
                previousPlatformPosition = currentPlatform.position;
                previousPlatformRotation = currentPlatform.rotation;
                platformVelocity = Vector3.zero;
                return;
            }
        }
        else
        {
            platformContactTimer -= Time.deltaTime;

            if (platformContactTimer <= 0f)
            {
                currentPlatform = null;
                platformVelocity = Vector3.zero;
                return;
            }
        }

        if (currentPlatform == null)
            return;

        Vector3 currentPlatformPosition = currentPlatform.position;
        Quaternion currentPlatformRotation = currentPlatform.rotation;

        Vector3 positionDelta =
            currentPlatformPosition - previousPlatformPosition;

        Quaternion rotationDelta =
            currentPlatformRotation *
            Quaternion.Inverse(previousPlatformRotation);

        Vector3 playerOffset =
            transform.position - previousPlatformPosition;

        Vector3 rotatedOffset =
            rotationDelta * playerOffset;

        Vector3 rotationMovement =
            rotatedOffset - playerOffset;

        Vector3 totalPlatformMovement =
            positionDelta + rotationMovement;

        if (Time.deltaTime > 0f)
        {
            platformVelocity =
                totalPlatformMovement / Time.deltaTime;
        }

        characterController.Move(totalPlatformMovement);

        previousPlatformPosition = currentPlatformPosition;
        previousPlatformRotation = currentPlatformRotation;
    }

    private Transform DetectPlatform()
    {
        Vector3 controllerCenter =
            transform.position + characterController.center;

        float bottomDistance =
            characterController.height * 0.5f -
            characterController.radius;

        Vector3 sphereOrigin =
            controllerCenter -
            Vector3.up * bottomDistance +
            Vector3.up * 0.08f;

        float sphereRadius =
            characterController.radius * 0.85f;

        bool hitGround = Physics.SphereCast(
            sphereOrigin,
            sphereRadius,
            Vector3.down,
            out RaycastHit hit,
            platformCheckDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        if (!hitGround)
            return null;

        Rigidbody platformRigidbody =
            hit.collider.attachedRigidbody;

        if (platformRigidbody != null)
            return platformRigidbody.transform;

        if (hit.collider.CompareTag("MovingPlatform"))
            return hit.collider.transform;

        if (hit.collider.transform.root.CompareTag("MovingPlatform"))
            return hit.collider.transform.root;

        return null;
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
            ? cameraForward * moveInput.y +
              cameraRight * moveInput.x
            : Vector3.zero;

        moveDirection =
            Vector3.ClampMagnitude(moveDirection, 1f);

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            targetRotation =
                Quaternion.LookRotation(moveDirection);
        }

        if (characterController.isGrounded)
        {
            coyoteTimeCounter = coyoteTime;

            if (velocity.y < 0f)
                velocity.y = -2f;

            externalVelocity.x = Mathf.MoveTowards(
                externalVelocity.x,
                0f,
                externalVelocityFadeSpeed * 3f * Time.deltaTime
            );

            externalVelocity.z = Mathf.MoveTowards(
                externalVelocity.z,
                0f,
                externalVelocityFadeSpeed * 3f * Time.deltaTime
            );
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (
            jumpBufferCounter > 0f &&
            coyoteTimeCounter > 0f
        )
        {
            velocity.y = Mathf.Sqrt(
                jumpHeight *
                -2f *
                Physics.gravity.y *
                gravityMultiplier
            );

            if (
                inheritPlatformVelocityOnJump &&
                currentPlatform != null
            )
            {
                externalVelocity += platformVelocity;
            }

            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            currentPlatform = null;
            platformVelocity = Vector3.zero;
            platformContactTimer = 0f;
        }

        Vector3 targetMoveVelocity =
            moveDirection * speed;

        float usedAcceleration =
            isMoving ? acceleration : deceleration;

        currentMoveVelocity = Vector3.MoveTowards(
            currentMoveVelocity,
            targetMoveVelocity,
            usedAcceleration * Time.deltaTime
        );

        velocity.x = currentMoveVelocity.x;
        velocity.z = currentMoveVelocity.z;

        velocity.y +=
            Physics.gravity.y *
            gravityMultiplier *
            Time.deltaTime;

        velocity.y =
            Mathf.Max(velocity.y, -maxFallSpeed);

        Vector3 finalVelocity =
            velocity + externalVelocity;

        characterController.Move(
            finalVelocity * Time.deltaTime
        );

        externalVelocity = Vector3.MoveTowards(
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

        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
        currentPlatform = null;
        platformVelocity = Vector3.zero;
        platformContactTimer = 0f;
    }


    private void LockTo2DPlane()
    {
        if (!twoDimensionalMovement)
        {
            lockedZPosition = transform.position.z;
            return;
        }

        currentMoveVelocity.z = 0f;
        velocity.z = 0f;
        externalVelocity.z = 0f;
        platformVelocity.z = 0f;

        float zCorrection = lockedZPosition - transform.position.z;

        if (Mathf.Abs(zCorrection) > 0.0001f)
            characterController.Move(new Vector3(0f, 0f, zCorrection));
    }

    private void Animate()
    {
        if (animator == null)
            return;

        animator.SetBool("IsMoving", isMoving);
    }

    private void Rotate()
    {
        if (!isMoving)
            return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}