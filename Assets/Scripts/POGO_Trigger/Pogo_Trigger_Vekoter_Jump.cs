using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ExtendedTrigger : MonoBehaviour
{
    public enum ReferenceMode
    {
        Point,
        Line
    }

    [Header("Anchor")]
    [Tooltip("Optional. Wenn gesetzt, wird der Trigger relativ zu diesem Anchor positioniert. Wenn leer, wird transform dieses Objekts benutzt.")]
    public Transform triggerAnchor;

    [Header("Trigger Volume")]
    public Vector2 triggerSizeXZ = new Vector2(2f, 2f);
    public float triggerHeightY = 3f;

    [Tooltip("x = lokale Rechts-Achse des Anchors, y = Welt-Y-Achse, z = lokale Vorwärts-Achse des Anchors")]
    public Vector3 triggerOffset = Vector3.zero;

    [Header("Reference")]
    [Tooltip("Der Referenzpunkt startet jetzt immer von der Mitte des grünen Triggers.")]
    public ReferenceMode referenceMode = ReferenceMode.Point;

    [Tooltip("Offset relativ zur Mitte des grünen Triggers. 0,0,0 = Mitte des Triggers.")]
    public Vector3 pointLocalOffset = Vector3.zero;

    [Tooltip("Linienstart relativ zur Mitte des grünen Triggers.")]
    public Vector3 lineLocalStart = new Vector3(-0.5f, 0f, 0f);

    [Tooltip("Linienende relativ zur Mitte des grünen Triggers.")]
    public Vector3 lineLocalEnd = new Vector3(0.5f, 0f, 0f);

    [Header("Impulse")]
    public float pushImpulse = 10f;
    public bool onlyHorizontal = true;
    public bool moveTowardReference = false;

    [Header("Gizmos")]
    public bool showGizmo = true;
    public Color gizmoTriggerFill = new Color(0f, 1f, 0f, 0.25f);
    public Color gizmoTriggerWire = Color.green;
    public Color gizmoPoint = Color.red;
    public Color gizmoLine = new Color(1f, 0.5f, 0f);
    public Color gizmoVector = Color.cyan;

    private BoxCollider triggerCollider;
    private Transform currentPlayer;
    private Rigidbody currentPlayerRb;

    private int playerColliderCount = 0;

    private void Awake()
    {
        EnsureTriggerColliderSetup();
    }

    private void Start()
    {
        EnsureTriggerColliderSetup();
    }

    public void RefreshTrigger()
    {
        EnsureTriggerColliderSetup();
    }

    private Transform GetTriggerAnchor()
    {
        return triggerAnchor != null ? triggerAnchor : transform;
    }

    private void EnsureTriggerColliderSetup()
    {
        triggerCollider = GetComponent<BoxCollider>();

        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }

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

        Vector3 triggerCenter = GetTriggerCenterWorld();

        Vector3 worldOffset =
            anchor.right * localOffset.x +
            Vector3.up * localOffset.y +
            anchor.forward * localOffset.z;

        return triggerCenter + worldOffset;
    }

    private void Update()
    {
        if (currentPlayer != null && Input.GetKeyDown(KeyCode.Space))
        {
            ApplyImpulseToPlayer();
        }
    }

    private void ApplyImpulseToPlayer()
    {
        if (currentPlayerRb == null)
        {
            return;
        }

        Vector3 refPos = GetReferencePosition(currentPlayer.position);
        Vector3 direction = currentPlayer.position - refPos;

        if (onlyHorizontal)
        {
            direction.y = 0f;
        }

        if (moveTowardReference)
        {
            direction = -direction;
        }

        if (direction.sqrMagnitude < 1e-6f)
        {
            return;
        }

        currentPlayerRb.AddForce(direction.normalized * pushImpulse, ForceMode.Impulse);
    }

    private Vector3 GetReferencePosition(Vector3 playerWorldPos)
    {
        if (referenceMode == ReferenceMode.Point)
        {
            return TriggerLocalOffsetToWorld(pointLocalOffset);
        }

        Vector3 a = TriggerLocalOffsetToWorld(lineLocalStart);
        Vector3 b = TriggerLocalOffsetToWorld(lineLocalEnd);

        return ClosestPointOnSegment(a, b, playerWorldPos);
    }

    private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float abLengthSqr = ab.sqrMagnitude;

        if (abLengthSqr < 1e-8f)
        {
            return a;
        }

        float t = Vector3.Dot(p - a, ab) / abLengthSqr;
        t = Mathf.Clamp01(t);

        return a + t * ab;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerColliderCount++;

        Rigidbody rb = other.attachedRigidbody;

        currentPlayerRb = rb;
        currentPlayer = rb != null ? rb.transform : other.transform;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        Rigidbody rb = other.attachedRigidbody;

        if (currentPlayerRb == null)
        {
            currentPlayerRb = rb;
        }

        if (currentPlayer == null)
        {
            currentPlayer = rb != null ? rb.transform : other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);

        if (playerColliderCount == 0)
        {
            currentPlayer = null;
            currentPlayerRb = null;
        }
    }

    private void Reset()
    {
        EnsureTriggerColliderSetup();
    }

    private void OnValidate()
    {
        ClampValues();

        triggerCollider = GetComponent<BoxCollider>();

        if (triggerCollider == null)
        {
            return;
        }

        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(triggerSizeXZ.x, triggerHeightY, triggerSizeXZ.y);
        triggerCollider.center = GetTriggerCenterLocal();
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo)
        {
            return;
        }

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

        Gizmos.color = gizmoTriggerFill;
        Gizmos.DrawCube(centerLocalPreview, size);

        Gizmos.color = gizmoTriggerWire;
        Gizmos.DrawWireCube(centerLocalPreview, size);

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawReferenceGizmo()
    {
        if (referenceMode == ReferenceMode.Point)
        {
            Vector3 refPoint = TriggerLocalOffsetToWorld(pointLocalOffset);

            Gizmos.color = gizmoPoint;
            Gizmos.DrawSphere(refPoint, 0.15f);

            if (Application.isPlaying && currentPlayer != null)
            {
                DrawVector(refPoint, currentPlayer.position, onlyHorizontal, moveTowardReference, gizmoVector);
            }
        }
        else
        {
            Vector3 a = TriggerLocalOffsetToWorld(lineLocalStart);
            Vector3 b = TriggerLocalOffsetToWorld(lineLocalEnd);

            Gizmos.color = gizmoLine;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.08f);
            Gizmos.DrawSphere(b, 0.08f);

            Vector3 closest;

            if (Application.isPlaying && currentPlayer != null)
            {
                closest = ClosestPointOnSegment(a, b, currentPlayer.position);
            }
            else
            {
                closest = (a + b) * 0.5f;
            }

            Gizmos.color = gizmoPoint;
            Gizmos.DrawSphere(closest, 0.12f);

            if (Application.isPlaying && currentPlayer != null)
            {
                DrawVector(closest, currentPlayer.position, onlyHorizontal, moveTowardReference, gizmoVector);
            }
        }
    }

    private void DrawVector(Vector3 refPos, Vector3 playerPos, bool horizontalOnly, bool towardRef, Color color)
    {
        Vector3 direction = playerPos - refPos;

        if (horizontalOnly)
        {
            direction.y = 0f;
        }

        if (towardRef)
        {
            direction = -direction;
        }

        if (direction.sqrMagnitude < 1e-8f)
        {
            return;
        }

        direction.Normalize();

        Vector3 end = refPos + direction;

        Gizmos.color = color;
        Gizmos.DrawLine(refPos, end);

        DrawArrowHead(end, direction, color);
    }

    private void DrawArrowHead(Vector3 position, Vector3 direction, Color color)
    {
        if (direction.sqrMagnitude < 1e-8f)
        {
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(direction);

        Vector3 right = rotation * Quaternion.Euler(0f, 180f + 20f, 0f) * Vector3.forward;
        Vector3 left = rotation * Quaternion.Euler(0f, 180f - 20f, 0f) * Vector3.forward;

        Gizmos.color = color;
        Gizmos.DrawLine(position, position + right * 0.2f);
        Gizmos.DrawLine(position, position + left * 0.2f);
    }
}