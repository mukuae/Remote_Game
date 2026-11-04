using UnityEngine;
using UnityEngine.InputSystem;

// Swinging pendulum platform. While it is selected by the TimeSlowAbility cone,
// the mouse wheel changes its swing speed.
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider))]
public class PendulumMovement : MonoBehaviour
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
    private bool isSelected;
    private bool isSubscribed;

    private void OnValidate()
    {
        minSpeed = Mathf.Max(minSpeed, absoluteMinSpeed);
    }

    private void OnEnable()
    {
        // Covers re-enabling. On scene start TimeSlowAbility may not exist yet, so Start retries.
        Subscribe();
    }

    private void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        currentSpeed = Mathf.Max(startSpeed, minSpeed);
        animator.speed = currentSpeed;

        Subscribe();
        if (!isSubscribed)
        {
            Debug.LogWarning($"{name}: No TimeSlowAbility found in the scene. " +
                             "Make sure TimeSlowAbility is on the player and this object is tagged MovingPlatform.", this);
        }
    }

    private void OnDisable()
    {
        if (isSubscribed && TimeSlowAbility.Instance != null)
        {
            TimeSlowAbility.Instance.OnPlatformSelected -= HandleSelected;
            TimeSlowAbility.Instance.OnPlatformDeselected -= HandleDeselected;
        }

        isSubscribed = false;
        isSelected = false;
    }

    private void Subscribe()
    {
        if (isSubscribed || TimeSlowAbility.Instance == null) return;

        TimeSlowAbility.Instance.OnPlatformSelected += HandleSelected;
        TimeSlowAbility.Instance.OnPlatformDeselected += HandleDeselected;
        isSubscribed = true;
    }

    private void HandleSelected(GameObject obj)
    {
        if (obj == gameObject) isSelected = true;
    }

    private void HandleDeselected(GameObject obj)
    {
        if (obj == gameObject) isSelected = false;
    }

    private void Update()
    {
        // Only react to scroll while this pendulum is selected.
        if (!isSelected || PauseMenu.GameIsPaused || Mouse.current == null)
            return;

        float rawScroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(rawScroll) < 0.01f)
            return;

        // One step per scroll event; raw scroll units differ between platforms.
        float scroll = Mathf.Sign(rawScroll);
        if (invertScroll)
            scroll = -scroll;

        currentSpeed += scroll * scrollSensitivity;
        currentSpeed = Mathf.Clamp(currentSpeed, Mathf.Max(minSpeed, absoluteMinSpeed), maxSpeed);

        animator.speed = currentSpeed;
    }
}
