using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Bewegung")]
    public float moveSpeed = 7f;
    public float sprintMultiplier = 1.5f;
    public float airControl = 0.5f;

    [Header("Sprung")]
    public float jumpForce = 8f;
    public float variableJumpMultiplier = 0.5f;
    public int maxJumps = 2;
    public float jumpCooldown = 0.8f; // Cooldown in Sekunden
    private int jumpsRemaining;
    private float lastJumpTime = -999f; // Zeitpunkt des letzten Sprungs

    [Header("Debug")]
    public bool showDebugInfo = true;

    private Rigidbody rb;
    private Vector3 movement;
    private bool isGrounded;
    private int groundContactCount = 0; // Zählt Bodenkontakte

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        jumpsRemaining = maxJumps;
    }

    void Update()
    {
        // Bewegungs-Input
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        movement = new Vector3(moveX, 0f, moveZ).normalized;

        // Boden Status basierend auf Kollisionen
        isGrounded = groundContactCount > 0;

        // Sprünge zurücksetzen wenn am Boden
        if (isGrounded)
        {
            jumpsRemaining = maxJumps;
        }

        // Debug
        if (showDebugInfo && Input.GetButtonDown("Jump"))
        {
            float timeSinceLastJump = Time.time - lastJumpTime;
            Debug.Log($"Jump! isGrounded: {isGrounded}, Contacts: {groundContactCount}, Jumps: {jumpsRemaining}, Cooldown: {timeSinceLastJump:F2}s");
        }

        // Sprung mit Cooldown-Prüfung
        if (Input.GetButtonDown("Jump") && jumpsRemaining > 0 && CanJump())
        {
            Jump();
        }

        // Variable Sprunghöhe
        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * variableJumpMultiplier, rb.linearVelocity.z);
        }
    }

    void FixedUpdate()
    {
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed *= sprintMultiplier;
        }

        float controlFactor = isGrounded ? 1f : airControl;
        Vector3 horizontalMove = movement * currentSpeed * controlFactor * Time.fixedDeltaTime;
        Vector3 newPosition = rb.position + horizontalMove;
        newPosition.y = rb.position.y;
        rb.MovePosition(newPosition);
    }

    bool CanJump()
    {
        // Prüfe ob genug Zeit seit dem letzten Sprung vergangen ist
        return (Time.time - lastJumpTime) >= jumpCooldown;
    }

    void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        
        jumpsRemaining--;
        lastJumpTime = Time.time; // Speichere Zeitpunkt des Sprungs
        
        if (showDebugInfo)
        {
            Debug.Log($"GESPRUNGEN! Verbleibend: {jumpsRemaining}, Nächster Sprung in: {jumpCooldown}s");
        }
    }

    // Kollisions-Erkennung
    void OnCollisionEnter(Collision collision)
    {
        // Prüfe ob Kollision von unten kommt
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f) // Boden-Kollision
            {
                groundContactCount++;
                if (showDebugInfo)
                {
                    Debug.Log($"Boden berührt! Contacts: {groundContactCount}");
                }
                break;
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        // Stelle sicher dass wir am Boden bleiben
        bool hasGroundContact = false;
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                hasGroundContact = true;
                break;
            }
        }
        
        if (!hasGroundContact && groundContactCount > 0)
        {
            groundContactCount--;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        // Prüfe ob wir den Boden verlassen
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                groundContactCount--;
                if (showDebugInfo)
                {
                    Debug.Log($"Boden verlassen! Contacts: {groundContactCount}");
                }
                break;
            }
        }
        
        // Sicherheit: Nie unter 0
        if (groundContactCount < 0) groundContactCount = 0;
    }
}
