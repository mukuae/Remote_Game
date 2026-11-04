using UnityEngine;

/// <summary>
/// Simple, render-pipeline-agnostic outline effect using the classic
/// "inverted hull" trick: duplicates the object's mesh into a child,
/// renders only its back faces, scales it slightly outward, and draws
/// it in a flat colour so it peeks out from behind the original mesh.
///
/// Add this to any GameObject with a MeshFilter + MeshRenderer. It starts
/// disabled — TimeSlowAbility enables/disables it and sets the colour.
/// You can also use it standalone: outline.OutlineColor = ...; outline.enabled = true;
///
/// For reliable back-face-only rendering, pair this with OutlineUnlit.shader
/// (included alongside this script) rather than a generic Unlit shader —
/// some default shaders don't expose a settable Cull property.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Outline : MonoBehaviour
{
    public Color OutlineColor = Color.yellow;
    [Range(0.001f, 0.1f)] public float OutlineWidth = 0.02f;

    private GameObject outlineObject;
    private MeshRenderer outlineRenderer;
    private Material outlineMaterial;

    private void Awake()
    {
        CreateOutlineObject();
        enabled = false;
    }

    private void OnEnable()
    {
        if (outlineObject != null) outlineObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (outlineObject != null) outlineObject.SetActive(false);
    }

    private void Update()
    {
        if (outlineMaterial != null && outlineMaterial.color != OutlineColor)
            outlineMaterial.color = OutlineColor;

        if (outlineObject != null)
            outlineObject.transform.localScale = Vector3.one * (1f + OutlineWidth);
    }

    private void CreateOutlineObject()
    {
        MeshFilter sourceFilter = GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null) return;

        outlineObject = new GameObject("Outline");
        outlineObject.transform.SetParent(transform, false);
        outlineObject.transform.localScale = Vector3.one * (1f + OutlineWidth);

        MeshFilter mf = outlineObject.AddComponent<MeshFilter>();
        mf.sharedMesh = sourceFilter.sharedMesh;

        outlineRenderer = outlineObject.AddComponent<MeshRenderer>();

        // Prefer the custom outline shader (guarantees Cull Front works).
        // Falls back to a generic unlit shader if it isn't in the project.
        Shader shader = Shader.Find("Custom/OutlineUnlit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
        outlineMaterial = new Material(shader) { color = OutlineColor };
        outlineRenderer.material = outlineMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        outlineObject.SetActive(false);
    }
}