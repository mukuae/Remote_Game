using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CubeWalk : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float turnSpeed = 720f;

    [Header("Jumping & Gravity")]
    public float jumpImpulse = 5f;
    public float extraDownForce = 5f;

    [Header("Ground Detection")]
    [Tooltip("Assign layers considered as 'ground' (e.g., your Plane layer).")]
    public LayerMask groundLayer;

    [Tooltip("Radius for the ground check cast (match half of the cube�s width).")]
    public float groundCheckRadius = 0.45f;

    [Tooltip("Vertical offset from the object center to the bottom (half the height).")]
    public float bottomOffset = 0.5f;

    [Tooltip("Extra distance below the bottom to look for ground.")]
    public float groundCheckDistance = 0.1f;

    [Range(0f, 89f)]
    public float maxGroundSlope = 60f;

    private Rigidbody rb;
    private Collider col;
    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;
    private Vector3 desiredVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        rb.constraints = RigidbodyConstraints.FreezeRotation; // keep upright
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Update()
    {
        // Input
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(inputX, 0f, inputZ).normalized;

        desiredVelocity = input * moveSpeed;

        // Jump
        if (isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            Vector3 v = rb.linearVelocity;
            if (v.y < 0f) v.y = 0f;
            v.y += jumpImpulse;
            rb.linearVelocity = v;
        }

        // Rotate to face movement
        RotateTowardsMovement(desiredVelocity);
    }

    private void FixedUpdate()
    {
        DoGroundCheck();

        // Project desired movement along the ground
        Vector3 moveOnGround = ProjectOnGround(desiredVelocity, groundNormal);

        // Apply velocity (keep vertical)
        Vector3 v = rb.linearVelocity;
        v.x = moveOnGround.x;
        v.z = moveOnGround.z;
        rb.linearVelocity = v;

        // Stick to ground when moving
        if (isGrounded && desiredVelocity.sqrMagnitude > 0.01f)
        {
            rb.AddForce(Vector3.down * extraDownForce, ForceMode.Acceleration);
        }
    }

    private void DoGroundCheck()
    {
        // Start near the bottom of the cube
        Vector3 origin = transform.position + Vector3.down * bottomOffset;

        // Perform a short sphere cast downward to detect ground and get a proper normal
        float castDistance = groundCheckDistance + 0.01f; // small epsilon
        isGrounded = false;
        groundNormal = Vector3.up;

        // Use SphereCast for robust contact with various collider types
        if (Physics.SphereCast(
                origin,
                groundCheckRadius,
                Vector3.down,
                out RaycastHit hit,
                castDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore))
        {
            // Check slope limit
            float dot = Vector3.Dot(hit.normal, Vector3.up);
            float slopeAngle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

            if (slopeAngle <= maxGroundSlope)
            {
                isGrounded = true;
                groundNormal = hit.normal;
            }
        }
        else
        {
            // As a fallback, if the bottom is already interpenetrating ground, use an OverlapSphere
            Collider[] overlaps = Physics.OverlapSphere(
                origin + Vector3.down * 0.02f,
                groundCheckRadius,
                groundLayer,
                QueryTriggerInteraction.Ignore);

            if (overlaps.Length > 0)
            {
                // Consider grounded on gentle surfaces when overlapping
                isGrounded = true;
                groundNormal = Vector3.up;
            }
        }
    }

    private Vector3 ProjectOnGround(Vector3 velocity, Vector3 normal)
    {
        if (velocity.sqrMagnitude < 0.0001f) return Vector3.zero;
        return Vector3.ProjectOnPlane(velocity, normal);
    }

    private void RotateTowardsMovement(Vector3 moveVel)
    {
        Vector3 flat = new Vector3(moveVel.x, 0f, moveVel.z);
        if (flat.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(flat, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            turnSpeed * Time.deltaTime
        );
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + Vector3.down * bottomOffset;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(origin, origin + Vector3.down * groundCheckDistance);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + groundNormal * 0.75f);
        }
    }
#endif
}

