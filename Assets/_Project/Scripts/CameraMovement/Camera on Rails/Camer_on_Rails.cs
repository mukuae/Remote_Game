using UnityEngine;

public class CameraRail : MonoBehaviour
{
    [Header("Rail Points")]
    [Tooltip("Die Wegpunkte, denen die Kamera folgt")]
    public Transform[] railPoints;
    
    [Header("Target")]
    [Tooltip("Der Spieler")]
    public Transform player;
    
    [Header("Camera Settings")]
    [Tooltip("Wie weit die Kamera vorausschaut")]
    public float lookAheadDistance = 2f;
    
    [Tooltip("Höhe des Look-At Punktes am Spieler")]
    public float lookAtHeight = 1.5f;
    
    [Tooltip("Smoothing der Kamerabewegung")]
    public float smoothSpeed = 5f;
    
    [Tooltip("Smoothing der Rotation")]
    public float rotationSpeed = 3f;
    
    [Header("Distance Settings")]
    [Tooltip("Minimaler Abstand zum Spieler")]
    public float minDistance = 5f;
    
    [Tooltip("Maximaler Abstand zum Spieler")]
    public float maxDistance = 15f;
    
    private float currentRailPosition = 0f;
    private Vector3 currentVelocity;

    void Start()
    {
        if (railPoints.Length < 2)
        {
            Debug.LogError("Mindestens 2 Rail Points benötigt!");
        }
        
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }
    }

    void LateUpdate()
    {
        if (player == null || railPoints.Length < 2) return;

        // Finde den nächsten Punkt auf der Schiene zum Spieler
        Vector3 closestPoint = FindClosestPointOnRail(player.position);
        
        // Berechne Zielposition mit Abstandsbegrenzung
        Vector3 targetPosition = closestPoint;
        float distanceToPlayer = Vector3.Distance(closestPoint, player.position);
        
        // Halte Kamera im erlaubten Abstandsbereich
        if (distanceToPlayer < minDistance)
        {
            Vector3 direction = (closestPoint - player.position).normalized;
            targetPosition = player.position + direction * minDistance;
        }
        else if (distanceToPlayer > maxDistance)
        {
            Vector3 direction = (closestPoint - player.position).normalized;
            targetPosition = player.position + direction * maxDistance;
        }
        
        // Smoothe Bewegung
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            targetPosition, 
            ref currentVelocity, 
            1f / smoothSpeed
        );
        
        // Schaue auf den Spieler mit Look-Ahead
        Vector3 playerVelocity = GetPlayerVelocity();
        Vector3 lookAtPoint = player.position + 
            new Vector3(0, lookAtHeight, 0) + 
            playerVelocity.normalized * lookAheadDistance;
        
        // Smoothe Rotation
        Quaternion targetRotation = Quaternion.LookRotation(lookAtPoint - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, 
            targetRotation, 
            rotationSpeed * Time.deltaTime
        );
    }

    Vector3 FindClosestPointOnRail(Vector3 position)
    {
        Vector3 closestPoint = railPoints[0].position;
        float closestDistance = float.MaxValue;
        
        // Durchsuche alle Segmente der Schiene
        for (int i = 0; i < railPoints.Length - 1; i++)
        {
            Vector3 segmentStart = railPoints[i].position;
            Vector3 segmentEnd = railPoints[i + 1].position;
            
            Vector3 pointOnSegment = GetClosestPointOnLineSegment(
                segmentStart, 
                segmentEnd, 
                position
            );
            
            float distance = Vector3.Distance(position, pointOnSegment);
            
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = pointOnSegment;
            }
        }
        
        return closestPoint;
    }

    Vector3 GetClosestPointOnLineSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 lineDirection = end - start;
        float lineLength = lineDirection.magnitude;
        lineDirection.Normalize();
        
        Vector3 startToPoint = point - start;
        float dotProduct = Vector3.Dot(startToPoint, lineDirection);
        
        dotProduct = Mathf.Clamp(dotProduct, 0f, lineLength);
        
        return start + lineDirection * dotProduct;
    }

    Vector3 GetPlayerVelocity()
    {
        // Versuche Velocity vom Rigidbody oder CharacterController zu holen
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null) return rb.linearVelocity;
        
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) return cc.velocity;
        
        return Vector3.zero;
    }

    // Visualisierung der Schiene im Editor
    void OnDrawGizmos()
    {
        if (railPoints == null || railPoints.Length < 2) return;
        
        Gizmos.color = Color.yellow;
        
        for (int i = 0; i < railPoints.Length - 1; i++)
        {
            if (railPoints[i] != null && railPoints[i + 1] != null)
            {
                Gizmos.DrawLine(railPoints[i].position, railPoints[i + 1].position);
                Gizmos.DrawWireSphere(railPoints[i].position, 0.3f);
            }
        }
        
        if (railPoints[railPoints.Length - 1] != null)
        {
            Gizmos.DrawWireSphere(railPoints[railPoints.Length - 1].position, 0.3f);
        }
    }
}
