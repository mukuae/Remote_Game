using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Add this to the player object (it contains no controls).
///
/// Creates a "Light Points" bar on screen that starts at 100%, drains at a
/// fixed rate, and refills while the player touches any object that has a
/// <see cref="SunZone"/> component.
///
/// - Running out of Light Points kills the player ("too cold").
/// - With Overheat enabled the bar can go above 100%, and reaching 200% kills
///   the player ("too hot").
/// - After death a message is shown and the player can no longer move.
///
/// All settings (decrease/increase rates, Overheat toggle, both death messages,
/// bar appearance, ...) are edited in the Inspector. Rates, the Overheat toggle
/// and the death messages can also be changed while the game is running.
///
/// Requirements: the player needs a Collider (or Collider2D) and, as usual for
/// Unity physics events, a Rigidbody (or Rigidbody2D) on either the player or
/// the sun zone object. Both trigger volumes and solid colliders are supported,
/// in 3D and 2D.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Light/Light Points")]
public class LightPoints : MonoBehaviour
{
    [Header("Light Points (% points per second)")]
    [Tooltip("Percentage points lost per second while NOT touching a sun zone.")]
    [Min(0f)] [SerializeField] private float drainPerSecond = 5f;

    [Tooltip("Percentage points gained per second while touching a sun zone.")]
    [Min(0f)] [SerializeField] private float regenPerSecond = 20f;

    [Header("Overheat")]
    [Tooltip("If enabled, Light Points can go above 100%. Reaching 200% kills the player.")]
    [SerializeField] private bool overheat = false;

    [Header("Death messages")]
    [Tooltip("Shown when Light Points run out.")]
    [FormerlySerializedAs("deathMessage")]
    [TextArea(1, 3)] [SerializeField] private string coldDeathMessage = "You got too cold and died";

    [Tooltip("Shown when Light Points reach 200% (only with Overheat).")]
    [TextArea(1, 3)] [SerializeField] private string hotDeathMessage = "You got too hot and died";

    [Header("On death")]
    [Tooltip("Disable every other script on this object (movement, input, etc.) so the player can't move.")]
    [SerializeField] private bool disableOtherScripts = true;

    [Tooltip("Scripts on this object that should keep running after death.")]
    [SerializeField] private MonoBehaviour[] keepEnabled;

    [Tooltip("Zero the Rigidbody/Rigidbody2D velocity and freeze it in place.")]
    [SerializeField] private bool freezePhysicsOnDeath = true;

    [Tooltip("Invoked once when the player dies.")]
    public UnityEvent onDeath = new UnityEvent();

    [Header("Bar appearance")]
    [SerializeField] private Vector2 barSize = new Vector2(300f, 28f);
    [Tooltip("Distance from the top-left corner of the screen.")]
    [SerializeField] private Vector2 barMargin = new Vector2(20f, 20f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color fillColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("Color of the part of the bar above 100% (only visible with Overheat).")]
    [SerializeField] private Color overheatColor = new Color(1f, 0.35f, 0.1f, 1f);

    /// <summary>Normal maximum: 100%.</summary>
    public const float Max = 100f;

    /// <summary>Death-by-heat threshold when Overheat is enabled: 200%.</summary>
    public const float OverheatMax = 200f;

    /// <summary>Current Light Points in percent (0 to 100, or up to 200 with Overheat).</summary>
    public float Current { get; private set; } = Max;

    /// <summary>The highest value Current can reach right now (100, or 200 with Overheat).</summary>
    public float CurrentMax => overheat ? OverheatMax : Max;

    /// <summary>Whether Overheat is enabled. Can be changed at runtime.</summary>
    public bool OverheatEnabled
    {
        get => overheat;
        set => overheat = value;
    }

    /// <summary>True while the player is touching at least one active sun zone.</summary>
    public bool IsInSunZone { get; private set; }

    /// <summary>True once the player has died (too cold or too hot).</summary>
    public bool IsDead { get; private set; }

