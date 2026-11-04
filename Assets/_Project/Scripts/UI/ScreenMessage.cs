using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Short centered on-screen message, e.g. "Checkpoint". Creates its own UI the first time it's used.
// Usage: ScreenMessage.Show("Checkpoint", 1.5f);
public class ScreenMessage : MonoBehaviour
{
    private static ScreenMessage instance;

    private TextMeshProUGUI text;
    private float hideTime;

    public static void Show(string message, float duration)
    {
        if (string.IsNullOrEmpty(message) || duration <= 0f) return;

        if (instance == null)
            instance = new GameObject("ScreenMessage").AddComponent<ScreenMessage>();

        instance.text.text = message;
        instance.text.enabled = true;
        instance.hideTime = Time.unscaledTime + duration;
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.7f);
        rect.sizeDelta = new Vector2(1200f, 120f);

        text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 56f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;
        text.enabled = false;
    }

    private void Update()
    {
        if (text.enabled && Time.unscaledTime >= hideTime)
            text.enabled = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
