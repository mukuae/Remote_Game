using UnityEngine;

// Moves a platform back and forth between two points, or spins it.
// Respects TimeControllable, so the platform stops while frozen.
[DefaultExecutionOrder(-50)] // move before PlayerController reads the platform's movement
public class PlatformMover : MonoBehaviour
{
    public enum MoveMode
    {
        PingPong,
        Rotate
    }

    public MoveMode mode = MoveMode.PingPong;

    [Header("Ping Pong")]
    [Tooltip("End point, relative to the start position (world space).")]
    public Vector3 moveOffset = new Vector3(5f, 0f, 0f);

    [Tooltip("Seconds for one trip from start to end.")]
    [Min(0.05f)] public float travelTime = 3f;

    [Tooltip("Seconds to wait at each end.")]
    [Min(0f)] public float pauseAtEnds = 0.3f;

    [Tooltip("Where in its cycle the platform starts (0-1). Give neighbouring platforms different values.")]
    [Range(0f, 1f)] public float startPhase = 0f;

    [Tooltip("Ease in/out at the ends instead of constant speed.")]
    public bool smooth = true;

    [Header("Rotate")]
    public Vector3 rotationAxis = Vector3.up;
    public float degreesPerSecond = 40f;

    private TimeControllable timeControllable;
    private Vector3 startPosition;
    private float cycleTime;

    private float CycleLength => 2f * (travelTime + pauseAtEnds);

    private void Awake()
    {
        timeControllable = GetComponent<TimeControllable>();
        startPosition = transform.position;
        cycleTime = startPhase * CycleLength;

        if (mode == MoveMode.PingPong)
            transform.position = startPosition + moveOffset * EvaluatePingPong(cycleTime);
    }

    private void Update()
    {
        float deltaTime = timeControllable != null ? timeControllable.DeltaTime : Time.deltaTime;
        if (deltaTime <= 0f) return;

        if (mode == MoveMode.Rotate)
        {
            transform.Rotate(rotationAxis, degreesPerSecond * deltaTime, Space.World);
            return;
        }

        cycleTime = (cycleTime + deltaTime) % CycleLength;
        transform.position = startPosition + moveOffset * EvaluatePingPong(cycleTime);
    }

    // Cycle: wait at start -> travel out -> wait at end -> travel back. Returns 0..1 along the path.
    private float EvaluatePingPong(float t)
    {
        if (t < pauseAtEnds) return 0f;
        t -= pauseAtEnds;

        if (t < travelTime) return Ease(t / travelTime);
        t -= travelTime;

        if (t < pauseAtEnds) return 1f;
        t -= pauseAtEnds;

        return 1f - Ease(t / travelTime);
    }

    private float Ease(float x)
    {
        return smooth ? Mathf.SmoothStep(0f, 1f, x) : x;
    }

    private void OnDrawGizmosSelected()
    {
        if (mode != MoveMode.PingPong) return;

        Vector3 start = Application.isPlaying ? startPosition : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start, start + moveOffset);
        Gizmos.DrawWireCube(start + moveOffset, transform.lossyScale);
    }
}
