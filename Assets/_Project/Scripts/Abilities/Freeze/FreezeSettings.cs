using UnityEngine;

// One global place for every number of the freeze ability.
// The asset lives at Assets/_Project/Resources/FreezeSettings.asset and is loaded automatically.
// Changes made in Play mode are kept (it's an asset), which makes tuning easy.
[CreateAssetMenu(fileName = "FreezeSettings", menuName = "Remote Game/Freeze Settings")]
public class FreezeSettings : ScriptableObject
{
    private const string ResourcePath = "FreezeSettings";
    private static FreezeSettings instance;

    public static FreezeSettings Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = Resources.Load<FreezeSettings>(ResourcePath);
            if (instance == null)
            {
                Debug.LogWarning("FreezeSettings asset not found in a Resources folder, using default values. " +
                                 "Create it via Tools > Remote Game > Create Freeze Settings Asset.");
                instance = CreateInstance<FreezeSettings>();
            }
            return instance;
        }
    }

    [Header("Freeze")]
    [Tooltip("How long an object stays frozen (seconds).")]
    [Min(0.1f)] public float freezeDuration = 5f;

    [Tooltip("The last X seconds of a freeze blink as a warning.")]
    [Min(0f)] public float warningDuration = 1.5f;

    [Tooltip("Blinks per second during the warning.")]
    [Min(0.1f)] public float warningBlinkRate = 6f;

    [Header("Charges")]
    [Tooltip("How many freezes can be stored.")]
    [Min(1)] public int maxCharges = 2;

    [Tooltip("Seconds to recharge one charge.")]
    [Min(0.1f)] public float rechargeTime = 4f;

    [Header("Targeting")]
    [Tooltip("Maximum distance to a freezable object (the cylinder's radius when height is ignored).")]
    [Min(1f)] public float targetRange = 25f;

    [Tooltip("Range is a cylinder: height differences don't count, so platforms moving up and down stay in range.")]
    public bool ignoreHeightInRange = true;

    [Tooltip("Height limit of the cylinder (up and down from the player). 0 = unlimited.")]
    [Min(0f)] public float maxHeightDifference = 0f;

    [Tooltip("Objects that neither the player nor the camera can see can't be chosen. " +
             "Once an object was choosable during targeting, it stays in the Tab list while in range.")]
    public bool requireLineOfSight = true;

    [Tooltip("Layers that block line of sight.")]
    public LayerMask lineOfSightMask = ~(1 << 2); // everything except Ignore Raycast

    [Tooltip("How far the right stick must be pushed to count as a flick.")]
    [Range(0.1f, 1f)] public float stickDeadzone = 0.5f;

    [Tooltip("A right-stick flick picks targets within this on-screen angle of the flick direction.")]
    [Range(5f, 180f)] public float stickFlickMaxAngle = 60f;

    [Tooltip("Height above the player's feet that distance and line of sight are measured from.")]
    public float aimOriginHeight = 1f;

    [Header("Momentum (objects with a non-kinematic Rigidbody)")]
    public bool momentumEnabled = true;

    [Tooltip("Launch speed added per hit on a frozen object.")]
    [Min(0f)] public float momentumPerHit = 4f;

    [Tooltip("Maximum launch speed that can be stored.")]
    [Min(0f)] public float maxStoredSpeed = 25f;

    [Tooltip("Upward part of the launch direction (0 = flat).")]
    [Range(0f, 1f)] public float launchUpwardBias = 0.15f;

    [Tooltip("Minimum seconds between two hits on the same object.")]
    [Min(0f)] public float hitCooldown = 0.25f;

    [Tooltip("How far in front of the player the Attack button reaches.")]
    [Min(0.1f)] public float strikeRange = 2f;

    [Tooltip("Running into a frozen object also counts as a hit.")]
    public bool bumpCountsAsHit = true;

    [Header("Visuals")]
    public Color targetOutlineColor = new Color(0.3f, 0.9f, 1f, 1f);
    [Range(0.001f, 0.1f)] public float targetOutlineWidth = 0.03f;
    [Tooltip("Outline colour while the chosen target is out of range (it can't be frozen then).")]
    public Color outOfRangeOutlineColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    public Color frozenTint = new Color(0.45f, 0.75f, 1f, 1f);
    [Range(0f, 1f)] public float frozenTintStrength = 0.65f;
    public Color momentumArrowColor = new Color(1f, 0.55f, 0.1f, 1f);

    [Tooltip("Arrow length per unit of stored launch speed.")]
    [Min(0f)] public float momentumArrowScale = 0.15f;

    [Header("Audio (leave empty for silence)")]
    public AudioClip freezeSound;
    public AudioClip unfreezeSound;
    [Tooltip("Plays once per second during the warning.")]
    public AudioClip warningTickSound;
    public AudioClip noChargeSound;
    public AudioClip hitSound;
    [Range(0f, 1f)] public float soundVolume = 0.8f;

    [Header("Tutorial Prompt")]
    public bool showTutorialPrompt = true;

    [Tooltip("Hide the 'press to freeze' prompt after the first successful freeze. " +
             "The controls shown while choosing a target always stay visible.")]
    public bool hidePromptAfterFirstFreeze = true;

    [Header("Failure Hints (shown when Freeze is pressed but nothing can be chosen)")]
    public bool showFailureHints = true;
    public string nothingInRangeMessage = "Nothing in range";
    public string nothingInSightMessage = "Nothing in sight";
    public string noChargesMessage = "No charges left";
    [Min(0f)] public float failureHintDuration = 1.2f;

    [Header("Debug")]
    [Tooltip("Scene view in Play mode: range circle, and lines to freezable objects in range " +
             "(yellow = selected, green = choosable, red = in range but not visible).")]
    public bool showDebugGizmos = true;
}
