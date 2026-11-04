using UnityEngine;

public class SmoothThirdPersonCamera : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Der Charakter, dem die Kamera folgen soll")]
    public Transform target;
    
    [Header("Camera Position")]
    [Tooltip("Offset der Kamera relativ zum Ziel")]
    public Vector3 offset = new Vector3(0f, 2f, -5f);
    
    [Header("Smoothing")]
    [Tooltip("Wie schnell die Kamera folgt (höher = direkter)")]
    [Range(1f, 20f)]
    public float followSpeed = 10f;
    
    [Tooltip("Wie schnell die Kamera rotiert")]
    [Range(1f, 20f)]
    public float rotationSpeed = 8f;
    
    [Header("Look At Settings")]
    [Tooltip("Punkt, auf den die Kamera schaut (relativ zum Ziel)")]
    public Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);
    
    [Header("Collision Detection")]
    [Tooltip("Verhindert, dass die Kamera durch Wände geht")]
    public bool detectCollisions = true;
    
    [Tooltip("Layer, die Kollisionen verursachen")]
    public LayerMask collisionLayers = -1;
    
    [Tooltip("Radius für Kollisionserkennung")]
    public float collisionRadius = 0.3f;
    
    private Vector3 currentVelocity;
    private Vector3 desiredPosition;

    void Start()
    {
        // Wenn kein Target gesetzt ist, versuche den Spieler zu finden
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogError("Kein Target für die Kamera gefunden! Bitte setze das 'target' oder tagge deinen Spieler mit 'Player'");
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Berechne die gewünschte Position
        desiredPosition = target.position + target.TransformDirection(offset);

        // Kollisionserkennung
        if (detectCollisions)
        {
            Vector3 direction = desiredPosition - target.position;
            float distance = direction.magnitude;
            
            RaycastHit hit;
            if (Physics.SphereCast(target.position + lookAtOffset, collisionRadius, 
                direction.normalized, out hit, distance, collisionLayers))
            {
                // Wenn Kollision erkannt, bewege Kamera näher
                desiredPosition = hit.point - direction.normalized * collisionRadius;
            }
        }

        // Smoothe Position mit Lerp
        transform.position = Vector3.Lerp(transform.position, desiredPosition, 
            followSpeed * Time.deltaTime);

        // Smoothe Rotation - schaue auf den Zielpunkt
        Vector3 lookAtPoint = target.position + lookAtOffset;
        Quaternion targetRotation = Quaternion.LookRotation(lookAtPoint - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
            rotationSpeed * Time.deltaTime);
    }
}
