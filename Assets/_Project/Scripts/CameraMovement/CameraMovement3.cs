using UnityEngine;

public class SimpleStableCamera : MonoBehaviour
{
    public Transform target;

    [Header("Offset")]
    public Vector3 offset = new Vector3(0f, 8f, -8f);

    [Header("Smoothing")]
    public float smoothTimeX = 0.15f;
    public float smoothTimeZ = 0.15f;

    private float velocityX;
    private float velocityZ;
    private float fixedY;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("SimpleStableCamera: Kein Target gesetzt.");
            return;
        }

        fixedY = offset.y;

        transform.position = new Vector3(
            target.position.x + offset.x,
            fixedY,
            target.position.z + offset.z
        );
    }

    void LateUpdate()
    {
        if (target == null) return;

        float targetX = target.position.x + offset.x;
        float targetZ = target.position.z + offset.z;

        float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref velocityX, smoothTimeX);
        float newZ = Mathf.SmoothDamp(transform.position.z, targetZ, ref velocityZ, smoothTimeZ);

        transform.position = new Vector3(newX, fixedY, newZ);
    }
}