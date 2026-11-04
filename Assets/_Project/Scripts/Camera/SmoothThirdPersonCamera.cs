using UnityEngine;

public class SmoothThirdPersonCamera : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The character the camera follows. If empty, the object tagged 'Player' is used.")]
    public Transform target;

    [Header("Camera Position")]
    [Tooltip("Camera offset relative to the target.")]
    public Vector3 offset = new Vector3(0f, 2f, -5f);

    [Header("Smoothing")]
    [Tooltip("How fast the camera follows (higher = more direct).")]
    [Range(1f, 20f)]
    public float followSpeed = 10f;

    [Tooltip("How fast the camera rotates.")]
    [Range(1f, 20f)]
    public float rotationSpeed = 8f;

    [Header("Look At Settings")]
    [Tooltip("Point the camera looks at, relative to the target.")]
    public Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Collision Detection")]
    [Tooltip("Keeps the camera from going through walls.")]
    public bool detectCollisions = true;

    [Tooltip("Layers that block the camera.")]
    public LayerMask collisionLayers = -1;

    [Tooltip("Radius used for collision detection.")]
    public float collisionRadius = 0.3f;

    private void Start()
    {
        if (target != null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;
        else
            Debug.LogError("SmoothThirdPersonCamera: no target found. Assign 'target' or tag your player 'Player'.", this);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + target.TransformDirection(offset);

        if (detectCollisions)
        {
            Vector3 direction = desiredPosition - target.position;
            float distance = direction.magnitude;

            if (Physics.SphereCast(target.position + lookAtOffset, collisionRadius,
                direction.normalized, out RaycastHit hit, distance, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                // Something is in the way - move the camera closer.
                desiredPosition = hit.point - direction.normalized * collisionRadius;
            }
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        Vector3 lookAtPoint = target.position + lookAtOffset;
        Quaternion targetRotation = Quaternion.LookRotation(lookAtPoint - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
}
