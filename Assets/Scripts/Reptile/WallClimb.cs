using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Add this to the player object (next to LightPoints).
///
/// - Press R (Grab Key) next to a wall with a <see cref="ClimbableWall"/>
///   component, within Grab Distance, and the player sticks to it.
/// - While clinging: E moves up, Q moves down, A/D move sideways along the
///   wall (3D). Your normal movement script is switched off (see
///   Disable While Clinging) and gravity is ignored.
/// - Press R again to let go and fall/move as usual. The player also lets go
///   if the wall ends or moves out of reach.
///
/// Works in 3D (Rigidbody, CharacterController or plain transform) and 2D
/// (Rigidbody2D). Works with the old Input Manager and the new Input System.
///
/// Needs a Collider (or Collider2D) on the player. If the player has a
/// LightPoints component, clinging stops permanently when the player dies.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Light/Wall Climb")]
public class WallClimb : MonoBehaviour
{
    public enum Dimension { Auto, ThreeD, TwoD }

    private enum Mover { Rigidbody3D, Rigidbody2D, CharacterController, Transform }

    [Header("Keys")]
    [SerializeField] private KeyCode grabKey = KeyCode.R;
    [SerializeField] private KeyCode upKey = KeyCode.E;
    [SerializeField] private KeyCode downKey = KeyCode.Q;
    [Tooltip("Move left along the wall (3D only).")]
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [Tooltip("Move right along the wall (3D only).")]
    [SerializeField] private KeyCode rightKey = KeyCode.D;

    [Header("Grabbing")]
    [Tooltip("Max gap between the player's surface and a wall at which the grab key works.")]
    [Min(0f)] [SerializeField] private float grabDistance = 0.6f;

    [Tooltip("Layers searched for walls. A wall must ALSO have a ClimbableWall component to be grabbable.")]
    [SerializeField] private LayerMask surfaceLayers = ~0;

    [Header("Clinging")]
    [Tooltip("Speed along the wall, in units per second.")]
    [Min(0f)] [SerializeField] private float climbSpeed = 3f;

    [Tooltip("3D only: turn to face the wall while clinging.")]
    [SerializeField] private bool faceWall = true;

    [Tooltip("Your normal movement script(s). They are disabled while clinging and re-enabled on release, so they don't fight the climbing.")]
    [SerializeField] private MonoBehaviour[] disableWhileClinging;

    [Tooltip("Auto picks 2D if the player has a Collider2D and no 3D Collider.")]
    [SerializeField] private Dimension dimension = Dimension.Auto;

    [Header("Events")]
    public UnityEvent onGrab = new UnityEvent();
    public UnityEvent onRelease = new UnityEvent();

    /// <summary>True while the player is stuck to a wall.</summary>
    public bool IsClinging { get; private set; }

    // Surfaces steeper than this (|normal.y|) count as walls; floors and ceilings don't.
    private const float MaxWallNormalY = 0.7f;
    // Speed (units/s) with which the player is pressed into the wall to stay on it.
    private const float MinPush = 0.5f;
    private const float MaxPush = 4f;
    private const float PushGain = 10f;
    // Number of horizontal directions searched for walls in 3D.
    private const int RingRays = 8;

    private readonly RaycastHit[] hits3D = new RaycastHit[32];
    private readonly RaycastHit2D[] hits2D = new RaycastHit2D[32];
    private readonly List<MonoBehaviour> disabledByUs = new List<MonoBehaviour>();

    private LightPoints lightPoints;
    private Rigidbody body3D;
    private Rigidbody2D body2D;
    private CharacterController controller;
    private Collider collider3D;
    private Collider2D collider2D;

    private bool use2D;
    private Mover mover;

    private bool savedUseGravity;
    private float savedGravityScale;

    private Vector3 wallNormal;
    private Vector3 clingVelocity;

