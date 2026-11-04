using UnityEngine;

public class StableFollowCamera : MonoBehaviour
{
    public Transform target;

    [Header("Position")]
    public Vector3 offset = new Vector3(0f, 6f, -8f);
    public float followSmoothTime = 0.2f;

    [Header("Look At")]
    public Vector3 lookOffset = new Vector3(0f, 1.5f, 0f);
    public float rotationSmoothSpeed = 6f;

    [Header("Look Ahead")]
    public float lookAheadDistance = 1.5f;
    public float lookAheadSmoothSpeed = 4f;
    public float movementThreshold = 0.05f;

    private Vector3 followVelocity;
    private Vector3 lastTargetPosition;
    private Vector3 currentLookAhead;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("StableFollowCamera: Kein Target gesetzt.");
            return;
        }

        lastTargetPosition = target.position;
        transform.position = target.position + offset;
        transform.LookAt(target.position + lookOffset);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 frameDelta = target.position - lastTargetPosition;

        Vector3 desiredLookAhead = Vector3.zero;

        Vector3 flatDelta = new Vector3(frameDelta.x, 0f, frameDelta.z);

        if (flatDelta.magnitude > movementThreshold)
        {
            desiredLookAhead = flatDelta.normalized * lookAheadDistance;
        }

        currentLookAhead = Vector3.Lerp(
            currentLookAhead,
            desiredLookAhead,
            Time.deltaTime * lookAheadSmoothSpeed
        );

        Vector3 desiredPosition = target.position + offset + currentLookAhead;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            followSmoothTime
        );

        Vector3 lookTarget = target.position + lookOffset + currentLookAhead * 0.5f;
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            Time.deltaTime * rotationSmoothSpeed
        );

        lastTargetPosition = target.position;
    }
}
