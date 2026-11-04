using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Time-Slow targeting cone:
/// - Press <see cref="activationKey"/> (default F) to toggle a yellow
///   "vision cone" on/off, drawn from <see cref="aimSource"/>'s position,
///   pointing along its facing direction.
/// - Any GameObject tagged "MovingPlatform" that enters the cone is
///   immediately highlighted AND selected — no mouse cursor involved at
///   all. It stays selected for as long as it's inside the cone, and
///   becomes deselected the moment it leaves the cone (or the ability is
///   turned off).
/// - More than one platform can be selected at the same time if more than
///   one is inside the cone.
///
/// This script does NOT change any platform's speed itself — it only
/// decides which platform(s) are currently selected, via
/// <see cref="OnPlatformSelected"/> / <see cref="OnPlatformDeselected"/>.
/// Your existing scroll-wheel speed script should check whether IT was the
/// object passed into OnPlatformSelected before reacting to scroll input
/// (see Pendulum_Movement.cs for the pattern already wired up).
///
/// NOTE: this version uses the original flat, horizontal cone (based on
/// aimSource.forward flattened onto the ground plane) — the "spawns from
/// the face and tilts with up/down look" experiment has been reverted per
/// your last message. Since you mentioned this is actually a 2D project,
/// we'll likely want to revisit how aimSource's direction is set once
/// you've settled on how facing/up-down look is represented in your
/// movement script — happy to adjust this whenever you're ready for that.
/// </summary>
public class TimeSlowAbility : MonoBehaviour
{
    public static TimeSlowAbility Instance { get; private set; }

    [Header("Activation")]
    [Tooltip("Key that toggles the ability on/off.")]
    public KeyCode activationKey = KeyCode.F;

    [Tooltip("Transform whose forward direction the cone points along. Defaults to this object.")]
    public Transform aimSource;

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

    [Header("Highlight Colour")]
    [Tooltip("Outline colour for a MovingPlatform while it's inside the cone (i.e. selected).")]
    public Color outlineColor = new Color(0.2f, 1f, 1f, 1f);

    [Range(0.001f, 0.1f)]
    public float outlineWidth = 0.02f;

    [Header("Events")]
    public Action<GameObject> OnPlatformSelected;
    public Action<GameObject> OnPlatformDeselected;

    public bool IsActive { get; private set; }

    private GameObject coneVisual;
    private MeshFilter coneMeshFilter;
    private MeshRenderer coneMeshRenderer;
    private Material coneMaterialInstance;

    private readonly HashSet<GameObject> selectedObjects = new HashSet<GameObject>();

    private GameObject[] cachedPlatforms = new GameObject[0];
    private float rescanTimer;

    private float lastBuiltLength = -1f;
    private float lastBuiltAngle = -1f;
    private int lastBuiltResolution = -1;

    private void Awake()
    {
        Instance = this;

        if (aimSource == null) aimSource = transform;

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
        DetectAndSelect();
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
            DeselectAll();
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
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = aimSource.up; // fallback if forward is straight up/down
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

    // ---------- Cone detection + selection (being in the cone IS being selected) ----------

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

    private void DetectAndSelect()
    {
        HashSet<GameObject> stillInCone = new HashSet<GameObject>();

        foreach (var obj in cachedPlatforms)
        {
            if (obj == null) continue;
            if (IsWithinCone(obj.transform.position))
            {
                stillInCone.Add(obj);
                if (!selectedObjects.Contains(obj))
                {
                    Select(obj);
                }
            }
        }

        selectedObjects.RemoveWhere(obj =>
        {
            if (obj == null) return true;
            if (!stillInCone.Contains(obj))
            {
                Deselect(obj);
                return true;
            }
            return false;
        });
    }

    private void Select(GameObject obj)
    {
        selectedObjects.Add(obj);
        Outline outline = obj.GetComponent<Outline>();
        if (outline == null) outline = obj.AddComponent<Outline>();
        outline.OutlineColor = outlineColor;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = true;

        OnPlatformSelected?.Invoke(obj);
    }

    private void Deselect(GameObject obj)
    {
        Outline outline = obj.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        OnPlatformDeselected?.Invoke(obj);
    }

    private void DeselectAll()
    {
        foreach (var obj in selectedObjects)
        {
            if (obj != null) Deselect(obj);
        }
        selectedObjects.Clear();
    }
}