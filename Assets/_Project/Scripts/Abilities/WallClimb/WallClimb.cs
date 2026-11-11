using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Wall-climb ability for the CharacterController-based <see cref="PlayerController"/>.
/// Add it to the player next to PlayerController.
///
/// Only walls with a <see cref="ClimbableWall"/> component can be climbed, and only
/// while the ability is unlocked (tick "Ability Unlocked", or call <see cref="Unlock"/>
/// from a pickup/trigger/etc.).
///
/// Controls (uses the same input actions as PlayerController):
/// - Walk into a climbable wall to grab it.
/// - Move up/down (and left/right) to climb.
/// - Climb past the top edge to pull yourself up onto the ledge.
/// - Jump to leap away from the wall.
/// - Climb down onto the ground to let go.
///
/// While climbing, PlayerController is disabled; it gets control back when you let go.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("Abilities/Wall Climb")]
public class WallClimb : MonoBehaviour
{
    [Header("Ability")]
    [Tooltip("The player can only climb while this is ticked. Can also be set with Unlock()/Lock().")]
    [SerializeField] private bool abilityUnlocked = false;

    [Header("References")]
    [Tooltip("Defaults to the PlayerController on this object.")]
    [SerializeField] private PlayerController playerController;
    [Tooltip("Used to turn stick/WASD input into a world direction. Defaults to the main camera.")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Optional. Gets a bool parameter set while climbing (if the parameter exists).")]
    [SerializeField] private Animator animator;
    [SerializeField] private string climbingAnimatorBool = "IsClimbing";

    [Header("Input (assign the same actions as PlayerController)")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Climbing")]
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private float sidewaysSpeed = 2.5f;
    [Tooltip("Left/right input moves the player sideways along the wall (towards screen-left/right).")]
    [SerializeField] private bool allowSidewaysClimbing = true;
    [Tooltip("How far in front of the player a wall can be to grab it.")]
    [SerializeField] private float grabDistance = 0.25f;
    [Tooltip("How many degrees a surface may lean away from vertical and still count as a wall.")]
    [Range(0f, 60f)] [SerializeField] private float maxWallTilt = 30f;
    [Tooltip("Seconds after letting go before the player can grab a wall again.")]
    [SerializeField] private float regrabCooldown = 0.3f;
    [Tooltip("Which layers are checked for walls and ledges.")]
    [SerializeField] private LayerMask wallLayers = ~0;

    [Header("Wall Jump")]
    [SerializeField] private float wallJumpForce = 8f;

    [Header("Ledge Climb")]
    [SerializeField] private float ledgeClimbDuration = 0.35f;

    private CharacterController characterController;

    private bool isClimbing;
    private bool isClimbingLedge;
    private Vector3 wallNormal;
    private float regrabTimer;

    private Vector3 ledgeStart;
    private Vector3 ledgeTarget;
    private float ledgeTimer;

    private int climbingBoolHash;
    private bool animatorHasClimbingBool;

    private readonly Collider[] overlapBuffer = new Collider[16];

    public bool AbilityUnlocked
    {
        get => abilityUnlocked;
        set => abilityUnlocked = value;
    }

    public bool IsClimbing => isClimbing || isClimbingLedge;

    public void Unlock() => abilityUnlocked = true;
    public void Lock() => abilityUnlocked = false;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (playerController == null)
        {
            Debug.LogError($"{nameof(WallClimb)} on '{name}' needs a PlayerController on the same object.", this);
            enabled = false;
            return;
        }

        if (moveAction == null)
            Debug.LogWarning($"{nameof(WallClimb)} on '{name}' has no Move action assigned; climbing won't work.", this);

        if (animator != null && !string.IsNullOrEmpty(climbingAnimatorBool))
        {
            climbingBoolHash = Animator.StringToHash(climbingAnimatorBool);
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == climbingBoolHash && p.type == AnimatorControllerParameterType.Bool)
                {
                    animatorHasClimbingBool = true;
                    break;
                }
            }
        }
    }

    private void OnEnable()
    {
        EnableActions();
    }

    private void OnDisable()
    {
        if (!isClimbing && !isClimbingLedge)
            return;

        if (isClimbingLedge)
            characterController.enabled = true;

        isClimbing = false;
        isClimbingLedge = false;
        SetAnimatorClimbing(false);

        // LightPoints disables every script on death; don't hand movement back to a dead player.
        bool playerIsDead = TryGetComponent(out LightPoints lightPoints) && lightPoints.IsDead;
        if (!playerIsDead && playerController != null)
        {
            playerController.ResetMotion();
            playerController.enabled = true;
        }
    }

    private void Update()
    {
        if (PauseMenu.GameIsPaused)
            return;

        regrabTimer -= Time.deltaTime;

        if (isClimbingLedge)
        {
            UpdateLedgeClimb();
            return;
        }

        Vector2 input = moveAction != null ? Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f) : Vector2.zero;

        if (!isClimbing)
        {
            if (abilityUnlocked && regrabTimer <= 0f && TryFindWallToGrab(input, out RaycastHit hit))
                StartClimbing(hit.normal);
            return;
        }

        // Ability was taken away mid-climb.
        if (!abilityUnlocked)
        {
            StopClimbing();
            return;
        }

        if (jumpAction != null && jumpAction.action.WasPressedThisFrame())
        {
            WallJump();
            return;
        }

        Climb(input);
    }

    // ---------------------------------------------------------------- grabbing

    private bool TryFindWallToGrab(Vector2 input, out RaycastHit hit)
    {
        hit = default;

        if (input.sqrMagnitude < 0.01f)
            return false;

        Vector3 moveDir = InputToWorldDirection(input);
        if (moveDir.sqrMagnitude < 0.01f)
            return false;

        GetCapsule(transform.position, out Vector3 center, out _, out float radius, out _);
        float castRadius = radius * 0.5f;
        float castDistance = radius - castRadius + characterController.skinWidth + grabDistance;

        if (!Physics.SphereCast(center, castRadius, moveDir, out hit, castDistance, wallLayers, QueryTriggerInteraction.Ignore))
            return false;

        return IsClimbable(hit.collider) && IsWall(hit.normal) && Vector3.Dot(moveDir, -hit.normal) > 0.5f;
    }

    private Vector3 InputToWorldDirection(Vector2 input)
    {
        Transform reference = cameraTransform != null ? cameraTransform : transform;

        Vector3 forward = reference.forward;
        Vector3 right = reference.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f).normalized;
    }

    private void StartClimbing(Vector3 normal)
    {
        isClimbing = true;
        wallNormal = normal;

        // PlayerController disables the shared input actions in its OnDisable, so turn them back on.
        playerController.enabled = false;
        EnableActions();

        SetAnimatorClimbing(true);
    }

    private void StopClimbing()
    {
        isClimbing = false;
        regrabTimer = regrabCooldown;

        SetAnimatorClimbing(false);

        playerController.ResetMotion();
        playerController.enabled = true;
    }

    private void WallJump()
    {
        Vector3 pushDir = wallNormal;
        pushDir.y = 0f;

        StopClimbing();
        playerController.PogoJump(pushDir, wallJumpForce);
    }

    // ---------------------------------------------------------------- climbing

    private void Climb(Vector2 input)
    {
        Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;

        Vector3 move = wallUp * input.y * climbSpeed;
        if (allowSidewaysClimbing)
            move += SidewaysDirection() * input.x * sidewaysSpeed;

        // Small push into the wall so the player stays in contact.
        move += -wallNormal * 1f;

        CollisionFlags flags = characterController.Move(move * Time.deltaTime);

        // Climbed down onto the floor: let go.
        if (input.y < -0.1f && (flags & CollisionFlags.Below) != 0)
        {
            StopClimbing();
            return;
        }

        GetCapsule(transform.position, out Vector3 center, out Vector3 top, out float radius, out _);
        float probeDistance = radius + characterController.skinWidth + grabDistance;

        bool bodyOnWall = ProbeWall(center, probeDistance, out RaycastHit bodyHit);
        bool headOnWall = ProbeWall(top, probeDistance, out _);

        // Reached the top edge while climbing up: try to pull up onto the ledge.
        if (input.y > 0.1f && !headOnWall)
        {
            if (TryStartLedgeClimb())
                return;
        }

        if (!bodyOnWall)
        {
            StopClimbing();
            return;
        }

        wallNormal = Vector3.Slerp(wallNormal, bodyHit.normal, 15f * Time.deltaTime).normalized;
        FaceWall();
    }

    // Left/right input moves the player towards screen-right along the wall.
    // Not normalized on purpose: on a wall facing the camera this is full speed, on a wall that
    // runs towards the camera it shrinks to ~zero, so side-view players can't leave the play plane.
    private Vector3 SidewaysDirection()
    {
        Transform reference = cameraTransform != null ? cameraTransform : transform;
        Vector3 screenRight = reference.right;
        screenRight.y = 0f;
        screenRight.Normalize();

        Vector3 alongWall = Vector3.ProjectOnPlane(screenRight, wallNormal);
        alongWall.y = 0f;
        return alongWall;
    }

    private bool ProbeWall(Vector3 origin, float distance, out RaycastHit hit)
    {
        return Physics.Raycast(origin, -wallNormal, out hit, distance, wallLayers, QueryTriggerInteraction.Ignore)
               && IsClimbable(hit.collider)
               && IsWall(hit.normal);
    }

    private void FaceWall()
    {
        Vector3 facing = -wallNormal;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
            return;

        Quaternion target = Quaternion.LookRotation(facing.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 15f * Time.deltaTime);
    }

    // ---------------------------------------------------------------- ledge climb

    private bool TryStartLedgeClimb()
    {
        GetCapsule(transform.position, out _, out _, out float radius, out float height);

        Vector3 forward = -wallNormal;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return false;
        forward.Normalize();

        Vector3 target = transform.position + Vector3.up * height + forward * (radius * 2f + 0.1f);

        // Only climb up if the player fits on top.
        GetCapsule(target, out Vector3 targetCenter, out _, out _, out _);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 p1 = targetCenter - Vector3.up * halfSegment;
        Vector3 p2 = targetCenter + Vector3.up * halfSegment;

        int count = Physics.OverlapCapsuleNonAlloc(p1, p2, radius, overlapBuffer, wallLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!overlapBuffer[i].transform.IsChildOf(transform))
                return false;
        }

        isClimbing = false;
        isClimbingLedge = true;
        ledgeStart = transform.position;
        ledgeTarget = target;
        ledgeTimer = 0f;

        // Move the transform directly during the short pull-up animation.
        characterController.enabled = false;
        return true;
    }

    private void UpdateLedgeClimb()
    {
        ledgeTimer += Time.deltaTime;
        float t = ledgeClimbDuration > 0f ? Mathf.Clamp01(ledgeTimer / ledgeClimbDuration) : 1f;

        // First go up, then forward onto the ledge.
        Vector3 corner = new Vector3(ledgeStart.x, ledgeTarget.y, ledgeStart.z);
        transform.position = t < 0.6f
            ? Vector3.Lerp(ledgeStart, corner, t / 0.6f)
            : Vector3.Lerp(corner, ledgeTarget, (t - 0.6f) / 0.4f);

        if (t < 1f)
            return;

        isClimbingLedge = false;
        characterController.enabled = true;
        Physics.SyncTransforms();
        StopClimbing();
    }

    // ---------------------------------------------------------------- helpers

    private bool IsClimbable(Collider col)
    {
        if (col == null || col.transform.IsChildOf(transform))
            return false;

        ClimbableWall wall = col.GetComponentInParent<ClimbableWall>();
        return wall != null && wall.isActiveAndEnabled;
    }

    private bool IsWall(Vector3 normal)
    {
        return Mathf.Abs(normal.y) <= Mathf.Sin(maxWallTilt * Mathf.Deg2Rad);
    }

    // World-space capsule of the CharacterController if the player stood at 'position'.
    private void GetCapsule(Vector3 position, out Vector3 center, out Vector3 top, out float radius, out float height)
    {
        Vector3 scale = transform.lossyScale;
        radius = characterController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        height = Mathf.Max(characterController.height * Mathf.Abs(scale.y), radius * 2f);

        center = position + transform.rotation * Vector3.Scale(characterController.center, scale);
        top = center + Vector3.up * (height * 0.5f - radius);
    }

    private void EnableActions()
    {
        if (moveAction != null)
            moveAction.action.Enable();

        if (jumpAction != null)
            jumpAction.action.Enable();
    }

    private void SetAnimatorClimbing(bool value)
    {
        if (animatorHasClimbingBool)
            animator.SetBool(climbingBoolHash, value);
    }
}
