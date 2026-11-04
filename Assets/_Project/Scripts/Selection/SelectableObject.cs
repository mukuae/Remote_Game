using UnityEngine;

// Attach this to any object that LineSelector should be able to select.
// (Not called "Selectable" to avoid clashing with UnityEngine.UI.Selectable.)
public class SelectableObject : MonoBehaviour
{
    [Header("Optional highlight")]
    public Renderer targetRenderer;
    public Color highlightColor = Color.yellow;

    private Color originalColor;
    private bool hasRenderer;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        hasRenderer = targetRenderer != null;
        if (hasRenderer)
            originalColor = targetRenderer.material.color;
    }

    public void OnSelected()
    {
        if (hasRenderer)
            targetRenderer.material.color = highlightColor;
    }

    public void OnDeselected()
    {
        if (hasRenderer)
            targetRenderer.material.color = originalColor;
    }
}
