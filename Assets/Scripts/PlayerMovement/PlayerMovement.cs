using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpielerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Debug Position Check")]
    public Vector3 targetPosition = new Vector3(10f, 0f, 10f);
    public float positionTolerance = 0.5f;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector3 moveDirection;

    void Awake()
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

    void Update()
    {
        // Ground Check
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        }

        // Check ob Position mit Ziel-Koordinaten übereinstimmt
        float distance = Vector3.Distance(transform.position, targetPosition);
        if (distance <= positionTolerance)
        {
            Debug.Log("*** MATCH! Ziel-Position erreicht! ***");
        }

        // Bewegung Input sammeln
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        moveDirection = transform.right * horizontal + transform.forward * vertical;

        // Springen: NUR wenn auf Boden UND Cube fällt nicht
        if (Input.GetButtonDown("Jump") && isGrounded && rb.linearVelocity.y <= 0.1f)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void FixedUpdate() // BEWEGUNG IN FIXEDUPDATE!
    {
        // Bewegung mit velocity statt MovePosition
        Vector3 velocity = moveDirection.normalized * moveSpeed;
        velocity.y = rb.linearVelocity.y; // Y-Velocity beibehalten (Gravität!)
        rb.linearVelocity = velocity;
    }
}