    private void Awake()
    {
        lightPoints = GetComponent<LightPoints>();
        body3D = GetComponent<Rigidbody>();
        body2D = GetComponent<Rigidbody2D>();
        controller = GetComponent<CharacterController>();
        collider3D = GetComponent<Collider>(); // also covers CharacterController
        collider2D = GetComponent<Collider2D>();

        switch (dimension)
        {
            case Dimension.ThreeD: use2D = false; break;
            case Dimension.TwoD: use2D = true; break;
            default: use2D = collider2D != null && collider3D == null; break;
        }

        if (use2D)
            mover = body2D != null ? Mover.Rigidbody2D : Mover.Transform;
        else if (controller != null)
            mover = Mover.CharacterController;
        else if (body3D != null && !body3D.isKinematic)
            mover = Mover.Rigidbody3D;
        else
            mover = Mover.Transform;

        if ((use2D && collider2D == null) || (!use2D && collider3D == null))
        {
            Debug.LogWarning("WallClimb needs a " + (use2D ? "Collider2D" : "Collider") + " on the player.", this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        // Silent: no events while being disabled/destroyed (e.g. on death or scene unload).
        Release(true);
    }

    private void Update()
    {
        if (lightPoints != null && lightPoints.IsDead) return;

        if (Keys.Down(grabKey))
        {
            if (IsClinging) Release();
            else TryGrab();
        }

        if (!IsClinging) return;

        // Keep track of the wall (it may curve, end, or move away).
        if (!CastForWall(BodyCenter(), -wallNormal, Support(-wallNormal) + grabDistance,
                         out float distance, out Vector3 normal))
        {
            Release();
            return;
        }
        wallNormal = normal;

        // Movement directions on the wall plane.
        Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
        Vector3 wallRight = use2D ? Vector3.zero : Vector3.Cross(wallNormal, wallUp);

        float h = Axis(rightKey, leftKey);
        float v = Axis(upKey, downKey);

        Vector3 slide = wallRight * h + wallUp * v;
        if (slide.sqrMagnitude > 1f) slide.Normalize();
        slide *= climbSpeed;

        // Keep the player pressed against the wall; harder if it's currently a bit away.
        float gap = Mathf.Max(0f, distance - Support(-wallNormal));
        float minPush = mover == Mover.Transform ? 0f : MinPush;
        float push = Mathf.Clamp(gap * PushGain, minPush, MaxPush);

        clingVelocity = slide - wallNormal * push;

        if (faceWall && !use2D)
        {
            Vector3 flat = Vector3.ProjectOnPlane(-wallNormal, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f)
            {
                Quaternion target = Quaternion.LookRotation(flat, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target,
                                                      1f - Mathf.Exp(-15f * Time.deltaTime));
            }
        }

        switch (mover)
        {
            case Mover.CharacterController:
                controller.Move(clingVelocity * Time.deltaTime);
                break;
            case Mover.Transform:
                transform.position += clingVelocity * Time.deltaTime;
                break;
        }
    }

    private void FixedUpdate()
    {
        // Rigidbody movement is applied here (no-op for the other movers).
        if (IsClinging) SetVelocity(clingVelocity);
    }

    // ---------------------------------------------------------------- grab / release

    private void TryGrab()
    {
        Vector3 origin = BodyCenter();
        int rays = use2D ? 2 : RingRays;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();

        bool found = false;
        float bestDistance = float.MaxValue;
        Vector3 bestNormal = Vector3.zero;

        for (int i = 0; i < rays; i++)
        {
            Vector3 direction = use2D
                ? (i == 0 ? Vector3.right : Vector3.left)
                : Quaternion.AngleAxis(i * (360f / RingRays), Vector3.up) * forward;

            if (CastForWall(origin, direction, Support(direction) + grabDistance,
                            out float distance, out Vector3 normal)
                && distance < bestDistance)
            {
                found = true;
                bestDistance = distance;
                bestNormal = normal;
            }
        }

        if (!found) return;

        wallNormal = bestNormal;
        Grab();
    }

    private void Grab()
    {
        IsClinging = true;
        clingVelocity = Vector3.zero;

        switch (mover)
        {
            case Mover.Rigidbody3D:
                savedUseGravity = body3D.useGravity;
                body3D.useGravity = false;
                break;
            case Mover.Rigidbody2D:
                savedGravityScale = body2D.gravityScale;
                body2D.gravityScale = 0f;
                break;
        }
        SetVelocity(Vector3.zero);

        disabledByUs.Clear();
        if (disableWhileClinging != null)
        {
            foreach (MonoBehaviour script in disableWhileClinging)
            {
                if (script == null || script == this || !script.enabled) continue;
                script.enabled = false;
                disabledByUs.Add(script);
            }
        }

        onGrab?.Invoke();
    }

    private void Release(bool silent = false)
    {
        if (!IsClinging) return;
        IsClinging = false;
        clingVelocity = Vector3.zero;

        switch (mover)
        {
            case Mover.Rigidbody3D:
                if (body3D != null) body3D.useGravity = savedUseGravity;
                break;
            case Mover.Rigidbody2D:
                if (body2D != null) body2D.gravityScale = savedGravityScale;
                break;
        }
        SetVelocity(Vector3.zero);

        // If the player died while clinging, LightPoints has just disabled the
        // movement scripts on purpose: don't switch them back on.
        bool dead = lightPoints != null && lightPoints.IsDead;
        if (!dead)
        {
            foreach (MonoBehaviour script in disabledByUs)
                if (script != null) script.enabled = true;
        }
        disabledByUs.Clear();

        if (!silent) onRelease?.Invoke();
    }

    // ---------------------------------------------------------------- helpers

    // Nearest surface along a ray; succeeds only if that surface is a climbable wall.
    private bool CastForWall(Vector3 origin, Vector3 direction, float length,
                             out float distance, out Vector3 normal)
    {
        distance = 0f;
        normal = Vector3.zero;

        Component nearest = null;
        float best = float.MaxValue;

        if (use2D)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(surfaceLayers);

            int count = Physics2D.Raycast(origin, direction, filter, hits2D, length);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits2D[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance >= best) continue;

                best = hit.distance;
                nearest = hit.collider;
                normal = hit.normal;
            }
        }
        else
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits3D, length,
                                                surfaceLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits3D[i];
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance >= best) continue;

