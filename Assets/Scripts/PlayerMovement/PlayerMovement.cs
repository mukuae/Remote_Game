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
    public int maxJumps = 7;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;

    [Header("Boden Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayers;

    [Header("Effekte & Feedback")]
    public ParticleSystem jumpParticles;
    public ParticleSystem landParticles;
    public AudioClip jumpSound;
    public AudioClip landSound;

    private Rigidbody rb;
    private Vector3 movement;
    private bool isGrounded;
    private bool wasGrounded;
    private int jumpsRemaining;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool isJumping;
    private AudioSource audioSource;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        audioSource = gameObject.AddComponent<AudioSource>();

        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            groundCheck = gc.transform;
        }

        jumpsRemaining = maxJumps;
    }

    void Update()
    {
        // Bewegungs-Input
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        movement = new Vector3(moveX, 0f, moveZ).normalized;

        // Boden Check
        wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);

        // Landungs-Effekt
        if (isGrounded && !wasGrounded)
        {
            OnLand();
        }

        // Sprünge zurücksetzen
        if (isGrounded)
        {
            jumpsRemaining = maxJumps;
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // Jump Buffer
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // Sprung ausführen
        if (jumpBufferCounter > 0f && (coyoteTimeCounter > 0f || jumpsRemaining > 0))
        {
            Jump();
            jumpBufferCounter = 0f;
        }

        // Variable Sprunghöhe
        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f && isJumping)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * variableJumpMultiplier, rb.linearVelocity.z);
            isJumping = false;
        }
    }

    void FixedUpdate()
    {
        // Sprint Check
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed *= sprintMultiplier;
        }

        // Luftkontrolle
        float controlFactor = isGrounded ? 1f : airControl;

        // Horizontale Bewegung
        Vector3 horizontalMove = movement * currentSpeed * controlFactor * Time.fixedDeltaTime;
        Vector3 newPosition = rb.position + horizontalMove;
        newPosition.y = rb.position.y;
        rb.MovePosition(newPosition);
    }

    void Jump()
    {
        if (coyoteTimeCounter <= 0f && jumpsRemaining <= 0) return;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        isJumping = true;
        coyoteTimeCounter = 0f;

        if (!isGrounded)
        {
            //jumpsRemaining--;
        }
        else
        {
            //jumpsRemaining = maxJumps - 1;
        }

        PlayJumpEffects();
    }

    void OnLand()
    {
        isJumping = false;

        if (landParticles != null)
        {
            landParticles.Play();
        }

        if (landSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(landSound);
        }
    }

    void PlayJumpEffects()
    {
        if (jumpParticles != null)
        {
            jumpParticles.Play();
        }

        if (jumpSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpSound);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
