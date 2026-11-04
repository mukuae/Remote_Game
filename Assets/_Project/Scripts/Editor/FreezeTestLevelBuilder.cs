using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds Assets/_Project/Scenes/Prototypes/FreezeTest.unity:
// a copy of MainLevel1_V02 (player, dolly camera, UI, audio, light) with the level geometry
// replaced by a short course for testing the freeze ability.
// Menu: Tools > Remote Game > Build Freeze Test Level
public static class FreezeTestLevelBuilder
{
    private const string SourceScene = "Assets/_Project/Scenes/Levels/MainLevel1_V02.unity";
    private const string TargetScene = "Assets/_Project/Scenes/Prototypes/FreezeTest.unity";
    private const string SettingsFolder = "Assets/_Project/Resources";
    private const string SettingsPath = SettingsFolder + "/FreezeSettings.asset";
    private const string LevelSettingsPath = SettingsFolder + "/LevelSettings.asset";
    private const string MaterialFolder = "Assets/_Project/Materials/FreezeTest";
    private const string ScreenshotFolder = "Screenshots";

    // The main level's camera dolly runs along world X; the player walks at this depth.
    // Screen-right is world -X, so the course is built from +X towards -X.
    private const float LaneZ = 22.5f;

    [MenuItem("Tools/Remote Game/Create Freeze Settings Asset")]
    public static void CreateSettingsMenu()
    {
        Selection.activeObject = EnsureSettingsAsset();
    }

    [MenuItem("Tools/Remote Game/Build Freeze Test Level")]
    public static void BuildMenu()
    {
        if (File.Exists(TargetScene) &&
            !EditorUtility.DisplayDialog("Rebuild Freeze Test Level",
                "FreezeTest.unity already exists and will be overwritten. Continue?", "Rebuild", "Cancel"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    // Batch mode entry point: Unity.exe -batchmode -quit -executeMethod FreezeTestLevelBuilder.BuildFromCommandLine
    public static void BuildFromCommandLine()
    {
        Build();
        CaptureScreenshots();
    }

    public static FreezeSettings EnsureSettingsAsset()
    {
        return EnsureAsset<FreezeSettings>(SettingsPath);
    }

    private static T EnsureAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static void Build()
    {
        EnsureSettingsAsset();
        EnsureAsset<LevelSettings>(LevelSettingsPath);

        if (File.Exists(TargetScene)) AssetDatabase.DeleteAsset(TargetScene);
        if (!AssetDatabase.CopyAsset(SourceScene, TargetScene))
            throw new IOException($"Could not copy {SourceScene}");

        var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

        GameObject player = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<PlayerController>() != null) player = root;
        }
        if (player == null) throw new System.Exception("No PlayerController found in " + SourceScene);

        // Keep the player, camera rig, UI, audio and light. Everything else is old level geometry.
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!ShouldKeep(root, player)) Object.DestroyImmediate(root);
        }