                best = hit.distance;
                nearest = hit.collider;
                normal = hit.normal;
            }
        }

        if (nearest == null) return false;
        if (normal.sqrMagnitude < 0.01f) return false;           // ray started inside a collider
        if (Mathf.Abs(normal.y) > MaxWallNormalY) return false;  // floor/ceiling, not a wall

        ClimbableWall wall = nearest.GetComponentInParent<ClimbableWall>();
        if (wall == null || !wall.isActiveAndEnabled) return false;

        distance = best;
        return true;
    }

    private Vector3 BodyCenter()
    {
        return use2D ? collider2D.bounds.center : collider3D.bounds.center;
    }

    // Half-size of the player's bounds along a direction (how far its surface is from its center).
    private float Support(Vector3 direction)
    {
        Vector3 d = direction.normalized;
        Vector3 e = use2D ? collider2D.bounds.extents : collider3D.bounds.extents;
        return Mathf.Abs(d.x) * e.x + Mathf.Abs(d.y) * e.y + Mathf.Abs(d.z) * e.z;
    }

    private void SetVelocity(Vector3 velocity)
    {
        if (mover == Mover.Rigidbody3D && body3D != null)
        {
#if UNITY_6000_0_OR_NEWER
            body3D.linearVelocity = velocity;
#else
            body3D.velocity = velocity;
#endif
        }
        else if (mover == Mover.Rigidbody2D && body2D != null)
        {
#if UNITY_6000_0_OR_NEWER
            body2D.linearVelocity = velocity;
#else
            body2D.velocity = velocity;
#endif
        }
    }

    private static float Axis(KeyCode positive, KeyCode negative)
    {
        return (Keys.Held(positive) ? 1f : 0f) - (Keys.Held(negative) ? 1f : 0f);
    }

    // Keyboard reading that works with the new Input System and the old Input Manager.
    private static class Keys
    {
#if ENABLE_INPUT_SYSTEM
        private static readonly Dictionary<KeyCode, UnityEngine.InputSystem.Key> map =
            new Dictionary<KeyCode, UnityEngine.InputSystem.Key>();

        private static UnityEngine.InputSystem.Controls.KeyControl Get(KeyCode code)
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return null;

            UnityEngine.InputSystem.Key key;
            if (!map.TryGetValue(code, out key))
            {
                if (!System.Enum.TryParse(code.ToString(), out key))
                    key = UnityEngine.InputSystem.Key.None;
                map[code] = key;
            }

            return key == UnityEngine.InputSystem.Key.None ? null : keyboard[key];
        }

        public static bool Down(KeyCode code)
        {
            var control = Get(code);
            return control != null && control.wasPressedThisFrame;
        }

        public static bool Held(KeyCode code)
        {
            var control = Get(code);
            return control != null && control.isPressed;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        public static bool Down(KeyCode code) { return Input.GetKeyDown(code); }
        public static bool Held(KeyCode code) { return Input.GetKey(code); }
#else
        public static bool Down(KeyCode code) { return false; }
        public static bool Held(KeyCode code) { return false; }
#endif
    }
}
