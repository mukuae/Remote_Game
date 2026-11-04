using UnityEngine;
using UnityEngine.InputSystem;

// Early, very simple Rigidbody movement (used in the old MainLevel1 scene).
// For new levels use PlayerController instead.
public class SimplePlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector3 moveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            groundCheck = gc.transform;
        }
    }

    private void Update()
    {
        if (PauseMenu.GameIsPaused)
        {
            moveDirection = Vector3.zero;
            return;
        }

        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        Vector2 input = ReadMoveInput();
        moveDirection = transform.right * input.x + transform.forward * input.y;

        // Only jump when grounded AND not already moving upward.
        if (JumpPressedThisFrame() && isGrounded && rb.linearVelocity.y <= 0.1f)
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void FixedUpdate()
    {
        Vector3 velocity = moveDirection.normalized * moveSpeed;
        velocity.y = rb.linearVelocity.y; // keep vertical velocity (gravity)
        rb.linearVelocity = velocity;
    }

    private static Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        }

        if (Gamepad.current != null)
            input += Gamepad.current.leftStick.ReadValue();

        return Vector2.ClampMagnitude(input, 1f);
    }

    private static bool JumpPressedThisFrame()
    {
        bool keyboardJump = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool gamepadJump = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        return keyboardJump || gamepadJump;
    }
}