    // Every sun-zone collider currently touching the player, mapped to its SunZone.
    private readonly Dictionary<Component, SunZone> contacts = new Dictionary<Component, SunZone>();
    private readonly List<Component> destroyed = new List<Component>();

    private GameObject canvasObject;
    private RectTransform fillRect;
    private RectTransform overheatFillRect;
    private GameObject markerObject;
    private Text label;
    private GameObject deathMessageObject;
    private Text deathText;

    private void Awake()
    {
        Current = Max;
        BuildBar();
        UpdateBar();
    }

    private void OnEnable()
    {
        if (canvasObject != null) canvasObject.SetActive(true);
    }

    private void OnDisable()
    {
        contacts.Clear();
        IsInSunZone = false;
        if (canvasObject != null) canvasObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }

    private void Update()
    {
        if (IsDead) return;

        IsInSunZone = EvaluateSunZones();

        float rate = IsInSunZone ? regenPerSecond : -drainPerSecond;
        // CurrentMax also pulls the value back to 100 if Overheat gets switched off.
        Current = Mathf.Clamp(Current + rate * Time.deltaTime, 0f, CurrentMax);

        UpdateBar();

        if (Current <= 0f)
            Die(coldDeathMessage);
        else if (overheat && Current >= OverheatMax)
            Die(hotDeathMessage);
    }

    // ---------------------------------------------------------------- death

    private void Die(string message)
    {
        IsDead = true;
        IsInSunZone = false;
        contacts.Clear();

        if (deathText != null) deathText.text = message;
        if (deathMessageObject != null) deathMessageObject.SetActive(true);

        if (freezePhysicsOnDeath) FreezeBody();
        if (disableOtherScripts) DisableOtherScripts();

        onDeath?.Invoke();
    }

