using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Makes an object freezable by FreezeAbility. Works with:
// - Animators (animation-driven platforms): playback is paused
// - Rigidbodies: the body is held in place; hits while frozen store momentum that launches it on unfreeze
// - Scripted movers: read DeltaTime instead of Time.deltaTime (see PlatformMover)
[DisallowMultipleComponent]
public class TimeControllable : MonoBehaviour
{
    private static readonly List<TimeControllable> all = new List<TimeControllable>();
    public static IReadOnlyList<TimeControllable> All => all;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material arrowMaterial;

    [Tooltip("Turn off to make this object immune to freezing (e.g. until a switch is pressed).")]
    public bool canBeFrozen = true;

    public UnityEvent onFrozen;
    public UnityEvent onUnfrozen;

    public bool IsFrozen { get; private set; }
    public float RemainingFreezeTime { get; private set; }
    public Vector3 StoredMomentum { get; private set; }

    // 0 while frozen, 1 otherwise. Scripted movers multiply their movement by this.
    public float TimeScale => IsFrozen ? 0f : 1f;
    public float DeltaTime => Time.deltaTime * TimeScale;

    public bool SupportsMomentum => body != null && !(IsFrozen ? bodyWasKinematic : body.isKinematic);

    // Center of the object's visible bounds - used for line of sight and the momentum arrow.
    public Vector3 AimPoint => TryGetBounds(out Bounds bounds) ? bounds.center : transform.position;

    // Point just below the top of the visible bounds - used as a second line-of-sight check,
    // because the top of a platform is often visible while its center isn't.
    public Vector3 TopPoint => TryGetBounds(out Bounds bounds) ? bounds.center + Vector3.up * (bounds.extents.y * 0.8f) : transform.position;

    // Distance from a point to the nearest edge of the object, so big platforms count as close
    // as soon as any part of them is close.
    public float DistanceTo(Vector3 point)
    {
        return Vector3.Distance(point, ClosestPoint(point));
    }

    // Point on the object's visible bounds nearest to the given point.
    public Vector3 ClosestPoint(Vector3 point)
    {
        return TryGetBounds(out Bounds bounds) ? bounds.ClosestPoint(point) : transform.position;
    }

    private bool TryGetBounds(out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = default;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return hasBounds;
    }

    private FreezeSettings Settings => FreezeSettings.Instance;

    private Animator[] animators;
    private float[] animatorSpeeds;
    private Rigidbody body;
    private bool bodyWasKinematic;
    private Vector3 savedVelocity;
    private Vector3 savedAngularVelocity;

    private Renderer[] renderers;
    private Color[] originalColors;
    private MaterialPropertyBlock propertyBlock;

    private float lastHitTime = -999f;
    private int lastWarningSecond = -1;
    private LineRenderer arrow;
    private AudioSource audioSource;

    private void Awake()
    {
        animators = GetComponentsInChildren<Animator>();
        animatorSpeeds = new float[animators.Length];
        body = GetComponent<Rigidbody>();

        // Cached before any outline/arrow renderers are added, so those are never tinted.
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = GetMaterialColor(renderers[i].sharedMaterial);

        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
        if (IsFrozen) Unfreeze();
    }

    private void Update()
    {
        if (!IsFrozen) return;

        RemainingFreezeTime -= Time.deltaTime;
        if (RemainingFreezeTime <= 0f)
        {
            Unfreeze();
            return;
        }

        UpdateFrozenVisuals();
    }

