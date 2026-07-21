using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Pendulum_Movement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Animator playing your pendulum swing animation clip. Auto-filled if left empty.")]
    public Animator animator;

    [Header("Speed Settings")]
    [Tooltip("Speed the pendulum starts at.")]
    public float startSpeed = 1f;

    [Tooltip("How much each scroll 'tick' changes the speed.")]
    public float scrollSensitivity = 0.3f;

    [Tooltip("Slowest allowed speed. Can't go to 0 - the pendulum will never fully stop.")]
    public float minSpeed = 0.2f;

    [Tooltip("Fastest allowed speed.")]
    public float maxSpeed = 3f;

    [Tooltip("Flip this if scrolling feels backwards for you.")]
    public bool invertScroll = false;

    // Hard floor so the pendulum can never be scrolled down to a full stop,
    // even if minSpeed is accidentally set to 0 (or less) in the Inspector.
    private const float absoluteMinSpeed = 0.05f;

    private float currentSpeed;

    // Keeps minSpeed itself from being set to 0 or below in the Inspector
    void OnValidate()
    {
        minSpeed = Mathf.Max(minSpeed, absoluteMinSpeed);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        currentSpeed = Mathf.Max(startSpeed, minSpeed);
        animator.speed = currentSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f)
            return;

        if (invertScroll)
            scroll = -scroll;

        currentSpeed += scroll * scrollSensitivity;
        currentSpeed = Mathf.Clamp(currentSpeed, Mathf.Max(minSpeed, absoluteMinSpeed), maxSpeed);

        animator.speed = currentSpeed;
    }
}