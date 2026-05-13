using UnityEngine;

public class ExtendedTrigger : MonoBehaviour
{
    public enum ReferenceMode { Point, Line }

    public Vector2 triggerSizeXZ = new Vector2(2f, 2f);
    public float triggerHeightY = 3f;

    public Vector3 triggerOffset = Vector3.zero;

    public ReferenceMode referenceMode = ReferenceMode.Point;
    public Vector3 pointLocalOffset = Vector3.zero;
    public Vector3 lineLocalStart = new Vector3(-0.5f, 0f, 0f);
    public Vector3 lineLocalEnd = new Vector3(0.5f, 0f, 0f);

    public float pushImpulse = 10f;
    public bool onlyHorizontal = true;
    public bool moveTowardReference = false;

    public bool showGizmo = true;
    public Color gizmoTriggerFill = new Color(0f, 1f, 0f, 0.25f);
    public Color gizmoTriggerWire = Color.green;
    public Color gizmoPoint = Color.red;
    public Color gizmoLine = new Color(1f, 0.5f, 0f);
    public Color gizmoVector = Color.cyan;

    private BoxCollider triggerCollider;
    private Transform currentPlayer;
    private Rigidbody currentPlayerRb;

    void Start()
    {
        EnsureTriggerColliderSetup();
    }

    void EnsureTriggerColliderSetup()
    {
        triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider == null) triggerCollider = gameObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(triggerSizeXZ.x, triggerHeightY, triggerSizeXZ.y);

        Vector3 objCenterWorld = transform.position;
        Vector3 worldOffset = transform.right * triggerOffset.x + Vector3.up * triggerOffset.y + transform.forward * triggerOffset.z;
        Vector3 triggerCenterWorld = objCenterWorld + worldOffset;
        triggerCollider.center = transform.InverseTransformPoint(triggerCenterWorld);
    }

    void Update()
    {
        if (currentPlayer != null && Input.GetKeyDown(KeyCode.Space)) ApplyImpulseToPlayer();
    }

    void ApplyImpulseToPlayer()
    {
        if (currentPlayerRb == null) return;
        Vector3 refPos = GetReferencePosition(currentPlayer.position);
        Vector3 v = currentPlayer.position - refPos;
        if (onlyHorizontal) v.y = 0f;
        if (moveTowardReference) v = -v;
        if (v.sqrMagnitude < 1e-6f) return;
        currentPlayerRb.AddForce(v.normalized * pushImpulse, ForceMode.Impulse);
    }

    Vector3 GetReferencePosition(Vector3 playerWorldPos)
    {
        if (referenceMode == ReferenceMode.Point) return transform.TransformPoint(pointLocalOffset);
        Vector3 a = transform.TransformPoint(lineLocalStart);
        Vector3 b = transform.TransformPoint(lineLocalEnd);
        return ClosestPointOnSegment(a, b, playerWorldPos);
    }

    static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float abLenSqr = Vector3.SqrMagnitude(ab);
        if (abLenSqr < 1e-8f) return a;
        float t = Vector3.Dot(p - a, ab) / abLenSqr;
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        currentPlayer = other.transform;
        currentPlayerRb = other.attachedRigidbody;
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (currentPlayer == null) currentPlayer = other.transform;
        if (currentPlayerRb == null) currentPlayerRb = other.attachedRigidbody;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        currentPlayer = null;
        currentPlayerRb = null;
    }

    void OnValidate()
    {
        if (Application.isEditor && !Application.isPlaying) EnsureTriggerColliderSetup();
    }

    void OnDrawGizmos()
    {
        if (!showGizmo) return;

        if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider>();

        Vector3 objCenterWorld = transform.position;
        Vector3 worldOffset = transform.right * triggerOffset.x + Vector3.up * triggerOffset.y + transform.forward * triggerOffset.z;
        Vector3 triggerCenterWorld = objCenterWorld + worldOffset;
        Vector3 centerLocalPreview = transform.InverseTransformPoint(triggerCenterWorld);

        Vector3 size = (triggerCollider != null)
            ? triggerCollider.size
            : new Vector3(triggerSizeXZ.x, triggerHeightY, triggerSizeXZ.y);

        Gizmos.color = gizmoTriggerFill;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(centerLocalPreview, size);
        Gizmos.color = gizmoTriggerWire;
        Gizmos.DrawWireCube(centerLocalPreview, size);

        Gizmos.matrix = Matrix4x4.identity;

        if (referenceMode == ReferenceMode.Point)
        {
            Vector3 refPoint = transform.TransformPoint(pointLocalOffset);
            Gizmos.color = gizmoPoint;
            Gizmos.DrawSphere(refPoint, 0.15f);
            if (Application.isPlaying && currentPlayer != null)
                DrawVector(refPoint, currentPlayer.position, onlyHorizontal, moveTowardReference, gizmoVector);
        }
        else
        {
            Vector3 a = transform.TransformPoint(lineLocalStart);
            Vector3 b = transform.TransformPoint(lineLocalEnd);
            Gizmos.color = gizmoLine;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.08f);
            Gizmos.DrawSphere(b, 0.08f);
            Vector3 closest = (Application.isPlaying && currentPlayer != null)
                ? ClosestPointOnSegment(a, b, currentPlayer.position)
                : (a + b) * 0.5f;
            Gizmos.color = gizmoPoint;
            Gizmos.DrawSphere(closest, 0.12f);
            if (Application.isPlaying && currentPlayer != null)
                DrawVector(closest, currentPlayer.position, onlyHorizontal, moveTowardReference, gizmoVector);
        }
    }

    void DrawVector(Vector3 refPos, Vector3 playerPos, bool horizontalOnly, bool towardRef, Color color)
    {
        Vector3 v = playerPos - refPos;
        if (horizontalOnly) v.y = 0f;
        if (towardRef) v = -v;
        if (v.sqrMagnitude < 1e-8f) return;
        Vector3 end = refPos + v.normalized;
        Gizmos.color = color;
        Gizmos.DrawLine(refPos, end);
        Vector3 dir = (end - refPos).normalized;
        Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
        Gizmos.DrawLine(end, end + right * 0.2f);
        Gizmos.DrawLine(end, end + left * 0.2f);
    }
}