    public bool Freeze(float duration)
    {
        if (IsFrozen || !canBeFrozen) return false;

        IsFrozen = true;
        RemainingFreezeTime = duration;
        StoredMomentum = Vector3.zero;
        lastWarningSecond = -1;

        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] == null) continue;
            animatorSpeeds[i] = animators[i].speed;
            animators[i].speed = 0f;
        }

        if (body != null)
        {
            bodyWasKinematic = body.isKinematic;
            if (!bodyWasKinematic)
            {
                savedVelocity = body.linearVelocity;
                savedAngularVelocity = body.angularVelocity;
            }
            body.isKinematic = true;
        }

        ApplyTint(Settings.frozenTintStrength);
        PlaySound(Settings.freezeSound);
        onFrozen?.Invoke();
        return true;
    }

    public void Unfreeze()
    {
        if (!IsFrozen) return;

        IsFrozen = false;
        RemainingFreezeTime = 0f;

        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
                animators[i].speed = animatorSpeeds[i];
        }

        if (body != null)
        {
            body.isKinematic = bodyWasKinematic;
            if (!bodyWasKinematic)
            {
                if (StoredMomentum.sqrMagnitude > 0.0001f)
                {
                    // Stored hits replace the old motion, like Zelda's Stasis.
                    body.linearVelocity = StoredMomentum;
                    body.angularVelocity = Vector3.zero;
                }
                else
                {
                    body.linearVelocity = savedVelocity;
                    body.angularVelocity = savedAngularVelocity;
                }
            }
        }

        StoredMomentum = Vector3.zero;
        ApplyTint(0f);
        if (arrow != null) arrow.enabled = false;

        PlaySound(Settings.unfreezeSound);
        onUnfrozen?.Invoke();
    }

    // Adds one hit of launch momentum in the given (horizontal) direction. Only while frozen.
    public bool TryAddHit(Vector3 direction)
    {
        if (!IsFrozen || !SupportsMomentum || !Settings.momentumEnabled) return false;
        if (Time.time - lastHitTime < Settings.hitCooldown) return false;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;

        direction = (direction.normalized + Vector3.up * Settings.launchUpwardBias).normalized;
        lastHitTime = Time.time;

        StoredMomentum = Vector3.ClampMagnitude(StoredMomentum + direction * Settings.momentumPerHit, Settings.maxStoredSpeed);

        PlaySound(Settings.hitSound);
        UpdateArrow();
        return true;
    }

    private void UpdateFrozenVisuals()
    {
        float warning = Settings.warningDuration;
        if (RemainingFreezeTime > warning)
            return;

        // Blink between frozen and normal colour just before unfreezing.
        bool tintOn = Mathf.Repeat(RemainingFreezeTime * Settings.warningBlinkRate, 1f) < 0.5f;
        ApplyTint(tintOn ? Settings.frozenTintStrength : 0f);

        int second = Mathf.CeilToInt(RemainingFreezeTime);
        if (second != lastWarningSecond)
        {
            lastWarningSecond = second;
            PlaySound(Settings.warningTickSound);
        }
    }

    private void ApplyTint(float strength)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            if (strength <= 0f)
            {
                r.SetPropertyBlock(null);
                continue;
            }

            Color tinted = Color.Lerp(originalColors[i], Settings.frozenTint, strength);
            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, tinted);
            propertyBlock.SetColor(ColorId, tinted);
            r.SetPropertyBlock(propertyBlock);
        }
    }

    private static Color GetMaterialColor(Material material)
    {
        if (material == null) return Color.white;
        if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
        if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
        return Color.white;
    }

    private void UpdateArrow()
    {
        if (arrow == null)
        {
            GameObject arrowObject = new GameObject("MomentumArrow");
            arrowObject.transform.SetParent(transform, false);
            arrow = arrowObject.AddComponent<LineRenderer>();
            arrow.useWorldSpace = true;
            arrow.positionCount = 2;
            arrow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arrow.receiveShadows = false;
            arrow.widthCurve = AnimationCurve.Linear(0f, 0.25f, 1f, 0.02f); // tapers like an arrow
            arrow.sharedMaterial = GetArrowMaterial();
        }

        Color color = Settings.momentumArrowColor;
        arrow.startColor = color;
        arrow.endColor = color;
        if (arrow.sharedMaterial != null)
        {
            arrow.sharedMaterial.SetColor(BaseColorId, color);
            arrow.sharedMaterial.SetColor(ColorId, color);
        }

        Vector3 start = AimPoint;
        arrow.SetPosition(0, start);
        arrow.SetPosition(1, start + StoredMomentum * Settings.momentumArrowScale);
        arrow.enabled = true;
    }

    private static Material GetArrowMaterial()
    {
        if (arrowMaterial != null) return arrowMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) arrowMaterial = new Material(shader);
        return arrowMaterial;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }
        audioSource.PlayOneShot(clip, Settings.soundVolume);
    }
}
