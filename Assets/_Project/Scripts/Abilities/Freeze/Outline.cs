using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple outline effect using the "inverted hull" trick: every mesh on this object and its
/// children gets a duplicate that renders only back faces, slightly scaled up, in a flat colour,
/// so it peeks out from behind the original mesh.
///
/// Starts disabled. FreezeAbility enables/disables it on the current target; it can also be used
/// standalone: outline.OutlineColor = ...; outline.enabled = true;
///
/// Uses Custom/OutlineUnlit (Assets/_Project/Shaders), which is listed under Always Included
/// Shaders so Shader.Find also works in builds.
/// </summary>
public class Outline : MonoBehaviour
{
    public Color OutlineColor = Color.yellow;
    [Range(0.001f, 0.1f)] public float OutlineWidth = 0.02f;

    private readonly List<GameObject> outlineObjects = new List<GameObject>();
    private Material outlineMaterial;

    private void Awake()
    {
        CreateOutlineObjects();
        enabled = false;
    }

    private void OnEnable()
    {
        SetOutlinesActive(true);
    }

    private void OnDisable()
    {
        SetOutlinesActive(false);
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }

    private void Update()
    {
        if (outlineMaterial != null && outlineMaterial.color != OutlineColor)
            outlineMaterial.color = OutlineColor;

        Vector3 scale = Vector3.one * (1f + OutlineWidth);
        foreach (GameObject outlineObject in outlineObjects)
            outlineObject.transform.localScale = scale;
    }

    private void SetOutlinesActive(bool active)
    {
        foreach (GameObject outlineObject in outlineObjects)
        {
            if (outlineObject != null) outlineObject.SetActive(active);
        }
    }

    private void CreateOutlineObjects()
    {
        // Meshes often sit on child objects (imported models), so look at the whole hierarchy.
        MeshFilter[] sourceFilters = GetComponentsInChildren<MeshFilter>();
        if (sourceFilters.Length == 0)
        {
            Debug.LogWarning($"Outline on '{name}': no MeshFilter found on this object or its children.", this);
            return;
        }

        Shader shader = Shader.Find("Custom/OutlineUnlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("Outline: shader 'Custom/OutlineUnlit' not found.", this);
            return;
        }

        outlineMaterial = new Material(shader) { color = OutlineColor };

        foreach (MeshFilter sourceFilter in sourceFilters)
        {
            if (sourceFilter.sharedMesh == null) continue;

            GameObject outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(sourceFilter.transform, false);
            outlineObject.transform.localScale = Vector3.one * (1f + OutlineWidth);

            MeshFilter mf = outlineObject.AddComponent<MeshFilter>();
            mf.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = outlineMaterial;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;

            outlineObject.SetActive(false);
            outlineObjects.Add(outlineObject);
        }
    }
}
