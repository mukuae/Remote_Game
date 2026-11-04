using UnityEngine;
using UnityEngine.InputSystem;

// Toggles an Animator between a normal and a changed playback speed when a button is pressed.
public class AnimationSpeedChanger : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private InputActionReference speedButton;

    public float normalSpeed = 1f;
    public float changedSpeed = 2f;

    private bool isChanged;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (speedButton != null)
            speedButton.action.Enable();
    }

    private void OnDisable()
    {
        if (speedButton != null)
            speedButton.action.Disable();
    }

    private void Update()
    {
        if (PauseMenu.GameIsPaused || animator == null)
            return;

        if (speedButton != null && speedButton.action.WasPressedThisFrame())
        {
            isChanged = !isChanged;
            animator.speed = isChanged ? changedSpeed : normalSpeed;
        }
    }
}
