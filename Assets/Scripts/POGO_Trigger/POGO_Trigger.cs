using UnityEngine;

public class ExtendedTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    public Vector3 triggerSize = new Vector3(2f, 3f, 2f); // Breite, Höhe, Tiefe
    public Vector3 triggerOffset = new Vector3(0f, 1f, 0f); // Verschiebung nach oben

    [Header("Detection")]
    public LayerMask playerLayer;
    public bool showGizmo = true;

    private BoxCollider triggerCollider;

    void Start()
    {
        // BoxCollider als Trigger erstellen
        triggerCollider = gameObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = triggerSize;
        triggerCollider.center = triggerOffset;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Spieler hat Trigger betreten!");
            // Deine Logik hier
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Spieler ist im Trigger!");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Spieler hat Trigger verlassen!");
        }
    }

    // Visualisierung im Editor
    void OnDrawGizmos()
    {
        if (showGizmo)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(triggerOffset, triggerSize);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(triggerOffset, triggerSize);
        }
    }
}
