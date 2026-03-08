using UnityEngine;

public class StableCameraWithLookAhead : MonoBehaviour
{
    public Transform target;

    [Header("Offset")]
    public Vector3 offset = new Vector3(0f, 8f, -8f);

    [Header("Smooth")]
    public float smoothTimeX = 0.15f;
    public float smoothTimeZ = 0.15f;

    [Header("Look Ahead")]
    public float lookAheadDistance = 1.2f;
    public float lookAheadLerpSpeed = 5f;
    public float movementThreshold = 0.01f;

    private float velocityX;
    private float velocityZ;
    private float currentLookAheadX;
    private float fixedY;
    private Vector3 lastTargetPosition;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("StableCameraWithLookAhead: Kein Target gesetzt.");
            return;
        }

        fixedY = offset.y;
        lastTargetPosition = target.position;

        transform.position = new Vector3(
            target.position.x + offset.x,
            fixedY,
            target.position.z + offset.z
        );
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 delta = target.position - lastTargetPosition;

        float desiredLookAheadX = 0f;

        if (Mathf.Abs(delta.x) > movementThreshold)
        {
            desiredLookAheadX = Mathf.Sign(delta.x) * lookAheadDistance;
        }

        currentLookAheadX = Mathf.Lerp(
            currentLookAheadX,
            desiredLookAheadX,
            Time.deltaTime * lookAheadLerpSpeed
        );

        float targetX = target.position.x + offset.x + currentLookAheadX;
        float targetZ = target.position.z + offset.z;

        float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref velocityX, smoothTimeX);
        float newZ = Mathf.SmoothDamp(transform.position.z, targetZ, ref velocityZ, smoothTimeZ);

        transform.position = new Vector3(newX, fixedY, newZ);

        lastTargetPosition = target.position;
    }
}
