using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Bewegung")]
    public float moveSpeed = 7f;
    public float sprintMultiplier = 1.5f;
    public float airControl = 0.5f;
    public float acceleration = 20f;
    public float airAcceleration = 8f;

    [Header("Sprung")]
    public float jumpForce = 8f;
    public float variableJumpMultiplier = 0.6f;
    public int maxJumps = 2;
    public float jumpCooldown = 0.2f;

    bool isGroundedad;
    

    [Header("Boden-Check")]
    public Transform groundCheck;
    public float groundRadius = 0.2f;
    public LayerMask groundMask = ~0;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private Rigidbody rb;
    private Vector3 movement;
    private int jumpsRemaining;
    private float lastJumpTime = -999f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        jumpsRemaining = maxJumps;
        if (groundCheck == null)
        {
            // Optional: automatisch unter dem Objekt platzieren
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.localPosition = new Vector3(0f, -0.51f, 0f);
            groundCheck = gc.transform;
        }
    }

    void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        movement = new Vector3(moveX, 0f, moveZ).normalized;



        if (Input.GetButtonDown("Jump") && jumpsRemaining > 0 && isGroundedad == true)
        {
            Jump();
        }

        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * variableJumpMultiplier, rb.linearVelocity.z);
        }
    }

    void FixedUpdate()
    {
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * sprintMultiplier : moveSpeed;

        
      

        Vector3 vel = rb.linearVelocity;
        Vector3 horiz = new Vector3(vel.x, 0f, vel.z);

       
      

     

        //isGroundedad = Physics.OverlapSphere(groundCheck.position,groundRadius,groundMask);
        isGroundedad = Physics.CheckSphere(groundCheck.position, groundRadius, groundMask);
        Debug.Log("isGrounded= "+isGroundedad.ToString());
    }

    void Jump()
    {
        // Vertikale Komponente nullen, dann Impuls
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        jumpsRemaining--;
        lastJumpTime = Time.time;

        if (showDebugInfo)
        {
            Debug.Log($"Sprung! Verbleibend: {jumpsRemaining}");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
    }
}
