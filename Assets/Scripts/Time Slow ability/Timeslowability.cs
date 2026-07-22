using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Time-Slow targeting cone:
/// - Press <see cref="activationKey"/> (default F) to toggle a yellow
///   "vision cone" on/off, drawn on the ground plane in front of the
///   player, in the direction <see cref="aimSource"/> is facing.
/// - Every GameObject tagged "MovingPlatform" that is inside that cone
///   gets an outline highlight (<see cref="inConeOutlineColor"/>).
/// - If the mouse cursor's world position is ALSO inside the cone, and
///   it's hovering one of those highlighted platforms, that platform
///   becomes the "Selected" platform (<see cref="selectedOutlineColor"/>)
///   and is exposed via <see cref="SelectedObject"/> / <see cref="Instance"/> /
///   <see cref="OnPlatformSelected"/>.
///
/// This script does NOT change any platform's speed itself — it only
/// decides which platform is currently selected. Your existing
/// scroll-wheel speed script should check whether it is the selected
/// object before reacting to scroll input, e.g.:
///
///   void Update()
///   {
///       if (TimeSlowAbility.Instance == null ||
///           TimeSlowAbility.Instance.SelectedObject != gameObject) return;
///
///       float scroll = Input.mouseScrollDelta.y;
///       // ... your existing speed-change code here ...
///   }
/// </summary>
public class TimeSlowAbility : MonoBehaviour
{
    public static TimeSlowAbility Instance { get; private set; }

    [Header("Activation")]
    [Tooltip("Key that toggles the ability on/off.")]
    public KeyCode activationKey = KeyCode.F;

    [Tooltip("Transform whose forward direction the cone points along. Defaults to this object.")]
    public Transform aimSource;

    [Tooltip("Camera used to raycast the mouse cursor into the world. Defaults to Camera.main.")]
    public Camera playerCamera;

    [Header("Performance")]
    [Tooltip("How often (seconds) we re-scan the scene for objects tagged MovingPlatform. 0 = every frame.")]
    public float rescanInterval = 0.5f;

    [Header("Cone Shape")]
    [Tooltip("How far the vision cone reaches.")]
    public float coneLength = 10f;

    [Range(1f, 180f)]
    [Tooltip("Full angle (in degrees) of the vision cone.")]
    public float coneAngle = 45f;

    [Range(3, 64)]
    [Tooltip("Number of segments used to build the cone mesh (higher = smoother edge).")]
    public int coneResolution = 24;

    [Header("Cone Appearance")]
    public Color coneColor = new Color(1f, 0.92f, 0.1f, 0.28f);

    [Header("Highlight Colours")]
    [Tooltip("Outline colour for a MovingPlatform that is simply inside the cone.")]
    public Color inConeOutlineColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Outline colour for a MovingPlatform that is inside the cone AND under the mouse cursor (selected).")]
    public Color selectedOutlineColor = new Color(0.2f, 1f, 1f, 1f);

    [Range(0.001f, 0.1f)]
    public float outlineWidth = 0.02f;

    [Header("Events")]
    public Action<GameObject> OnPlatformSelected;
    public Action<GameObject> OnPlatformDeselected;

    public bool IsActive { get; private set; }
    public GameObject SelectedObject { get; private set; }

    private GameObject coneVisual;
    private MeshFilter coneMeshFilter;
    private MeshRenderer coneMeshRenderer;
    private Material coneMaterialInstance;

    private readonly HashSet<GameObject> highlightedObjects = new HashSet<GameObject>();

    private GameObject[] cachedPlatforms = new GameObject[0];
    private float rescanTimer;

    private float lastBuiltLength = -1f;
    private float lastBuiltAngle = -1f;
    private int lastBuiltResolution = -1;

    private void Awake()
    {
        Instance = this;

        if (aimSource == null) aimSource = transform;
        if (playerCamera == null) playerCamera = Camera.main;

        BuildConeVisual();
        coneVisual.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(activationKey))
        {
            Toggle();
        }

        if (!IsActive) return;

