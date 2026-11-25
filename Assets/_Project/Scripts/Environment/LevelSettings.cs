using UnityEngine;

// Global level values (checkpoints, respawn).
// The asset lives at Assets/_Project/Resources/LevelSettings.asset and is loaded automatically.
[CreateAssetMenu(fileName = "LevelSettings", menuName = "Remote Game/Level Settings")]
public class LevelSettings : ScriptableObject
{
    private const string ResourcePath = "LevelSettings";
    private static LevelSettings instance;

    public static LevelSettings Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = Resources.Load<LevelSettings>(ResourcePath);
            if (instance == null)
            {
                Debug.LogWarning("LevelSettings asset not found in a Resources folder, using default values.");
                instance = CreateInstance<LevelSettings>();
            }
            return instance;
        }
    }

    [Header("Checkpoints")]
    [Tooltip("Text shown on screen when a new checkpoint is reached.")]
    public string checkpointMessage = "Checkpoint";

    [Tooltip("How long the checkpoint message stays on screen (seconds).")]
    [Min(0f)] public float checkpointMessageDuration = 1.5f;

    [Tooltip("Flag colour before the checkpoint is reached.")]
    public Color checkpointInactiveColor = new Color(0.55f, 0.15f, 0.15f);

    [Tooltip("Flag colour once the checkpoint is reached.")]
    public Color checkpointActiveColor = new Color(0.2f, 0.9f, 0.35f);

    [Header("Pogo (defaults for all pads - a pad can override them)")]
    [Tooltip("How high a pogo launches the player, in meters (like the player's Jump Height).")]
    [Min(0f)] public float pogoLaunchHeight = 4f;

    [Tooltip("Sideways launch speed (m/s), away from the pad's reference point.")]
    [Min(0f)] public float pogoSidewaysSpeed = 6f;

    [Header("Respawn")]
    [Tooltip("Default height below which the player respawns. Each level can override it on PlayerRespawn.")]
    public float defaultKillHeight = -10f;
}
