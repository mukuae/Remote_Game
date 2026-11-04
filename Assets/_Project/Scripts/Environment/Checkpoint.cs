using UnityEngine;

// Trigger volume: after falling, the player respawns here (see PlayerRespawn).
// Optionally lights up a flag and shows a message (colours and text in LevelSettings).
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Tooltip("Respawn position relative to this object.")]
    public Vector3 respawnOffset = new Vector3(0f, 0.2f, 0f);

    [Tooltip("Optional flag that changes colour when the checkpoint is reached.")]
    public Renderer flag;

    [Tooltip("Lit from the start without a message (use for the level start).")]
    public bool startsActive = false;

    private bool isActive;
    private MaterialPropertyBlock propertyBlock;

    private Vector3 RespawnPosition => transform.position + respawnOffset;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        bool restored = PlayerRespawn.IsActiveCheckpoint(RespawnPosition, gameObject.scene.path);
        SetActive(startsActive || restored);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerRespawn respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn == null) return;

        bool isNew = (respawn.RespawnPoint - RespawnPosition).sqrMagnitude > 0.01f;
        respawn.RespawnPoint = RespawnPosition;

        if (isNew && !isActive)
        {
            LevelSettings settings = LevelSettings.Instance;
            ScreenMessage.Show(settings.checkpointMessage, settings.checkpointMessageDuration);
        }
        SetActive(true);
    }

    private void SetActive(bool active)
    {
        isActive = active;
        if (flag == null) return;

        LevelSettings settings = LevelSettings.Instance;
        Color color = active ? settings.checkpointActiveColor : settings.checkpointInactiveColor;

        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        flag.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        flag.SetPropertyBlock(propertyBlock);
    }
}
