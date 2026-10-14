using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Add this to the player object (it contains no controls).
///
/// Creates a "Light Points" bar on screen that starts at 100%, drains at a
/// fixed rate, and refills while the player touches any object that has a
/// <see cref="SunZone"/> component. When it reaches 0 the player dies: a death
/// message is shown and the player can no longer move.
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
    [Header("Light Points (percent)")]
    [Tooltip("Percent lost per second while NOT touching a sun zone.")]
    [SerializeField] private float drainPerSecond = 5f;

    [Tooltip("Percent gained per second while touching a sun zone (capped at 100%).")]
    [SerializeField] private float regenPerSecond = 20f;

    [Header("Bar appearance")]
    [SerializeField] private Vector2 barSize = new Vector2(300f, 28f);
    [Tooltip("Distance from the top-left corner of the screen.")]
    [SerializeField] private Vector2 barMargin = new Vector2(20f, 20f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color fillColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("Death")]
    [SerializeField] private string deathMessage = "You got too cold and died";

    [Tooltip("On death, disable every other script on this object (movement, input, etc.) so the player can't move.")]
    [SerializeField] private bool disableOtherScripts = true;

    [Tooltip("Scripts on this object that should keep running after death.")]
    [SerializeField] private MonoBehaviour[] keepEnabled;

    [Tooltip("On death, zero the Rigidbody/Rigidbody2D velocity and freeze it in place.")]
    [SerializeField] private bool freezePhysicsOnDeath = true;

    [Tooltip("Invoked once when the player runs out of Light Points.")]
    public UnityEvent onDeath = new UnityEvent();

    public const float Max = 100f;

    /// <summary>Current Light Points, 0 to 100.</summary>
    public float Current { get; private set; } = Max;

    /// <summary>True while the player is touching at least one active sun zone.</summary>
    public bool IsInSunZone { get; private set; }

    /// <summary>True once the player has run out of Light Points.</summary>
    public bool IsDead { get; private set; }

    // Every sun-zone collider currently touching the player, mapped to its SunZone.
    private readonly Dictionary<Component, SunZone> contacts = new Dictionary<Component, SunZone>();
    private readonly List<Component> destroyed = new List<Component>();

    private GameObject canvasObject;
    private RectTransform fillRect;
    private Text label;
    private GameObject deathMessageObject;

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
        Current = Mathf.Clamp(Current + rate * Time.deltaTime, 0f, Max);

        UpdateBar();

        if (Current <= 0f) Die();
    }

    // ---------------------------------------------------------------- death

    private void Die()
    {
        IsDead = true;
        IsInSunZone = false;
        contacts.Clear();

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

    // ---------------------------------------------------------------- UI

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

        // Fill: resized by changing its right anchor.
        fillRect = CreateRect("Fill", bar);
        Stretch(fillRect);
        Image fill = fillRect.gameObject.AddComponent<Image>();
        fill.color = fillColor;
        fill.raycastTarget = false;

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
        Text deathText = deathTextRect.gameObject.AddComponent<Text>();
        deathText.font = GetDefaultFont();
        deathText.fontSize = 64;
        deathText.alignment = TextAnchor.MiddleCenter;
        deathText.color = Color.white;
        deathText.text = deathMessage;
        deathText.raycastTarget = false;
        deathTextRect.gameObject.AddComponent<Outline>();

        deathMessageObject.SetActive(false);
    }

    private void UpdateBar()
    {
        fillRect.anchorMax = new Vector2(Current / Max, 1f);
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