        RescanPlatformsIfNeeded();
        UpdateConeTransform();
        UpdateConeMesh();
        DetectAndHighlight();
        HandleMouseSelection();
    }

    public void Toggle()
    {
        IsActive = !IsActive;
        coneVisual.SetActive(IsActive);

        if (IsActive)
        {
            RescanPlatformsIfNeeded(true);
        }
        else
        {
            ClearAllHighlights();
            SetSelected(null);
        }
    }

    // ---------- Cone mesh ----------

    private void BuildConeVisual()
    {
        coneVisual = new GameObject("VisionCone");
        coneVisual.transform.SetParent(transform, false);

        coneMeshFilter = coneVisual.AddComponent<MeshFilter>();
        coneMeshRenderer = coneVisual.AddComponent<MeshRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Transparent")
                         ?? Shader.Find("Sprites/Default");
        coneMaterialInstance = new Material(shader);
        SetupTransparentMaterial(coneMaterialInstance);
        coneMeshRenderer.material = coneMaterialInstance;
        coneMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coneMeshRenderer.receiveShadows = false;

        RebuildConeMesh();
    }

    private static void SetupTransparentMaterial(Material mat)
    {
        // Covers both Built-in Standard-style shaders and URP Unlit's Surface Type.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1); // URP: Transparent
        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
    }

    private void UpdateConeMesh()
    {
        if (Mathf.Approximately(lastBuiltLength, coneLength) &&
            Mathf.Approximately(lastBuiltAngle, coneAngle) &&
            lastBuiltResolution == coneResolution)
        {
            if (coneMaterialInstance.color != coneColor)
                coneMaterialInstance.color = coneColor;
            return;
        }

        RebuildConeMesh();
    }

    private void RebuildConeMesh()
    {
        lastBuiltLength = coneLength;
        lastBuiltAngle = coneAngle;
        lastBuiltResolution = coneResolution;

        Mesh mesh = new Mesh { name = "VisionConeMesh" };

        int segments = coneResolution;
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero; // apex at the player's position (local space)

        float halfAngle = coneAngle * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(angle) * coneLength, 0f, Mathf.Cos(angle) * coneLength);
        }

        for (int i = 0; i < segments; i++)
        {
            int tri = i * 3;
            triangles[tri] = 0;
            triangles[tri + 1] = i + 1;
            triangles[tri + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        coneMeshFilter.mesh = mesh;
        coneMaterialInstance.color = coneColor;
    }

    private void UpdateConeTransform()
    {
        // Cone sits flat on the XZ plane, pointing along aimSource's forward direction.
        coneVisual.transform.position = aimSource.position;
        Vector3 flatForward = aimSource.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = aimSource.up; // fallback if looking straight up/down
        coneVisual.transform.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
    }

    // ---------- Platform scanning ----------

    private void RescanPlatformsIfNeeded(bool force = false)
    {
        rescanTimer -= Time.deltaTime;
        if (!force && rescanTimer > 0f) return;
        rescanTimer = rescanInterval;
        cachedPlatforms = GameObject.FindGameObjectsWithTag("MovingPlatform");
    }

    // ---------- Cone detection + highlight ----------

    private bool IsWithinCone(Vector3 worldPosition)
    {
        Vector3 toTarget = worldPosition - aimSource.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance > coneLength || distance < 0.0001f) return false;

        Vector3 flatForward = aimSource.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) return false;

        float angle = Vector3.Angle(flatForward.normalized, toTarget.normalized);
        return angle <= coneAngle * 0.5f;
    }

    private void DetectAndHighlight()
    {
        HashSet<GameObject> stillInCone = new HashSet<GameObject>();

        foreach (var obj in cachedPlatforms)
        {
            if (obj == null) continue;
            if (IsWithinCone(obj.transform.position))
            {
                stillInCone.Add(obj);
                if (!highlightedObjects.Contains(obj))
                {
                    Highlight(obj, inConeOutlineColor);
                    highlightedObjects.Add(obj);
                }
            }
        }

        highlightedObjects.RemoveWhere(obj =>
        {
            if (obj == null) return true;
            if (!stillInCone.Contains(obj))
            {
                if (obj != SelectedObject) RemoveHighlight(obj);
                return true;
            }
            return false;
        });
    }

    private void ClearAllHighlights()
    {
        foreach (var obj in highlightedObjects)
        {
            if (obj != null) RemoveHighlight(obj);
        }
        highlightedObjects.Clear();
    }

    private void Highlight(GameObject obj, Color color)
    {
        Outline outline = obj.GetComponent<Outline>();
        if (outline == null) outline = obj.AddComponent<Outline>();
        outline.OutlineColor = color;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = true;
    }

    private void RemoveHighlight(GameObject obj)
    {
        Outline outline = obj.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    // ---------- Mouse selection ----------

    private void HandleMouseSelection()
    {
        if (playerCamera == null) { SetSelected(null); return; }

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            GameObject hitObj = hit.collider.gameObject;
            // hit.point is where the cursor is actually pointing in the world —
            // that's what has to be inside the cone, not the object's pivot.
            if (hitObj.CompareTag("MovingPlatform") && IsWithinCone(hit.point))
            {
                SetSelected(hitObj);
                return;
            }
        }

        SetSelected(null);
    }

    private void SetSelected(GameObject obj)
    {
        if (SelectedObject == obj) return;

        if (SelectedObject != null)
        {
            if (highlightedObjects.Contains(SelectedObject))
                Highlight(SelectedObject, inConeOutlineColor);
            else
                RemoveHighlight(SelectedObject);

            OnPlatformDeselected?.Invoke(SelectedObject);
        }

        SelectedObject = obj;

        if (SelectedObject != null)
        {
            Highlight(SelectedObject, selectedOutlineColor);
            OnPlatformSelected?.Invoke(SelectedObject);
        }
    }
}
