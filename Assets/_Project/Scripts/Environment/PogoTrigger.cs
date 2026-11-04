using UnityEngine;

// Trigger volume that launches the player when they press Jump while standing inside it.
// The launch direction points away from (or toward) a reference point or line.
// The jump input itself is read by PlayerController, which calls TryLaunch(), so a pogo
// launch and a normal jump can never happen on the same button press.
[RequireComponent(typeof(BoxCollider))]
public class PogoTrigger : MonoBehaviour
{
    public enum ReferenceMode
    {
        Point,
        Line
    }

    public enum RechargeMode
    {
        OnLanding,   // one launch, usable again once the player touches the ground
        OnReentry,   // one launch, usable again after leaving and re-entering the trigger
        Never        // one launch for the whole level
    }

    [Header("Anchor")]
    [Tooltip("Optional. If set, the trigger is positioned relative to this anchor. If empty, this object's transform is used.")]
    public Transform triggerAnchor;

    [Header("Trigger Volume")]
    public Vector2 triggerSizeXZ = new Vector2(2f, 2f);
    public float triggerHeightY = 3f;

    [Tooltip("x = anchor's local right axis, y = world Y axis, z = anchor's local forward axis")]
    public Vector3 triggerOffset = Vector3.zero;

    [Header("Reference")]
    [Tooltip("The reference point/line is measured from the center of the green trigger volume.")]
    public ReferenceMode referenceMode = ReferenceMode.Point;

    [Tooltip("Offset from the center of the green trigger. 0,0,0 = trigger center.")]
    public Vector3 pointLocalOffset = Vector3.zero;

    [Tooltip("Line start, relative to the center of the green trigger.")]
    public Vector3 lineLocalStart = new Vector3(-0.5f, 0f, 0f);

    [Tooltip("Line end, relative to the center of the green trigger.")]
    public Vector3 lineLocalEnd = new Vector3(0.5f, 0f, 0f);

    [Header("Impulse")]
    public float pushImpulse = 10f;
    public bool onlyHorizontal = true;
    public bool moveTowardReference = false;

    [Header("Usage")]
    [Tooltip("The pogo can only be used once; this decides when it becomes usable again.")]
    public RechargeMode rechargeMode = RechargeMode.OnLanding;

    public bool IsUsedUp { get; private set; }

    [Header("Gizmos")]
    public bool showGizmo = true;
    public Color gizmoTriggerFill = new Color(0f, 1f, 0f, 0.25f);
    public Color gizmoTriggerWire = Color.green;
    public Color gizmoPoint = Color.red;
    public Color gizmoLine = new Color(1f, 0.5f, 0f);
    public Color gizmoVector = Color.cyan;

    private BoxCollider triggerCollider;
    private PlayerController currentPlayer;
    private int playerColliderCount = 0;

    private void Awake()
    {
        EnsureTriggerColliderSetup();
    }

    private void OnDisable()
    {
        if (currentPlayer != null)
            currentPlayer.ExitPogoZone(this);

        currentPlayer = null;
        playerColliderCount = 0;
    }

    public void RefreshTrigger()
    {
        EnsureTriggerColliderSetup();
    }

    // Called by PlayerController when Jump is pressed inside this trigger.
    // Returns false if the pogo is used up or no sensible launch direction exists
    // (the press is then handled as a normal jump).
    public bool TryLaunch(PlayerController player)
    {
        if (IsUsedUp && rechargeMode == RechargeMode.OnLanding && player.IsGrounded)
            IsUsedUp = false;

        if (IsUsedUp) return false;

        Vector3 playerPosition = player.transform.position;
        Vector3 direction = playerPosition - GetReferencePosition(playerPosition);

        if (onlyHorizontal) direction.y = 0f;
        if (moveTowardReference) direction = -direction;
        if (direction.sqrMagnitude < 1e-6f) return false;

        player.PogoJump(direction.normalized, pushImpulse);
        IsUsedUp = true;
        return true;
    }

    private Transform GetTriggerAnchor()
    {
        return triggerAnchor != null ? triggerAnchor : transform;
    }

    private void EnsureTriggerColliderSetup()
    {
        triggerCollider = GetComponent<BoxCollider>();

        if (triggerCollider == null)
            triggerCollider = gameObject.AddComponent<BoxCollider>();

        ApplyColliderSettings();
    }

    private void ApplyColliderSettings()
    {
        ClampValues();

        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(triggerSizeXZ.x, triggerHeightY, triggerSizeXZ.y);
        triggerCollider.center = GetTriggerCenterLocal();
    }

    private void ClampValues()
    {
        triggerSizeXZ.x = Mathf.Max(0.01f, triggerSizeXZ.x);
        triggerSizeXZ.y = Mathf.Max(0.01f, triggerSizeXZ.y);
        triggerHeightY = Mathf.Max(0.01f, triggerHeightY);
        pushImpulse = Mathf.Max(0f, pushImpulse);
    }

    private Vector3 GetTriggerCenterWorld()
    {
        Transform anchor = GetTriggerAnchor();

        Vector3 worldOffset =
            anchor.right * triggerOffset.x +
            Vector3.up * triggerOffset.y +
            anchor.forward * triggerOffset.z;

        return anchor.position + worldOffset;
    }

