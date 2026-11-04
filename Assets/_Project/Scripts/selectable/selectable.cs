using UnityEngine;

// Attach this to any object that should be selectable (e.g. the pendulum).
public class Selectable : MonoBehaviour
{
    [Header("Optional highlight")]
    public Renderer targetRenderer;
    public Color highlightColor = Color.yellow;

    private Color _originalColor;
    private bool _hasRenderer;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        _hasRenderer = targetRenderer != null;
        if (_hasRenderer)
            _originalColor = targetRenderer.material.color;
    }

    public void OnSelected()
    {
        if (_hasRenderer)
            targetRenderer.material.color = highlightColor;
    }

    public void OnDeselected()
    {
        if (_hasRenderer)
            targetRenderer.material.color = _originalColor;
    }
}