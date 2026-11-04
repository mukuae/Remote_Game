using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider))] // <-- added: needed so TimeSlowAbility's mouse raycast can hit this object
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

    // --- added: tracks whether this platform is currently selected via the F-key cone ---
    private bool isSelected;

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

        // --- added: subscribe to the player's TimeSlowAbility selection events.
        // Done in Start (not Awake/OnEnable) so we're guaranteed TimeSlowAbility.Instance
        // already exists on the player by the time this runs.
        if (TimeSlowAbility.Instance != null)
        {
            TimeSlowAbility.Instance.OnPlatformSelected += HandleSelected;
            TimeSlowAbility.Instance.OnPlatformDeselected += HandleDeselected;
        }
        else
        {
            Debug.LogWarning($"{name}: No TimeSlowAbility found in the scene. " +
                              "Make sure TimeSlowAbility is on the player and this object is tagged MovingPlatform.");
        }
    }

    // --- added: unsubscribe to avoid errors/leaks if this platform is disabled or destroyed ---
    void OnDisable()
    {
        if (TimeSlowAbility.Instance != null)
        {
            TimeSlowAbility.Instance.OnPlatformSelected -= HandleSelected;
            TimeSlowAbility.Instance.OnPlatformDeselected -= HandleDeselected;
        }
    }

    // --- added ---
    private void HandleSelected(GameObject obj)
    {
        if (obj == gameObject) isSelected = true;
    }

    // --- added ---
    private void HandleDeselected(GameObject obj)
    {
        if (obj == gameObject) isSelected = false;
    }

    // Update is called once per frame
    void Update()
    {
        // --- added: only react to scroll while this pendulum is the selected one ---
        if (!isSelected)
            return;

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