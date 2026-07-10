using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerController : MonoBehaviour
{
[SerializeField] private Transform cameraTransform;
[SerializeField] private CharacterController characterController;
[SerializeField] private Animator animator;
[SerializeField] private float speed = 5f;
[SerializeField] private float RotationSpeed = 10f;
[SerializeField] private float jumpHeight = 2f;
[SerializeField] private InputActionReference moveAction;
[SerializeField] private InputActionReference jumpAction;

          
public float gravityMultiplier = 3f;

private Vector2 moveInput;
private Quaternion targetRotation;
private bool isMoving;

private Vector3 velocity;
private Vector3 externalVelocity;

private void Awake()
{
    if (characterController == null)
        characterController = GetComponent<CharacterController>();

    if (cameraTransform == null)
        cameraTransform = Camera.main.transform;
}

private void OnEnable()
{
    moveAction.action.Enable();

    if (jumpAction != null)
        jumpAction.action.Enable();
}

private void OnDisable()
{
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

    if (characterController.isGrounded && velocity.y < 0)
    {
        velocity.y = -2f;
    }

    if (characterController.isGrounded &&
        jumpAction != null &&
        jumpAction.action.WasPressedThisFrame())
    {
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
    }

    velocity.x = moveDirection.x * speed;
//    velocity.z = moveDirection.z * speed;

        velocity += externalVelocity;

    externalVelocity = Vector3.Lerp(
        externalVelocity,
        Vector3.zero,
        8f * Time.deltaTime
    );

    velocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;

    characterController.Move(velocity * Time.deltaTime);
}

public void PogoJump(Vector3 direction, float force)
{
    direction.Normalize();

    externalVelocity = direction * force;
    velocity.y = force; // Direkt velocity.y setzen für sofortigen Sprung-Impuls
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