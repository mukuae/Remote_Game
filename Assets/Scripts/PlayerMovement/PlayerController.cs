using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using UnityEngine.UIElements;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private float speed = 5f;
    [SerializeField] private float RotationSpeed;
    [SerializeField] private InputActionReference moveAction;


    private Vector2 moveInput;
    private Quaternion targetRotation;
    private bool isMoving;

    private void Awake()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
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
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

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

        Vector3 velocity = moveDirection.normalized * speed;

        if (!characterController.isGrounded)
        {
            velocity.y = Physics.gravity.y;
        }

        characterController.Move(velocity * Time.deltaTime);
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