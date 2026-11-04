using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Time-slow targeting cone.
/// Press <see cref="toggleKey"/> (default F) to show a vision cone from <see cref="aimSource"/>.
/// Every GameObject tagged "MovingPlatform" inside the cone is outlined and selected; it is
/// deselected when it leaves the cone or when the ability is turned off. Several platforms can
/// be selected at once.
///
/// This script only decides what is selected. Platforms react through
/// <see cref="OnPlatformSelected"/> / <see cref="OnPlatformDeselected"/>
/// (see PendulumMovement for an example).
/// </summary>
public class TimeSlowAbility : MonoBehaviour
{
    public enum ConePlane
    {
        Horizontal, // cone lies on the ground plane (XZ) - free 3D movement
        SideView    // cone lies on the XY plane - side-scrolling (PlayerController 2D mode)
    }

    public static TimeSlowAbility Instance { get; private set; }

    [Header("Activation")]
    [Tooltip("Key that toggles the ability on/off.")]
    public Key toggleKey = Key.F;

    [Tooltip("Transform whose forward direction the cone points along. Defaults to this object. " +
             "Use the head bone to make the cone follow look-up/look-down animations.")]
    public Transform aimSource;

    [Tooltip("Horizontal = ground plane (3D movement). SideView = XY plane, so platforms above and below the player count too.")]
    public ConePlane conePlane = ConePlane.Horizontal;

    [Header("Performance")]
    [Tooltip("How often (seconds) the scene is re-scanned for objects tagged MovingPlatform. 0 = every frame.")]
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
    private Mesh coneMesh;
    private Material coneMaterialInstance;

    private readonly HashSet<GameObject> selectedObjects = new HashSet<GameObject>();
    private readonly HashSet<GameObject> objectsInCone = new HashSet<GameObject>();
    private Predicate<GameObject> deselectIfOutsideCone;

    private GameObject[] cachedPlatforms = new GameObject[0];
    private float rescanTimer;

    private float lastBuiltLength = -1f;
    private float lastBuiltAngle = -1f;
    private int lastBuiltResolution = -1;

    private Vector3 PlaneNormal => conePlane == ConePlane.SideView ? Vector3.forward : Vector3.up;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("More than one TimeSlowAbility in the scene; platforms only listen to the newest one.", this);

        Instance = this;

        if (aimSource == null) aimSource = transform;

        deselectIfOutsideCone = DeselectIfOutsideCone;

        BuildConeVisual();
        coneVisual.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (coneMesh != null) Destroy(coneMesh);
        if (coneMaterialInstance != null) Destroy(coneMaterialInstance);
    }

    private void Update()
    {
        if (PauseMenu.GameIsPaused) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
            Toggle();

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
            RescanPlatformsIfNeeded(true);
        else
            DeselectAll();
    }

    // ---------- Cone mesh ----------

    private void BuildConeVisual()
    {
        coneVisual = new GameObject("VisionCone");
        coneVisual.transform.SetParent(transform, false);

        coneMeshFilter = coneVisual.AddComponent<MeshFilter>();
        MeshRenderer coneMeshRenderer = coneVisual.AddComponent<MeshRenderer>();
        coneMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coneMeshRenderer.receiveShadows = false;

        // URP/Unlit is listed under Always Included Shaders (Project Settings > Graphics),
        // so Shader.Find also works in builds.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            coneMaterialInstance = new Material(shader);
            SetupTransparentMaterial(coneMaterialInstance);
            coneMeshRenderer.sharedMaterial = coneMaterialInstance;
        }
        else
        {
            Debug.LogError("TimeSlowAbility: no unlit shader found for the vision cone.", this);
        }

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
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
    }

    private void UpdateConeMesh()
    {
        if (Mathf.Approximately(lastBuiltLength, coneLength) &&
            Mathf.Approximately(lastBuiltAngle, coneAngle) &&
            lastBuiltResolution == coneResolution)
        {
            if (coneMaterialInstance != null && coneMaterialInstance.color != coneColor)
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

        if (coneMesh == null)
        {
            coneMesh = new Mesh { name = "VisionConeMesh" };
            coneMeshFilter.sharedMesh = coneMesh;
        }

        int segments = coneResolution;
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero; // apex at the aim source (local space)

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

        coneMesh.Clear();
        coneMesh.vertices = vertices;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateNormals();
        coneMesh.RecalculateBounds();

        if (coneMaterialInstance != null)
            coneMaterialInstance.color = coneColor;
    }

    // The cone direction is aimSource.forward flattened onto the cone plane.
    private bool TryGetConeDirection(out Vector3 direction)
    {
        direction = Vector3.ProjectOnPlane(aimSource.forward, PlaneNormal);

        // Fallback if forward points straight along the plane normal.
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.ProjectOnPlane(aimSource.up, PlaneNormal);

        if (direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();
        return true;
    }

    private void UpdateConeTransform()
    {
        if (!TryGetConeDirection(out Vector3 direction)) return;

        // The mesh is built on its local XZ plane, so its local up must be the plane normal.
        coneVisual.transform.SetPositionAndRotation(aimSource.position, Quaternion.LookRotation(direction, PlaneNormal));
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

    private bool IsWithinCone(Vector3 worldPosition, Vector3 coneDirection)
    {
        Vector3 toTarget = Vector3.ProjectOnPlane(worldPosition - aimSource.position, PlaneNormal);
        float distance = toTarget.magnitude;
        if (distance > coneLength || distance < 0.0001f) return false;

        return Vector3.Angle(coneDirection, toTarget) <= coneAngle * 0.5f;
    }

    private void DetectAndSelect()
    {
        objectsInCone.Clear();

        if (TryGetConeDirection(out Vector3 coneDirection))
        {
            foreach (GameObject obj in cachedPlatforms)
            {
                if (obj == null || !IsWithinCone(obj.transform.position, coneDirection)) continue;

                objectsInCone.Add(obj);
                if (!selectedObjects.Contains(obj))
                    Select(obj);
            }
        }

        selectedObjects.RemoveWhere(deselectIfOutsideCone);
    }

    private bool DeselectIfOutsideCone(GameObject obj)
    {
        if (obj == null) return true;
        if (objectsInCone.Contains(obj)) return false;

        Deselect(obj);
        return true;
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
        foreach (GameObject obj in selectedObjects)
        {
            if (obj != null) Deselect(obj);
        }
        selectedObjects.Clear();
    }
}
