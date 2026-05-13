using UnityEngine;

public class ExtendedTrigger : MonoBehaviour
{
    public enum ReferenceMode
    {
        Point,  // fester Punkt
        Line    // Linie/Segment
    }

    [Header("Trigger Settings")]
    public Vector3 triggerSize = new Vector3(2f, 3f, 2f);
    public Vector3 triggerOffset = new Vector3(0f, 1f, 0f);

    [Header("Reference (Point or Line)")]
    public ReferenceMode referenceMode = ReferenceMode.Point;

    // POINT: relativer Offset vom Transform (lokal) zur Referenz
    public Vector3 pointLocalOffset = Vector3.zero;

    // LINE: lokale Start-/Endpunkte relativ zum Transform (z. B. Kantenpunkte der Plattform)
    public Vector3 lineLocalStart = new Vector3(-0.5f, 0f, 0f);
    public Vector3 lineLocalEnd = new Vector3(0.5f, 0f, 0f);

    [Header("Player Push Settings")]
    public float pushImpulse = 10f;        // Stärke des Impulses
    public bool onlyHorizontal = true;     // Y ignorieren?
    public bool moveTowardRef = false;     // false = vom Referenzpunkt weg, true = hin zum Referenzpunkt

    [Header("Debug / Gizmos")]
    public bool showGizmo = true;
    public Color gizmoTriggerFill = new Color(0f, 1f, 0f, 0.25f);
    public Color gizmoTriggerWire = Color.green;
    public Color gizmoPoint = Color.red;
    public Color gizmoLine = Color.magenta;
    public Color gizmoVector = Color.cyan;

    private BoxCollider triggerCollider;
    private Transform currentPlayer;
    private Rigidbody currentPlayerRb;

    void Start()
    {
        // Trigger-Collider anlegen
        triggerCollider = gameObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = triggerSize;
        triggerCollider.center = triggerOffset;
    }

    void Update()
    {
        if (currentPlayer != null && Input.GetKeyDown(KeyCode.Space))
        {
            ApplyImpulseToPlayer();
        }
    }

    void ApplyImpulseToPlayer()
    {
        if (currentPlayerRb == null) return;

        // Referenzpunkt bestimmen (je nach Modus)
        Vector3 refPos = GetReferenceWorldPosition();

        // Vektor von Referenz → Spieler
        Vector3 v = currentPlayer.position - refPos;

        if (onlyHorizontal) v.y = 0f;

        // Richtung wählen: vom Referenzpunkt weg (Standard) oder hin
        if (moveTowardRef) v = -v;

        if (v.sqrMagnitude < 0.0001f) return;

        currentPlayerRb.AddForce(v.normalized * pushImpulse, ForceMode.Impulse);
        Debug.Log($"Impulse to Player. refMode={referenceMode}, refPos={refPos}, dir={v.normalized}, impulse={pushImpulse}");
    }

    // Ermittelt die Weltposition des Referenzpunkts (Punkt oder nächster Punkt auf der Linie)
    Vector3 GetReferenceWorldPosition()
    {
        if (referenceMode == ReferenceMode.Point)
        {
            // lokaler Punkt → Welt
            return transform.TransformPoint(pointLocalOffset);
        }
        else // ReferenceMode.Line
        {
            // lokale Linienpunkte → Welt
            Vector3 a = transform.TransformPoint(lineLocalStart);
            Vector3 b = transform.TransformPoint(lineLocalEnd);

            // Wenn kein Player vorhanden, gib einfach Linienmittelpunkt zurück (z. B. für Gizmos)
            if (currentPlayer == null) return (a + b) * 0.5f;

            // Nächstgelegenen Punkt auf der Linie (a→b) zu Spieler finden
            Vector3 p = currentPlayer.position;
            Vector3 closest = ClosestPointOnSegment(a, b, p);
            return closest;
        }
    }

    // Mathe-Helfer: Nächster Punkt auf einem Liniensegment
    static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float abLenSqr = Vector3.SqrMagnitude(ab);
        if (abLenSqr < 1e-6f) return a; // degeneriertes Segment

        float t = Vector3.Dot(p - a, ab) / abLenSqr;
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            currentPlayer = other.transform;
            currentPlayerRb = other.attachedRigidbody;
            Debug.Log("Player im Trigger. SPACE drückt einen Impuls auf den Player (vom gewählten Referenzpunkt aus).");
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (currentPlayer == null) currentPlayer = other.transform;
            if (currentPlayerRb == null) currentPlayerRb = other.attachedRigidbody;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            currentPlayer = null;
            currentPlayerRb = null;
        }
    }

    // Gizmos zur Visualisierung
    void OnDrawGizmos()
    {
        if (!showGizmo) return;

        // Trigger-Volumen
        Gizmos.color = gizmoTriggerFill;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(triggerOffset, triggerSize);
        Gizmos.color = gizmoTriggerWire;
        Gizmos.DrawWireCube(triggerOffset, triggerSize);

        Gizmos.matrix = Matrix4x4.identity;

        // Referenz anzeigen
        if (referenceMode == ReferenceMode.Point)
        {
            Vector3 refPoint = Application.isPlaying
                ? transform.TransformPoint(pointLocalOffset)
                : transform.TransformPoint(pointLocalOffset);

            Gizmos.color = gizmoPoint;
            Gizmos.DrawSphere(refPoint, 0.15f);

            // Vorschau-Vektor, falls Player vorhanden
            if (Application.isPlaying && currentPlayer != null)
            {
                Vector3 v = currentPlayer.position - refPoint;
                if (onlyHorizontal) v.y = 0f;
                if (moveTowardRef) v = -v;

                Gizmos.color = gizmoVector;
                Gizmos.DrawLine(refPoint, refPoint + v.normalized);
            }
        }
        else // Line
        {
            // Linie in Weltcoords
            Vector3 a = transform.TransformPoint(lineLocalStart);
            Vector3 b = transform.TransformPoint(lineLocalEnd);

            Gizmos.color = gizmoLine;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.08f);
            Gizmos.DrawSphere(b, 0.08f);

            // Nächster Punkt zu Player (falls vorhanden)
            if (Application.isPlaying && currentPlayer != null)
            {
                Vector3 closest = ClosestPointOnSegment(a, b, currentPlayer.position);

                Gizmos.color = gizmoPoint;
                Gizmos.DrawSphere(closest, 0.12f);

                Vector3 v = currentPlayer.position - closest;
                if (onlyHorizontal) v.y = 0f;
                if (moveTowardRef) v = -v;

                Gizmos.color = gizmoVector;
                Gizmos.DrawLine(closest, closest + v.normalized);
            }
            else
            {
                // Linienmittelpunkt markieren, wenn kein Player vorhanden
                Vector3 mid = 0.5f * (a + b);
                Gizmos.color = gizmoPoint;
                Gizmos.DrawSphere(mid, 0.12f);
            }
        }
    }
}
