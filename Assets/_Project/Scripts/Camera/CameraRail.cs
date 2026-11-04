using UnityEngine;

// Camera that slides along a path of rail points, staying as close to the player as the rail allows.
public class CameraRail : MonoBehaviour
{
    [Header("Rail Points")]
    [Tooltip("Waypoints the camera moves along.")]
    public Transform[] railPoints;

    [Header("Target")]
    [Tooltip("The player. If empty, the object tagged 'Player' is used.")]
    public Transform player;

    [Header("Camera Settings")]
    [Tooltip("How far ahead of the player the camera looks.")]
    public float lookAheadDistance = 2f;

    [Tooltip("Height of the look-at point on the player.")]
    public float lookAtHeight = 1.5f;

    [Tooltip("Smoothing of the camera movement.")]
    public float smoothSpeed = 5f;

    [Tooltip("Smoothing of the rotation.")]
    public float rotationSpeed = 3f;

    [Header("Distance Settings")]
    [Tooltip("Minimum distance to the player.")]
    public float minDistance = 5f;

    [Tooltip("Maximum distance to the player.")]
    public float maxDistance = 15f;

    private Vector3 currentVelocity;
    private Rigidbody playerRigidbody;
    private CharacterController playerCharacterController;

    private void Start()
    {
        if (railPoints == null || railPoints.Length < 2)
            Debug.LogError("CameraRail: at least 2 rail points are required.", this);

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player != null)
        {
            playerRigidbody = player.GetComponent<Rigidbody>();
            playerCharacterController = player.GetComponent<CharacterController>();
        }
    }

    private void LateUpdate()
    {
        if (player == null || railPoints == null || railPoints.Length < 2) return;

        // Closest point on the rail to the player.
        Vector3 closestPoint = FindClosestPointOnRail(player.position);

        // Keep the camera within the allowed distance range.
        Vector3 targetPosition = closestPoint;
        float distanceToPlayer = Vector3.Distance(closestPoint, player.position);
        Vector3 directionFromPlayer = (closestPoint - player.position).normalized;

        if (distanceToPlayer < minDistance)
            targetPosition = player.position + directionFromPlayer * minDistance;
        else if (distanceToPlayer > maxDistance)
            targetPosition = player.position + directionFromPlayer * maxDistance;

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, 1f / smoothSpeed);

        // Look at the player, slightly ahead in their movement direction.
        Vector3 lookAtPoint = player.position +
            new Vector3(0, lookAtHeight, 0) +
            GetPlayerVelocity().normalized * lookAheadDistance;

        Quaternion targetRotation = Quaternion.LookRotation(lookAtPoint - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private Vector3 FindClosestPointOnRail(Vector3 position)
    {
        Vector3 closestPoint = railPoints[0].position;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < railPoints.Length - 1; i++)
        {
            Vector3 pointOnSegment = GetClosestPointOnLineSegment(railPoints[i].position, railPoints[i + 1].position, position);
            float distance = Vector3.Distance(position, pointOnSegment);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = pointOnSegment;
            }
        }

        return closestPoint;
    }

    private static Vector3 GetClosestPointOnLineSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 lineDirection = end - start;
        float lineLength = lineDirection.magnitude;
        if (lineLength < 0.0001f) return start;

        lineDirection /= lineLength;
        float projection = Mathf.Clamp(Vector3.Dot(point - start, lineDirection), 0f, lineLength);
        return start + lineDirection * projection;
    }

    private Vector3 GetPlayerVelocity()
    {
        if (playerRigidbody != null) return playerRigidbody.linearVelocity;
        if (playerCharacterController != null) return playerCharacterController.velocity;
        return Vector3.zero;
    }

    // Draws the rail in the Scene view.
    private void OnDrawGizmos()
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
            Gizmos.DrawWireSphere(railPoints[railPoints.Length - 1].position, 0.3f);
    }
}
