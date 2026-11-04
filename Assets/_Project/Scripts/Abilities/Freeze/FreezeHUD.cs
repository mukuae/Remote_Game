using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Shows the freeze charges, a tutorial prompt, and the controls while choosing a target.
// Builds its own UI at runtime, so it only needs to be placed in the scene. Prompts show the
// buttons of the device that was used last (gamepad or keyboard/mouse).
public class FreezeHUD : MonoBehaviour
{
    [Tooltip("Defaults to the FreezeAbility in the scene.")]
    [SerializeField] private FreezeAbility ability;

    private TextMeshProUGUI chargesText;
    private TextMeshProUGUI promptText;
    private bool usingGamepad;
    private bool promptDismissed;

    private void Awake()
    {
        if (ability == null)
            ability = FindFirstObjectByType<FreezeAbility>();

        BuildUI();
    }

    private void OnEnable()
    {
        InputSystem.onActionChange += OnActionChange;
        if (ability != null) ability.Froze += OnFroze;
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= OnActionChange;
        if (ability != null) ability.Froze -= OnFroze;
    }

    private void Update()
    {
        if (ability == null)
        {
            chargesText.text = string.Empty;
            promptText.enabled = false;
            return;
        }

        FreezeSettings settings = FreezeSettings.Instance;

        string charges = $"Freeze charges: {ability.Charges}/{settings.maxCharges}";
        if (ability.Charges < settings.maxCharges)
            charges += $"   (recharging {Mathf.RoundToInt(ability.RechargeProgress * 100f)}%)";
        chargesText.text = charges;

        string prompt = null;
        if (ability.IsTargeting)
        {
            // Always shown while choosing, so the controls are never a mystery.
            prompt = $"{Binding("Player/Freeze")} Freeze      {Binding("Player/CycleTarget")} Next target      {Binding("Player/CancelTarget")} Cancel";
            if (!ability.IsTargetInRange)
                prompt = "<color=#AAAAAA>Target out of range</color>\n" + prompt;
            if (usingGamepad)
                prompt += $"\n{Binding("Player/AimTarget")} Flick to pick a direction";
        }
        else if (settings.showTutorialPrompt && !promptDismissed && ability.HasTargetInRange && ability.Charges > 0)
        {
            prompt = $"{Binding("Player/Freeze")} Choose a platform to freeze";
        }

        promptText.enabled = prompt != null;
        if (prompt != null) promptText.text = prompt;
    }

    private void OnFroze(TimeControllable target)
    {
        if (FreezeSettings.Instance.hidePromptAfterFirstFreeze)
            promptDismissed = true;
    }

    private void OnActionChange(object actionOrMap, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed) return;
        if (actionOrMap is InputAction action && action.activeControl != null)
            usingGamepad = action.activeControl.device is Gamepad;
    }

    private string Binding(string actionName)
    {
        InputAction action = InputSystem.actions != null ? InputSystem.actions.FindAction(actionName) : null;
        if (action == null) return "[?]";

        string group = usingGamepad ? "Gamepad" : "Keyboard&Mouse";
        string display = action.GetBindingDisplayString(InputBinding.MaskByGroup(group));
        return $"[{display}]";
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("FreezeHUD Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        chargesText = CreateText(canvas.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), TextAlignmentOptions.BottomLeft, 32f);
        promptText = CreateText(canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 130f), TextAlignmentOptions.Bottom, 38f);
        promptText.enabled = false;
    }

    private static TextMeshProUGUI CreateText(Transform parent, Vector2 anchor, Vector2 position, TextAlignmentOptions alignment, float size)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(1600f, 120f);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        return text;
    }
}
