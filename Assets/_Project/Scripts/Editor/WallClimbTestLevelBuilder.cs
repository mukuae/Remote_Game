using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds Assets/_Project/Scenes/Prototypes/WallClimbTest.unity:
// a copy of MainLevel1_V02 (player, dolly camera, UI, audio, light) with the level geometry
// replaced by a short course for testing the wall-climb ability.
// Menu: Tools > Remote Game > Build Wall Climb Test Level
public static class WallClimbTestLevelBuilder
{
    public const string SourceScene = "Assets/_Project/Scenes/Levels/MainLevel1_V02.unity";
    public const string TargetScene = "Assets/_Project/Scenes/Prototypes/WallClimbTest.unity";
    private const string MaterialFolder = "Assets/_Project/Materials/WallClimbTest";

    // The main level's camera dolly runs along world X and looks towards -Z; the player walks at this depth.
    // Screen-right is world -X, so the course is built from +X towards -X.
    public const float LaneZ = 22.5f;

    // Course layout (shared with WallClimbSelfTest).
    public static readonly Vector3 StartPosition = new Vector3(16f, 0.1f, LaneZ);
    public const float BackWallFaceZ = LaneZ - 1.5f;   // front face of the walls behind the lane
    public const float LockedWallX = 12f;               // climbable, but tried before the pickup
    public static readonly Vector3 PickupPosition = new Vector3(7f, 1f, LaneZ);
    public const float ClimbWallX = 2f;                 // climbable, 4 m tall
    public const float ClimbWallHeight = 4f;
    public const float PlainWallX = -4f;                // NOT climbable
    public const float SideWallFaceX = -11f;            // climbable wall facing the player along the lane
    public const float SideWallHeight = 4f;

    [MenuItem("Tools/Remote Game/Build Wall Climb Test Level")]
    public static void BuildMenu()
    {
        if (File.Exists(TargetScene) &&
            !EditorUtility.DisplayDialog("Rebuild Wall Climb Test Level",
                "WallClimbTest.unity already exists and will be overwritten. Continue?", "Rebuild", "Cancel"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    // Batch mode: Unity.exe -batchmode -projectPath . -executeMethod WallClimbTestLevelBuilder.BuildAndTestFromCommandLine
    // (no -quit: the self test exits the editor itself when it's done)
    public static void BuildAndTestFromCommandLine()
    {
        Build();
        WallClimbSelfTest.Run(exitWhenDone: true);
    }

    public static void Build()
    {
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

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!ShouldKeep(root, player)) Object.DestroyImmediate(root);
        }

        SetupPlayer(player);
        DisableMusic(scene);
        BuildCourse();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("WallClimbTestLevelBuilder: built " + TargetScene);
    }

    private static bool ShouldKeep(GameObject root, GameObject player)
    {
        if (root == player) return true;
        if (root.GetComponent<Camera>() || root.GetComponent<Light>() || root.GetComponent<Canvas>()) return true;
        if (root.GetComponent<AudioManager>() || root.GetComponent<UnityEngine.EventSystems.EventSystem>()) return true;

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
        WallClimb wallClimb = player.GetComponent<WallClimb>();
        if (wallClimb == null) wallClimb = player.AddComponent<WallClimb>();

        // Use the same input actions as PlayerController.
        SerializedObject controller = new SerializedObject(player.GetComponent<PlayerController>());
        SerializedObject climb = new SerializedObject(wallClimb);
        climb.FindProperty("moveAction").objectReferenceValue = controller.FindProperty("moveAction").objectReferenceValue;
        climb.FindProperty("jumpAction").objectReferenceValue = controller.FindProperty("jumpAction").objectReferenceValue;
        climb.FindProperty("animator").objectReferenceValue = controller.FindProperty("animator").objectReferenceValue;
        climb.FindProperty("abilityUnlocked").boolValue = false;       // unlocked by the pickup
        climb.FindProperty("allowSidewaysClimbing").boolValue = true;  // A/D move along walls facing the camera
        climb.ApplyModifiedPropertiesWithoutUndo();

        PlayerRespawn respawn = player.GetComponent<PlayerRespawn>();
        if (respawn == null) respawn = player.AddComponent<PlayerRespawn>();
        respawn.overrideKillHeight = true;
        respawn.killHeight = -8f;

        player.transform.position = StartPosition;
    }

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
        Material climbable = GetMaterial("ClimbableWall", new Color(0.3f, 0.75f, 0.35f));
        Material plain = GetMaterial("PlainWall", new Color(0.6f, 0.6f, 0.65f));
        Material pickup = GetMaterial("Pickup", new Color(0.2f, 0.6f, 1f));

        Transform course = new GameObject("WallClimbTestCourse").transform;

        Box("Ground", course, new Vector3(3f, -0.5f, LaneZ - 2f), new Vector3(34f, 1f, 12f), ground);
        Label(course, "WALL CLIMB TEST\nGreen walls are climbable once you have the ability", new Vector3(16f, 6.5f, LaneZ));

        // 1: climbable, but the ability isn't unlocked yet.
        BackWall("1_LockedClimbableWall", course, LockedWallX, 3f, climbable, isClimbable: true);
        Label(course, "1  Locked: walk into it (W)\nnothing happens yet", new Vector3(LockedWallX, 4.2f, LaneZ));

        // 2: pickup that unlocks the ability.
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "2_WallClimbUnlock";
        orb.transform.SetParent(course, false);
        orb.transform.position = PickupPosition;
        orb.transform.localScale = Vector3.one * 0.8f;
        orb.GetComponent<Renderer>().sharedMaterial = pickup;
        orb.GetComponent<Collider>().isTrigger = true;
        orb.AddComponent<WallClimbUnlock>();
        Label(course, "2  Grab the orb\nto unlock climbing", new Vector3(PickupPosition.x, 3f, LaneZ));

        // 3: climbable wall with a ledge on top.
        BackWall("3_ClimbableWall", course, ClimbWallX, ClimbWallHeight, climbable, isClimbable: true);
        Label(course, "3  Walk into it (W), climb with W A S D\nJump to leap off, S to climb down", new Vector3(ClimbWallX, 5.4f, LaneZ));

        // 4: same wall without ClimbableWall.
        BackWall("4_PlainWall", course, PlainWallX, ClimbWallHeight, plain, isClimbable: false);
        Label(course, "4  Grey walls can't be climbed", new Vector3(PlainWallX, 5.4f, LaneZ));

        // 5: climbable wall across the lane, grabbed by walking right into it.
        GameObject side = Box("5_SideClimbableWall", course,
            new Vector3(SideWallFaceX - 1f, SideWallHeight * 0.5f, LaneZ - 1f), new Vector3(2f, SideWallHeight, 8f), climbable);
        side.AddComponent<ClimbableWall>();
        Label(course, "5  Walk right into it,\nthen hold W to climb", new Vector3(SideWallFaceX + 2.5f, 5.4f, LaneZ));
    }

    private static void BackWall(string name, Transform parent, float x, float height, Material material, bool isClimbable)
    {
        GameObject wall = Box(name, parent, new Vector3(x, height * 0.5f, BackWallFaceZ - 1.5f), new Vector3(3f, height, 3f), material);
        if (isClimbable) wall.AddComponent<ClimbableWall>();
    }

    private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static void Label(Transform parent, string text, Vector3 position)
    {
        GameObject label = new GameObject("Label");
        label.transform.SetParent(parent, false);
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // the camera looks towards -Z

        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.1f, 0.12f, 0.18f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.rectTransform.sizeDelta = new Vector2(5.5f, 3f);
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
}
