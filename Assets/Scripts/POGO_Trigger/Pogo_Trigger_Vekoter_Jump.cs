using UnityEngine;

public class ExtendedTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    public Vector3 triggerSize = new Vector3(2f, 3f, 2f); // Breite, Höhe, Tiefe
    public Vector3 triggerOffset = new Vector3(0f, 1f, 0f); // Verschiebung nach oben

    [Header("Jump Settings")]
    public float jumpForce = 10f; // Stärke des Sprungs
    public bool useRigidbody = true; // Physik-basiert oder direkt bewegen?

    [Header("Detection")]
    public LayerMask playerLayer;
    public bool showGizmo = true;

    private BoxCollider triggerCollider;
    private Transform currentPlayer; // Speichert den Spieler wenn er im Trigger ist
    private bool playerInTrigger = false;
    private Rigidbody rb;

    void Start()
    {
        // BoxCollider als Trigger erstellen
        triggerCollider = gameObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = triggerSize;
        triggerCollider.center = triggerOffset;

        // Rigidbody holen oder erstellen (falls Physik gewünscht)
        if (useRigidbody)
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.useGravity = true;
            }
        }
    }

    void Update()
    {
        // Prüfen ob Spieler im Trigger ist UND Space gedrückt wird
        if (playerInTrigger && Input.GetKeyDown(KeyCode.Space))
        {
            JumpWithVector();
        }
    }

    void JumpWithVector()
    {
        if (currentPlayer != null)
        {
            // Vektor von Cube-Mitte zum Spieler berechnen
            Vector3 vectorToPlayer = currentPlayer.position - transform.position;

            Debug.Log($"Cube springt mit Vektor: {vectorToPlayer}");
            Debug.Log($"Sprung-Stärke: {jumpForce}");

            if (useRigidbody && rb != null)
            {
                // Physik-basierter Sprung
                rb.AddForce(vectorToPlayer.normalized * jumpForce, ForceMode.Impulse);
            }
            else
            {
                // Direktes Bewegen (ohne Physik)
                transform.position += vectorToPlayer.normalized * jumpForce * Time.deltaTime;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            currentPlayer = other.transform;
            playerInTrigger = true;

            // Vektor von Cube-Mitte zum Spieler berechnen
            Vector3 vectorToPlayer = other.transform.position - transform.position;

            Debug.Log("Spieler hat Trigger betreten!");
            Debug.Log($"Vektor (Cube-Mitte → Spieler): {vectorToPlayer}");
            Debug.Log($"Distanz: {vectorToPlayer.magnitude:F2}m");
            Debug.Log("Drücke SPACE um den Cube springen zu lassen!");
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            currentPlayer = other.transform; // Position aktualisieren
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Spieler hat Trigger verlassen!");
            currentPlayer = null;
            playerInTrigger = false;
        }
    }

    // Visualisierung im Editor
    void OnDrawGizmos()
    {
        if (showGizmo)
        {
            // Trigger-Bereich (grün)
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(triggerOffset, triggerSize);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(triggerOffset, triggerSize);

            Gizmos.matrix = Matrix4x4.identity;

            // Cube-Mitte (rote Kugel)
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, 0.2f);

            // Wenn Spieler im Trigger ist: Linie und Vektor anzeigen
            if (Application.isPlaying && currentPlayer != null)
            {
                // Gelbe Linie von Cube-Mitte zum Spieler
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, currentPlayer.position);

                // Vektor-Pfeil (cyan)
                Vector3 vectorToPlayer = currentPlayer.position - transform.position;
                DrawArrow(transform.position, vectorToPlayer, Color.cyan);

                // Sprung-Vorschau (magenta)
                Vector3 jumpPreview = vectorToPlayer.normalized * jumpForce * 0.1f;
                DrawArrow(transform.position, jumpPreview, Color.magenta);
            }
        }
    }

    // Hilfsmethode um einen Pfeil zu zeichnen
    void DrawArrow(Vector3 start, Vector3 direction, Color color)
    {
        Gizmos.color = color;
        Vector3 end = start + direction;

        // Hauptlinie
        Gizmos.DrawLine(start, end);

        // Pfeilspitze
        if (direction.magnitude > 0.1f)
        {
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;

            Gizmos.DrawLine(end, end + right * 0.3f);
            Gizmos.DrawLine(end, end + left * 0.3f);
        }
    }
}
