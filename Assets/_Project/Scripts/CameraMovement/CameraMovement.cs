using UnityEngine;

public class SideViewDynamicCamera : MonoBehaviour
{
    public Transform target;

    [Header("Offsets")]
    public Vector3 baseOffset = new Vector3(0f, 5f, -10f);
    public Vector3 lookOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Follow")]
    public float smoothX = 0.15f;
    public float smoothY = 0.3f;
    public float smoothZ = 0.2f;

    [Header("Look Ahead")]
    public float lookAheadX = 2f;
    public float lookAheadZ = 1f;

    private Vector3 velocity;
    private Vector3 lastTargetPos;

    void Start()
    {
        if (target == null) return;

        lastTargetPos = target.position;
        transform.position = target.position + baseOffset;
        transform.LookAt(target.position + lookOffset);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 delta = target.position - lastTargetPos;

        Vector3 desiredPosition = target.position + baseOffset;
        desiredPosition.x += Mathf.Clamp(delta.x * lookAheadX * 10f, -2f, 2f);
        desiredPosition.z += Mathf.Clamp(delta.z * lookAheadZ * 10f, -1.5f, 1.5f);

        float newX = Mathf.SmoothDamp(transform.position.x, desiredPosition.x, ref velocity.x, smoothX);
        float newY = Mathf.SmoothDamp(transform.position.y, desiredPosition.y, ref velocity.y, smoothY);
        float newZ = Mathf.SmoothDamp(transform.position.z, desiredPosition.z, ref velocity.z, smoothZ);

        transform.position = new Vector3(newX, newY, newZ);

        Vector3 lookTarget = target.position + lookOffset;
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 4f);

        lastTargetPos = target.position;
    }
}