    private Vector3 GetTriggerCenterLocal()
    {
        return transform.InverseTransformPoint(GetTriggerCenterWorld());
    }

    private Vector3 TriggerLocalOffsetToWorld(Vector3 localOffset)
    {
        Transform anchor = GetTriggerAnchor();

        Vector3 worldOffset =
            anchor.right * localOffset.x +
            Vector3.up * localOffset.y +
            anchor.forward * localOffset.z;

        return GetTriggerCenterWorld() + worldOffset;
    }

    private Vector3 GetReferencePosition(Vector3 playerWorldPos)
    {
        if (referenceMode == ReferenceMode.Point)
            return TriggerLocalOffsetToWorld(pointLocalOffset);

        Vector3 a = TriggerLocalOffsetToWorld(lineLocalStart);
        Vector3 b = TriggerLocalOffsetToWorld(lineLocalEnd);

        return ClosestPointOnSegment(a, b, playerWorldPos);
    }

    private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float abLengthSqr = ab.sqrMagnitude;

        if (abLengthSqr < 1e-8f)
            return a;

        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLengthSqr);
        return a + t * ab;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        playerColliderCount++;
        currentPlayer = player;
        currentPlayer.EnterPogoZone(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);

        if (playerColliderCount == 0 && currentPlayer != null)
        {
            currentPlayer.ExitPogoZone(this);
            currentPlayer = null;

            if (rechargeMode == RechargeMode.OnReentry)
                IsUsedUp = false;
        }
    }

    private void Reset()
    {
        EnsureTriggerColliderSetup();
    }

    private void OnValidate()
    {
        triggerCollider = GetComponent<BoxCollider>();

        if (triggerCollider == null)
        {
            ClampValues();
            return;
        }

        ApplyColliderSettings();
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        DrawTriggerGizmo();
        DrawReferenceGizmo();
    }

    private void DrawTriggerGizmo()
    {
        Vector3 centerLocalPreview = GetTriggerCenterLocal();

        Vector3 size = new Vector3(
            Mathf.Max(0.01f, triggerSizeXZ.x),
            Mathf.Max(0.01f, triggerHeightY),
            Mathf.Max(0.01f, triggerSizeXZ.y)
        );

        Gizmos.matrix = transform.localToWorldMatrix;

        // Grey while used up, so you can see the state in the Scene view during Play mode.
        bool showUsed = Application.isPlaying && IsUsedUp;

        Gizmos.color = showUsed ? new Color(0.5f, 0.5f, 0.5f, 0.25f) : gizmoTriggerFill;
        Gizmos.DrawCube(centerLocalPreview, size);

        Gizmos.color = showUsed ? Color.grey : gizmoTriggerWire;
        Gizmos.DrawWireCube(centerLocalPreview, size);

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawReferenceGizmo()
    {
        bool showPlayerVector = Application.isPlaying && currentPlayer != null;
        Vector3 playerPosition = showPlayerVector ? currentPlayer.transform.position : Vector3.zero;

        if (referenceMode == ReferenceMode.Point)
        {
            Vector3 refPoint = TriggerLocalOffsetToWorld(pointLocalOffset);

            Gizmos.color = gizmoPoint;
            Gizmos.DrawSphere(refPoint, 0.15f);

            if (showPlayerVector)
                DrawVector(refPoint, playerPosition, onlyHorizontal, moveTowardReference, gizmoVector);

            return;
        }

        Vector3 a = TriggerLocalOffsetToWorld(lineLocalStart);
        Vector3 b = TriggerLocalOffsetToWorld(lineLocalEnd);

        Gizmos.color = gizmoLine;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawSphere(a, 0.08f);
        Gizmos.DrawSphere(b, 0.08f);

        Vector3 closest = showPlayerVector ? ClosestPointOnSegment(a, b, playerPosition) : (a + b) * 0.5f;

        Gizmos.color = gizmoPoint;
        Gizmos.DrawSphere(closest, 0.12f);

        if (showPlayerVector)
            DrawVector(closest, playerPosition, onlyHorizontal, moveTowardReference, gizmoVector);
    }

    private void DrawVector(Vector3 refPos, Vector3 playerPos, bool horizontalOnly, bool towardRef, Color color)
    {
        Vector3 direction = playerPos - refPos;

        if (horizontalOnly) direction.y = 0f;
        if (towardRef) direction = -direction;
        if (direction.sqrMagnitude < 1e-8f) return;

        direction.Normalize();
        Vector3 end = refPos + direction;

        Gizmos.color = color;
        Gizmos.DrawLine(refPos, end);
        DrawArrowHead(end, direction, color);
    }

    private void DrawArrowHead(Vector3 position, Vector3 direction, Color color)
    {
        if (direction.sqrMagnitude < 1e-8f) return;

        Quaternion rotation = Quaternion.LookRotation(direction);
        Vector3 right = rotation * Quaternion.Euler(0f, 180f + 20f, 0f) * Vector3.forward;
        Vector3 left = rotation * Quaternion.Euler(0f, 180f - 20f, 0f) * Vector3.forward;

        Gizmos.color = color;
        Gizmos.DrawLine(position, position + right * 0.2f);
        Gizmos.DrawLine(position, position + left * 0.2f);
    }
}
