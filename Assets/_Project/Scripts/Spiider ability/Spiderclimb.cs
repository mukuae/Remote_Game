using UnityEngine;

public class SpiderClimbing : MonoBehaviour
{
    [Header("Climbing Settings")]
    public float climbSpeed = 4f;
    public float rotationSpeed = 10f;
    public float surfaceDetectionRange = 1.2f;
    public LayerMask climbableLayers;

    [Header("Sticky Settings")]
    public float stickyForce = 20f; // Force pushing player into the surface

    private Rigidbody rb;
    private bool isClimbing = false;
    private Vector3 surfaceNormal = Vector3.up;
    private bool wasKinematic;
    private float originalGravityScale;

    // Reference to disable your existing movement script while climbing
    // Drag your movement script type here if needed, or handle via event
    // public YourMovementScript movementScript; // <-- optional

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("SpiderClimbing: No Rigidbody found on " + gameObject.name);
        }
    }

    void Update()
    {
        // Toggle climbing with X
        if (Input.GetKeyDown(KeyCode.X))
        {
            ToggleClimbing();
        }

        if (isClimbing)
        {
            DetectSurface();
            HandleClimbInput();
            AlignToSurface();
        }
    }

    void FixedUpdate()
    {
        if (isClimbing)
        {
            // Push player into the surface so they stick
            rb.AddForce(-surfaceNormal * stickyForce);
        }
    }

    void ToggleClimbing()
    {
        isClimbing = !isClimbing;

        if (isClimbing)
        {
            // Disable gravity while climbing
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;

            // Optional: disable your movement script here
            // if (movementScript != null) movementScript.enabled = false;

            Debug.Log("Spider Climbing: ON");
        }
        else
        {
            // Re-enable gravity when climbing stops
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;

            // Snap rotation back to upright
            StartCoroutine(ResetRotation());

            // Optional: re-enable your movement script here
            // if (movementScript != null) movementScript.enabled = true;

            Debug.Log("Spider Climbing: OFF");
        }
    }

    void DetectSurface()
    {
        // Cast a ray downward relative to the player's current orientation
        // to find the surface they are standing/climbing on
        RaycastHit hit;

        // Try current "down" direction first (relative to player)
        if (Physics.Raycast(transform.position, -transform.up, out hit, surfaceDetectionRange, climbableLayers))
        {
            surfaceNormal = hit.normal;
            return;
        }

        // Fallback: try world directions to find any nearby surface
        Vector3[] directions = {
            -transform.up,
            transform.up,
            transform.right,
            -transform.right,
            transform.forward,
            -transform.forward
        };

        float closestDist = float.MaxValue;
        foreach (Vector3 dir in directions)
        {
            if (Physics.Raycast(transform.position, dir, out hit, surfaceDetectionRange, climbableLayers))
            {
                if (hit.distance < closestDist)
                {
                    closestDist = hit.distance;
                    surfaceNormal = hit.normal;
                }
            }
        }
    }

    void HandleClimbInput()
    {
        // Up arrow = move forward along the surface
        // Down arrow = move backward along the surface
        float vertical = 0f;

        if (Input.GetKey(KeyCode.UpArrow))
            vertical = 1f;
        else if (Input.GetKey(KeyCode.DownArrow))
            vertical = -1f;

        if (vertical != 0f)
        {
            // Move along the surface: forward is perpendicular to the surface normal
            // and aligned with the player's current forward direction projected onto the surface
            Vector3 moveDir = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;
            rb.MovePosition(rb.position + moveDir * climbSpeed * vertical * Time.deltaTime);
        }
    }

    void AlignToSurface()
    {
        // Smoothly rotate the player so their "up" matches the surface normal
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, surfaceNormal) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    System.Collections.IEnumerator ResetRotation()
    {
        // Smoothly rotate back to upright when climbing ends
        Quaternion startRot = transform.rotation;
        Quaternion endRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * rotationSpeed;
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
    }

    // Visual debug: draw the surface normal in the Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, surfaceNormal * 1.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, -transform.up * surfaceDetectionRange);
    }
}
