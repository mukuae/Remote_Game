using UnityEngine;

// Spider-style wall/roof climbing ability.
// Attach to the same GameObject as your CharacterController and your
// existing ground-movement script.
//
// Press X to toggle the ability on/off. While it's on, walking toward
// anything on the Climbable layer sticks you to it, and your normal
// Horizontal/Vertical input crawls you across that surface - up walls,
// around corners, onto ceilings. Space lets go. Your own movement
// script is simply disabled while you're stuck to a surface and
// re-enabled the instant you detach, so it keeps doing everything else
// exactly as before.
[RequireComponent(typeof(CharacterController))]
public class SpiderClimb : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.X;
    public KeyCode letGoKey = KeyCode.Space;

    [Header("Scripts to pause while climbing")]
    [Tooltip("Drag your ground-movement script here (and a separate gravity script too, if you have one). They get disabled while stuck to a surface and re-enabled the moment you let go.")]
    public MonoBehaviour[] scriptsToDisableWhileClimbing;

    [Header("Surface Detection")]
    [Tooltip("Put your walls/roofs on their own layer and select ONLY that layer here - otherwise the ability will also try to stick to the ground.")]
    public LayerMask climbableMask = ~0;
    public float attachRange = 0.8f;
    public float stickRange = 1.0f;
    public float hugDistance = 0.05f;
    public Vector3 probeOffset = new Vector3(0f, 1f, 0f);

    [Header("Climbing Movement")]
    public float climbSpeed = 3.5f;
    public float turnSpeed = 540f;
    public float detachCooldown = 0.25f;
    public float detachPushback = 0.3f;

    public bool AbilityActive { get; private set; }
    public bool IsClimbing { get; private set; }

    CharacterController cc;
    Vector3 surfaceNormal = Vector3.up;
    float cooldownTimer;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            AbilityActive = !AbilityActive;
            if (!AbilityActive && IsClimbing) Detach(false);
        }

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (!IsClimbing)
        {
            if (AbilityActive && cooldownTimer <= 0f) TryAttach();
            return;
        }

        if (Input.GetKeyDown(letGoKey))
        {
            Detach(true);
            return;
        }

        ClimbMove();
    }

    void TryAttach()
    {
        Vector3 origin = transform.position + probeOffset;
        Vector3[] probes =
        {
            transform.forward,
            Vector3.up,
            (transform.forward + Vector3.up).normalized
        };

        foreach (var dir in probes)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, attachRange, climbableMask))
            {
                Attach(hit.normal);
                return;
            }
        }
    }

    void Attach(Vector3 normal)
    {
        IsClimbing = true;
        surfaceNormal = normal;
        cc.enabled = false; // drive the transform by hand while climbing so it can rotate freely onto walls/ceilings
        SetScriptsEnabled(false);
        SnapRotation(normal, transform.forward);
    }

    void Detach(bool pushOff)
    {
        IsClimbing = false;
        if (pushOff) transform.position += surfaceNormal * detachPushback;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        cc.enabled = true;
        SetScriptsEnabled(true);
        cooldownTimer = detachCooldown;
    }

    void SetScriptsEnabled(bool enable)
    {
        foreach (var script in scriptsToDisableWhileClimbing)
            if (script) script.enabled = enable;
    }

    void ClimbMove()
    {
        Vector3 climbUp = Vector3.ProjectOnPlane(Vector3.up, surfaceNormal).normalized;
        if (climbUp.sqrMagnitude < 0.001f)
            climbUp = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;
        Vector3 climbRight = Vector3.Cross(surfaceNormal, climbUp).normalized;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 move = climbRight * h + climbUp * v;
        if (move.sqrMagnitude > 1f) move.Normalize();
        transform.position += move * climbSpeed * Time.deltaTime;

        // Re-probe every frame: keeps you hugging the surface and lets you
        // wrap from a wall onto a ceiling (or around a corner) as you walk into one.
        Vector3 origin = transform.position + probeOffset;
        Vector3[] probes = { -surfaceNormal, transform.forward, climbUp, -climbUp };
        bool stillStuck = false;

        foreach (var dir in probes)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, stickRange, climbableMask))
            {
                transform.position = hit.point + hit.normal * hugDistance - probeOffset;
                surfaceNormal = hit.normal;
                stillStuck = true;
                break;
            }
        }

        if (!stillStuck)
        {
            Detach(false); // walked off the edge of the climbable surface
            return;
        }

        SnapRotation(surfaceNormal, climbUp);
    }

    void SnapRotation(Vector3 normal, Vector3 forwardHint)
    {
        Vector3 forward = Vector3.ProjectOnPlane(forwardHint, normal);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(Vector3.up, normal);

        Quaternion target = Quaternion.LookRotation(forward.normalized, normal);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }
}