    private void FreezeBody()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            if (!body.isKinematic)
            {
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity = Vector3.zero;
#else
                body.velocity = Vector3.zero;
#endif
                body.angularVelocity = Vector3.zero;
            }
            body.constraints = RigidbodyConstraints.FreezeAll;
        }

        Rigidbody2D body2D = GetComponent<Rigidbody2D>();
        if (body2D != null)
        {
#if UNITY_6000_0_OR_NEWER
            body2D.linearVelocity = Vector2.zero;
#else
            body2D.velocity = Vector2.zero;
#endif
            body2D.angularVelocity = 0f;
            body2D.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private void DisableOtherScripts()
    {
        foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
        {
            if (behaviour == this) continue;
            if (keepEnabled != null && System.Array.IndexOf(keepEnabled, behaviour) >= 0) continue;
            behaviour.enabled = false;
        }
    }

    // ---------------------------------------------------------------- contacts

    private bool EvaluateSunZones()
    {
        bool inSun = false;
        destroyed.Clear();

        foreach (KeyValuePair<Component, SunZone> pair in contacts)
        {
            // Collider or zone was destroyed while touching: forget it.
            if (pair.Key == null || pair.Value == null)
            {
                destroyed.Add(pair.Key);
                continue;
            }

            // Disabled/deactivated zones are ignored but kept, so they work
            // again if they are re-enabled while still overlapping.
            if (pair.Value.isActiveAndEnabled && pair.Key.gameObject.activeInHierarchy)
                inSun = true;
        }

        for (int i = 0; i < destroyed.Count; i++)
            contacts.Remove(destroyed[i]);

        return inSun;
    }

    private void AddContact(Component other)
    {
        // GetComponentInParent so a collider on a child of the sun zone object counts too.
        SunZone zone = other.GetComponentInParent<SunZone>();
        if (zone != null) contacts[other] = zone;
    }

    private void RemoveContact(Component other)
    {
        contacts.Remove(other);
    }

    // 3D
    private void OnTriggerEnter(Collider other) => AddContact(other);
    private void OnTriggerExit(Collider other) => RemoveContact(other);
    private void OnCollisionEnter(Collision collision) => AddContact(collision.collider);
    private void OnCollisionExit(Collision collision) => RemoveContact(collision.collider);

    // 2D
    private void OnTriggerEnter2D(Collider2D other) => AddContact(other);
    private void OnTriggerExit2D(Collider2D other) => RemoveContact(other);
    private void OnCollisionEnter2D(Collision2D collision) => AddContact(collision.collider);
    private void OnCollisionExit2D(Collision2D collision) => RemoveContact(collision.collider);

    // ---------------------------------------------------------------- bar UI

    private void BuildBar()
    {
        canvasObject = new GameObject("Light Points Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // The bar itself (top-left of the screen).
        RectTransform bar = CreateRect("Light Points", canvasObject.transform);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 1f);
        bar.sizeDelta = barSize;
        bar.anchoredPosition = new Vector2(barMargin.x, -barMargin.y);
        Image background = bar.gameObject.AddComponent<Image>();
        background.color = backgroundColor;
        background.raycastTarget = false;

        // Fill up to 100%: resized by changing its right anchor.
        fillRect = CreateRect("Fill", bar);
        Stretch(fillRect);
        Image fill = fillRect.gameObject.AddComponent<Image>();
        fill.color = fillColor;
        fill.raycastTarget = false;

        // Fill above 100% (Overheat): its left/right anchors are set in UpdateBar.
        overheatFillRect = CreateRect("Overheat Fill", bar);
        overheatFillRect.offsetMin = overheatFillRect.offsetMax = Vector2.zero;
        Image overheatFill = overheatFillRect.gameObject.AddComponent<Image>();
        overheatFill.color = overheatColor;
        overheatFill.raycastTarget = false;

        // Thin line marking 100% while Overheat is enabled (the bar then spans 0-200%).
        RectTransform marker = CreateRect("100% Marker", bar);
        markerObject = marker.gameObject;
        marker.anchorMin = new Vector2(0.5f, 0f);
        marker.anchorMax = new Vector2(0.5f, 1f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = new Vector2(2f, 0f);
        marker.anchoredPosition = Vector2.zero;
        Image markerImage = markerObject.AddComponent<Image>();
        markerImage.color = new Color(1f, 1f, 1f, 0.8f);
        markerImage.raycastTarget = false;

        // Text label centered on the bar.
        RectTransform labelRect = CreateRect("Label", bar);
        Stretch(labelRect);
        label = labelRect.gameObject.AddComponent<Text>();
        label.font = GetDefaultFont();
        label.fontSize = Mathf.Max(8, Mathf.RoundToInt(barSize.y * 0.65f));
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        labelRect.gameObject.AddComponent<Outline>();

        // Death message: full-screen dim overlay with centered text.
        // Created last so it draws on top of the bar; hidden until the player dies.
        RectTransform deathRect = CreateRect("Death Message", canvasObject.transform);
        deathMessageObject = deathRect.gameObject;
        Stretch(deathRect);
        Image dim = deathMessageObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        dim.raycastTarget = false;

        RectTransform deathTextRect = CreateRect("Text", deathRect);
        Stretch(deathTextRect);
        deathText = deathTextRect.gameObject.AddComponent<Text>();
        deathText.font = GetDefaultFont();
        deathText.fontSize = 64;
        deathText.alignment = TextAnchor.MiddleCenter;
        deathText.color = Color.white;
        deathText.raycastTarget = false;
        deathTextRect.gameObject.AddComponent<Outline>();

        deathMessageObject.SetActive(false);
    }

    private void UpdateBar()
    {
        // The bar spans 0-100%, or 0-200% while Overheat is enabled.
        float span = overheat ? OverheatMax : Max;
        float normalPart = Mathf.Min(Current, Max) / span;
        float overPart = Mathf.Max(0f, Current - Max) / span;
        float hundredMark = Max / span;

        fillRect.anchorMax = new Vector2(normalPart, 1f);
        overheatFillRect.anchorMin = new Vector2(hundredMark, 0f);
        overheatFillRect.anchorMax = new Vector2(hundredMark + overPart, 1f);

        if (markerObject.activeSelf != overheat) markerObject.SetActive(overheat);

        label.text = "Light Points " + Mathf.CeilToInt(Current) + "%";
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Font GetDefaultFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }
}