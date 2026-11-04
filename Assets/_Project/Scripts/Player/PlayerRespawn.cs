using UnityEngine;
using UnityEngine.SceneManagement;

// Falling below the kill height reloads the whole level (platforms, charges, frozen objects,
// physics objects - everything back to the start), and the player spawns at the last checkpoint.
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("Respawn when the player falls below this height. Uses LevelSettings' default if unchanged.")]
    public bool overrideKillHeight = false;
    public float killHeight = -10f;

    // Survives the scene reload. Only used when the reload was caused by a respawn,
    // so entering the level normally (e.g. from the menu) always starts at the beginning.
    private static string savedScenePath;
    private static Vector3 savedRespawnPoint;
    private static bool respawnReloadPending;

    private Vector3 respawnPoint;
    private bool isReloading;
    private CharacterController characterController;

    public Vector3 RespawnPoint
    {
        get => respawnPoint;
        set
        {
            respawnPoint = value;
            savedScenePath = gameObject.scene.path;
            savedRespawnPoint = value;
        }
    }

    private float KillHeight => overrideKillHeight ? killHeight : LevelSettings.Instance.defaultKillHeight;

    // True if this position is the checkpoint the player respawned at (used to light up its flag).
    public static bool IsActiveCheckpoint(Vector3 point, string scenePath)
    {
        return savedScenePath == scenePath && (savedRespawnPoint - point).sqrMagnitude < 0.01f;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        string scenePath = gameObject.scene.path;

        if (respawnReloadPending && savedScenePath == scenePath)
        {
            respawnPoint = savedRespawnPoint;
            Teleport(respawnPoint);
        }
        else
        {
            savedScenePath = null; // fresh start
            respawnPoint = transform.position;
        }

        respawnReloadPending = false;
    }

    private void Update()
    {
        if (!isReloading && transform.position.y < KillHeight)
            Respawn();
    }

    public void Respawn()
    {
        isReloading = true;
        savedScenePath = gameObject.scene.path;
        savedRespawnPoint = respawnPoint;
        respawnReloadPending = true;

        Scene scene = gameObject.scene;
#if UNITY_EDITOR
        // Prototype scenes aren't in the Build Settings; the editor can still reload them by path.
        if (scene.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void Teleport(Vector3 position)
    {
        // A CharacterController overrides position changes while enabled.
        if (characterController != null) characterController.enabled = false;
        transform.position = position;
        if (characterController != null) characterController.enabled = true;
    }
}
