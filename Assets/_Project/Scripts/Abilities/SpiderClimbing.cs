using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Lets a Rigidbody player stick to and walk along walls/ceilings on the climbable layers.
// Note: this needs a Rigidbody; it does not work with the CharacterController-based PlayerController.
public class SpiderClimbing : MonoBehaviour
{
    [Header("Input")]
    public Key toggleKey = Key.X;
    public Key climbForwardKey = Key.UpArrow;
    public Key climbBackwardKey = Key.DownArrow;

    [Header("Climbing Settings")]
    public float climbSpeed = 4f;
    public float rotationSpeed = 10f;
    public float surfaceDetectionRange = 1.2f;
    public LayerMask climbableLayers;

    [Header("Sticky Settings")]
    [Tooltip("Force pushing the player into the surface.")]
    public float stickyForce = 20f;

    [Header("Optional")]
    [Tooltip("Movement script to disable while climbing (e.g. your player controller).")]
    public MonoBehaviour movementScriptToDisable;

    private Rigidbody rb;
    private bool isClimbing;
    private float climbInput;
    private Vector3 surfaceNormal = Vector3.up;
    private Coroutine resetRotationRoutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"SpiderClimbing: no Rigidbody found on '{name}'. Component disabled.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (PauseMenu.GameIsPaused) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[toggleKey].wasPressedThisFrame)
            ToggleClimbing();

        climbInput = 0f;
        if (!isClimbing) return;

        if (keyboard[climbForwardKey].isPressed)
            climbInput = 1f;
        else if (keyboard[climbBackwardKey].isPressed)
            climbInput = -1f;
    }

    private void FixedUpdate()
    {
        if (!isClimbing) return;

        DetectSurface();

        // Push the player into the surface so they stick.
        rb.AddForce(-surfaceNormal * stickyForce);

        if (climbInput != 0f)
        {
            // Move along the surface, in the player's forward direction projected onto it.
            Vector3 moveDir = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;
            rb.MovePosition(rb.position + moveDir * climbSpeed * climbInput * Time.fixedDeltaTime);
        }

        // Smoothly rotate the player so their "up" matches the surface normal.
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, surfaceNormal) * rb.rotation;
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    private void ToggleClimbing()
    {
        isClimbing = !isClimbing;

        if (resetRotationRoutine != null)
        {
            StopCoroutine(resetRotationRoutine);
            resetRotationRoutine = null;
        }

        rb.useGravity = !isClimbing;
        rb.linearVelocity = Vector3.zero;

        if (movementScriptToDisable != null)
            movementScriptToDisable.enabled = !isClimbing;

        if (!isClimbing)
            resetRotationRoutine = StartCoroutine(ResetRotation());
    }

    private void DetectSurface()
    {
        // Try the player's current "down" first.
        if (Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, surfaceDetectionRange, climbableLayers))
        {
            surfaceNormal = hit.normal;
            return;
        }

        // Fallback: look in all local directions for the closest surface.
        Vector3[] directions =
        {
            transform.up,
            transform.right,
            -transform.right,
            transform.forward,
            -transform.forward
        };

        float closestDist = float.MaxValue;
        foreach (Vector3 dir in directions)
        {
            if (Physics.Raycast(transform.position, dir, out hit, surfaceDetectionRange, climbableLayers) &&
                hit.distance < closestDist)
            {
                closestDist = hit.distance;
                surfaceNormal = hit.normal;
            }
        }
    }

    private IEnumerator ResetRotation()
    {
        // Smoothly rotate back to upright when climbing ends.
        Quaternion startRot = transform.rotation;
        Quaternion endRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * rotationSpeed;
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
        resetRotationRoutine = null;
    }

    // Debug: draw the surface normal in the Scene view.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, surfaceNormal * 1.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, -transform.up * surfaceDetectionRange);
    }
}