        SetupPlayer(player);
        DisableMusic(scene);
        new GameObject("FreezeHUD").AddComponent<FreezeHUD>();
        BuildCourse();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("FreezeTestLevelBuilder: built " + TargetScene);
    }

    private static bool ShouldKeep(GameObject root, GameObject player)
    {
        if (root == player) return true;
        if (root.GetComponent<Camera>() || root.GetComponent<Light>() || root.GetComponent<Canvas>()) return true;
        if (root.GetComponent<AudioManager>() || root.GetComponent<UnityEngine.EventSystems.EventSystem>()) return true;

        // Cinemachine camera + dolly spline (checked by name to avoid a hard package dependency).
        foreach (Component component in root.GetComponents<Component>())
        {
            if (component == null) continue;
            string typeName = component.GetType().Name;
            if (typeName == "CinemachineCamera" || typeName == "SplineContainer") return true;
        }
        return false;
    }

    private static void SetupPlayer(GameObject player)
    {
        if (player.GetComponent<FreezeAbility>() == null) player.AddComponent<FreezeAbility>();

        PlayerRespawn respawn = player.GetComponent<PlayerRespawn>();
        if (respawn == null) respawn = player.AddComponent<PlayerRespawn>();
        respawn.overrideKillHeight = true;
        respawn.killHeight = -8f;

        player.transform.position = new Vector3(21f, 0.1f, LaneZ);
    }

    // Test level only: the music stays in the menu and the main level. Click sounds are kept.
    private static void DisableMusic(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (AudioManager audioManager in root.GetComponentsInChildren<AudioManager>(true))
            {
                SerializedObject serialized = new SerializedObject(audioManager);
                AudioSource musicSource = serialized.FindProperty("musicSource").objectReferenceValue as AudioSource;
                serialized.FindProperty("mainMenuTheme").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (musicSource != null)
                {
                    musicSource.clip = null;
                    musicSource.playOnAwake = false;
                }
            }
        }
    }

    // ---------- Course ----------

    private static void BuildCourse()
    {
        Material ground = GetMaterial("Ground", new Color(0.42f, 0.44f, 0.47f));
        Material platform = GetMaterial("MovingPlatform", new Color(0.95f, 0.75f, 0.2f));
        Material crate = GetMaterial("Crate", new Color(0.75f, 0.35f, 0.15f));
        Material target = GetMaterial("TargetBlocks", new Color(0.85f, 0.85f, 0.9f));

        Transform course = new GameObject("FreezeTestCourse").transform;

        // --- Start
        Box("Ground_Start", course, new Vector3(19.5f, -0.5f, LaneZ), new Vector3(11f, 1f, 6f), ground);
        AddCheckpoint(course, new Vector3(21f, 0f, LaneZ), startsActive: true);
        Label(course, "FREEZE TEST\nRB / F / Right Mouse: choose, then freeze\nLB / Tab: next target   B / Backspace: cancel", new Vector3(19.5f, 3.6f, LaneZ));

        // --- 1: one slow moving platform (learn targeting + freezing)
        GameObject slider = Box("1_SlidingPlatform", course, new Vector3(11.5f, -0.25f, LaneZ), new Vector3(3f, 0.5f, 4f), platform, "MovingPlatform");
        Mover(slider, new Vector3(-7f, 0f, 0f), 2.5f, 0.5f, 0f);
        Label(course, "1  Press Freeze, then Freeze again\nwhen the platform is close", new Vector3(8f, 3f, LaneZ));

        Box("Ground_1", course, new Vector3(-1f, -0.5f, LaneZ), new Vector3(6f, 1f, 6f), ground);
        AddCheckpoint(course, new Vector3(0f, 0f, LaneZ));

        // --- 2: three fast pistons (needs good timing and both charges)
        for (int i = 0; i < 3; i++)
        {
            GameObject piston = Box($"2_Piston_{i + 1}", course, new Vector3(-6.5f - 4f * i, -3.25f, LaneZ), new Vector3(2.5f, 0.5f, 4f), platform, "MovingPlatform");
            Mover(piston, new Vector3(0f, 5.5f, 0f), 1.1f, 0.2f, i / 3f);
        }
        Label(course, "2  Too fast to jump on\nFreeze them at the right height", new Vector3(-10.5f, 4.2f, LaneZ));

        Box("Ground_2", course, new Vector3(-19.5f, -0.5f, LaneZ), new Vector3(5f, 1f, 6f), ground);
        AddCheckpoint(course, new Vector3(-18.5f, 0f, LaneZ));

        // --- 3: spinning bridge (freeze it when it lines up)
        GameObject bridge = Box("3_SpinningBridge", course, new Vector3(-27.5f, -0.25f, LaneZ), new Vector3(9f, 0.5f, 2f), platform, "MovingPlatform");
        bridge.AddComponent<TimeControllable>(); // was missing - the bridge couldn't be frozen
        PlatformMover spin = bridge.AddComponent<PlatformMover>();
        spin.mode = PlatformMover.MoveMode.Rotate;
        spin.rotationAxis = Vector3.up;
        spin.degreesPerSecond = 45f;
        Label(course, "3  Freeze the bridge\nwhen it lines up", new Vector3(-27.5f, 3f, LaneZ));

        Box("Ground_3", course, new Vector3(-42.5f, -0.5f, LaneZ), new Vector3(19f, 1f, 6f), ground);
        AddCheckpoint(course, new Vector3(-34.5f, 0f, LaneZ));

        // --- 4: momentum - freeze the crate, hit it, watch it fly into the blocks
        GameObject crateObject = Box("4_Crate", course, new Vector3(-37f, 0.75f, LaneZ), new Vector3(1.5f, 1.5f, 1.5f), crate);
        Rigidbody crateBody = crateObject.AddComponent<Rigidbody>();
        crateBody.mass = 5f;
        crateObject.AddComponent<TimeControllable>();

        Vector3 stackBase = new Vector3(-46f, 0.4f, LaneZ);
        int row = 0;
        for (int count = 3; count > 0; count--, row++)
        {
            for (int i = 0; i < count; i++)
            {
                float x = stackBase.x - (i - (count - 1) * 0.5f) * 0.85f;
                GameObject block = Box($"4_Block_{row}_{i}", course, new Vector3(x, stackBase.y + row * 0.82f, LaneZ), Vector3.one * 0.8f, target);
                block.AddComponent<Rigidbody>().mass = 0.5f;
            }
        }
        Label(course, "4  Freeze the crate, hit it (Attack)\nor run into it, then wait", new Vector3(-41f, 3.4f, LaneZ));
    }

    private static void Mover(GameObject target, Vector3 offset, float travelTime, float pause, float phase)
    {
        target.AddComponent<TimeControllable>();
        PlatformMover mover = target.AddComponent<PlatformMover>();
        mover.mode = PlatformMover.MoveMode.PingPong;
        mover.moveOffset = offset;
        mover.travelTime = travelTime;
        mover.pauseAtEnds = pause;
        mover.startPhase = phase;
    }

    private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, string tag = "Untagged")
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.tag = tag;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static void AddCheckpoint(Transform parent, Vector3 position, bool startsActive = false)
    {
        GameObject checkpoint = new GameObject("Checkpoint");
        checkpoint.transform.SetParent(parent, false);
        checkpoint.transform.position = position + Vector3.up * 1.5f;

        BoxCollider trigger = checkpoint.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2f, 3f, 6f);

        Checkpoint component = checkpoint.AddComponent<Checkpoint>();
        component.respawnOffset = new Vector3(0f, -1.3f, 0f);
        component.startsActive = startsActive;

        // Flag at the back edge of the lane, so it never blocks the player or the camera's view.
        Vector3 postBase = new Vector3(position.x, 0f, LaneZ - 2.6f);
        GameObject post = Decoration("FlagPost", checkpoint.transform, PrimitiveType.Cylinder,
            postBase + Vector3.up * 1.25f, new Vector3(0.12f, 1.25f, 0.12f), GetMaterial("FlagPost", new Color(0.2f, 0.2f, 0.22f)));
        GameObject flag = Decoration("Flag", checkpoint.transform, PrimitiveType.Cube,
            postBase + new Vector3(-0.5f, 2.2f, 0f), new Vector3(0.9f, 0.55f, 0.06f), GetMaterial("CheckpointFlag", Color.white));
        component.flag = flag.GetComponent<Renderer>();
    }

    // Visual-only primitive (no collider).
    private static GameObject Decoration(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject decoration = GameObject.CreatePrimitive(type);
        decoration.name = name;
        Object.DestroyImmediate(decoration.GetComponent<Collider>());
        decoration.transform.SetParent(parent, true);
        decoration.transform.position = position;
        decoration.transform.localScale = scale;
        decoration.GetComponent<Renderer>().sharedMaterial = material;
        return decoration;
    }

    private static void Label(Transform parent, string text, Vector3 position)
    {
        GameObject label = new GameObject("Label");
        label.transform.SetParent(parent, false);
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // the camera looks towards -Z

        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.1f, 0.12f, 0.18f); // dark, the signs sit in front of the bright sky
        tmp.fontStyle = FontStyles.Bold;
        tmp.rectTransform.sizeDelta = new Vector2(12f, 3f);
    }

    private static Material GetMaterial(string name, Color color)
    {
        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ---------- Screenshots (for checking the layout without opening the editor) ----------

    [MenuItem("Tools/Remote Game/Capture Freeze Test Screenshots")]
    public static void CaptureScreenshots()
    {
        EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        Directory.CreateDirectory(ScreenshotFolder);

        // Gameplay-like views: same height/distance as the dolly camera.
        Capture("view_start", new Vector3(14f, 3.5f, 35.6f), new Vector3(14f, 0.5f, LaneZ), 60f);
        Capture("view_pistons", new Vector3(-10f, 3.5f, 35.6f), new Vector3(-10f, 0.5f, LaneZ), 60f);
        Capture("view_end", new Vector3(-36f, 3.5f, 35.6f), new Vector3(-36f, 0.5f, LaneZ), 60f);
        Capture("overview", new Vector3(-12f, 30f, 62f), new Vector3(-12f, 0f, LaneZ), 60f);

        Debug.Log("FreezeTestLevelBuilder: screenshots saved to " + Path.GetFullPath(ScreenshotFolder));
    }

    private static void Capture(string name, Vector3 position, Vector3 lookAt, float fieldOfView)
    {
        GameObject cameraObject = new GameObject("ScreenshotCamera");
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = position;
            camera.transform.LookAt(lookAt);
            camera.fieldOfView = fieldOfView;

            RenderTexture renderTexture = new RenderTexture(1600, 900, 24);
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(ScreenshotFolder, name + ".png"), image.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(renderTexture);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }
}
