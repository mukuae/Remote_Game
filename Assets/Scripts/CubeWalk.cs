using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CubeWalk : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Horizontal movement speed in units per second.")]
    public float moveSpeed = 6f;

    [Tooltip("Upward force applied when jumping.")]
    public float jumpForce = 7f;

    [Tooltip("Extra downward force to make jumps feel snappier.")]
    public float extraGravity = 10f;

    [Header("Ground Check")]
    [Tooltip("Transform used to check if the cube is grounded (e.g., placed at the bottom of the cube).")]
    public Transform groundCheck;

    [Tooltip("Radius used for the ground check sphere.")]
    public float groundCheckRadius = 0.2f;

    [Tooltip("Which layers count as ground.")]
    public LayerMask groundLayer;

    private Rigidbody rb;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Helps keep the cube upright and movement stable
        rb.freezeRotation = true;

        // If no groundCheck assigned, create one at runtime at the bottom of the cube
        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            // Place it slightly below the cube assuming a 1x1x1 cube
            gc.transform.localPosition = new Vector3(0f, -0.51f, 0f);
            groundCheck = gc.transform;
        }
    }

    void Update()
    {
        // Horizontal input: A/D or Left/Right arrows
        float inputX = Input.GetAxisRaw("Horizontal"); // -1, 0, 1
        Vector3 velocity = rb.linearVelocity;

        // Set X velocity directly for responsive movement; keep Y as is
        velocity.x = inputX * moveSpeed;
        rb.linearVelocity = velocity;

        // Ground check
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            // Reset Y velocity so jumps are consistent, then add jump force
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }

        // Apply extra gravity for a snappier fall (only when not grounded)
        if (!isGrounded)
        {
            rb.AddForce(Physics.gravity * extraGravity * Time.deltaTime, ForceMode.Acceleration);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Visualize ground check sphere in the editor
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}

