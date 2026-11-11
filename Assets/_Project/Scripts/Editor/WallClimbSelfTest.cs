using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Automated play-mode check of WallClimb in WallClimbTest.unity.
// Drives a virtual keyboard (W/S/D/Space) and checks where the player ends up.
// Menu: Tools > Remote Game > Run Wall Climb Self Test
// Results: Console + Logs/WallClimbSelfTest.txt (+ screenshots in Screenshots/).
[InitializeOnLoad]
public static class WallClimbSelfTest
{
    private const string RunningKey = "WallClimbSelfTest.Running";
    private const string ExitKey = "WallClimbSelfTest.Exit";
    private const string ResultsPath = "Logs/WallClimbSelfTest.txt";
    private const string ScreenshotFolder = "Screenshots";
    private const float TotalTimeout = 180f;

    private static readonly Stack<IEnumerator> routine = new Stack<IEnumerator>();
    private static Keyboard keyboard;
    private static Key[] heldKeys = new Key[0];
    private static readonly List<string> results = new List<string>();
    private static int failures;
    private static double startTime;

    private static InputSettings.BackgroundBehavior savedBackground;
    private static InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;

    private static PlayerController player;
    private static CharacterController controller;
    private static WallClimb climb;

    static WallClimbSelfTest()
    {
        // Survives the domain reload that happens when entering play mode.
        if (SessionState.GetBool(RunningKey, false))
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Remote Game/Run Wall Climb Self Test")]
    public static void RunMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(WallClimbTestLevelBuilder.TargetScene)) WallClimbTestLevelBuilder.Build();
        Run(exitWhenDone: false);
    }

    public static void Run(bool exitWhenDone)
    {
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(ExitKey, exitWhenDone);

        EditorSceneManager.OpenScene(WallClimbTestLevelBuilder.TargetScene, OpenSceneMode.Single);
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunningKey, false))
            return;

        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        results.Clear();
        failures = 0;
        startTime = EditorApplication.timeSinceStartup;
        routine.Clear();
        routine.Push(Steps());
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        bool finished;
        try
        {
            SendHeldKeys();
            finished = !Step();
            if (!finished && EditorApplication.timeSinceStartup - startTime > TotalTimeout)
            {
                Fail("Whole test", "timed out");
                finished = true;
            }
        }
        catch (System.Exception e)
        {
            Fail("Exception", e.ToString());
            finished = true;
        }

        if (finished) Finish();
    }

    // Advances the routine by one tick, running nested IEnumerators like Unity coroutines do.
    // Returns false when everything is done.
    private static bool Step()
    {
        while (routine.Count > 0)
        {
            IEnumerator current = routine.Peek();
            if (!current.MoveNext())
            {
                routine.Pop();
                continue; // the parent continues in the same tick
            }

            if (current.Current is IEnumerator nested)
            {
                routine.Push(nested);
                continue; // start the nested routine right away
            }

            return true; // yield return null: wait for the next tick
        }
        return false;
    }

    // ---------------------------------------------------------------- steps

    private static IEnumerator Steps()
    {
        yield return Wait(1f);

        player = Object.FindFirstObjectByType<PlayerController>();
        climb = player != null ? player.GetComponent<WallClimb>() : null;
        controller = player != null ? player.GetComponent<CharacterController>() : null;
        if (player == null || climb == null || controller == null)
        {
            Fail("Setup", "player, WallClimb or CharacterController missing");
            yield break;
        }

        SetupInput();
        float lane = WallClimbTestLevelBuilder.LaneZ;
        float backFace = WallClimbTestLevelBuilder.BackWallFaceZ;
        float wallTop = WallClimbTestLevelBuilder.ClimbWallHeight;

        // 1. Locked: walking into a climbable wall does nothing.
        Check("Starts locked", !climb.AbilityUnlocked, "AbilityUnlocked was already true");
        yield return Teleport(new Vector3(WallClimbTestLevelBuilder.LockedWallX, 0.1f, lane));
        Press(Key.W);
        bool grabbedWhileLocked = false;
        for (float end = Time.time + 1.5f; Time.time < end;) { grabbedWhileLocked |= climb.IsClimbing; yield return null; }
        Press();
        Check("Locked: can't climb", !grabbedWhileLocked && player.transform.position.y < 0.5f,
            $"climbing={grabbedWhileLocked}, y={player.transform.position.y:F2}");

        // 2. Pickup unlocks the ability.
        yield return Teleport(WallClimbTestLevelBuilder.PickupPosition + new Vector3(2f, -0.9f, 0f));
        Press(Key.D); // screen-right = towards the orb
        yield return WaitUntil(() => climb.AbilityUnlocked, 2f);
        Press();
        Check("Pickup unlocks", climb.AbilityUnlocked, "walked through the orb but still locked");
        if (!climb.AbilityUnlocked) climb.Unlock();

        // 3. Grab, climb up and pull up onto the ledge.
        yield return Teleport(new Vector3(WallClimbTestLevelBuilder.ClimbWallX, 0.1f, lane));
        Press(Key.W);
        yield return WaitUntil(() => climb.IsClimbing, 1.5f);
        Check("Grabs climbable wall", climb.IsClimbing, $"not climbing, z={player.transform.position.z:F2}");
        yield return Wait(0.6f);
        CaptureGameView("wallclimb_climbing");
        Check("Climbs up", player.transform.position.y > 1f, $"y={player.transform.position.y:F2}");
        yield return WaitUntil(() => !climb.IsClimbing, 5f);
        Press();
        yield return Wait(0.5f);
        Vector3 p = player.transform.position;
        Check("Pulls up onto ledge", !climb.IsClimbing && p.y > wallTop - 0.3f && p.z < backFace,
            $"climbing={climb.IsClimbing}, pos={p}");
        CaptureGameView("wallclimb_on_ledge");

        // 4. Wall jump.
        yield return GrabClimbWallAndClimb(0.5f);
        float zBefore = player.transform.position.z;
        Press(Key.Space);
        yield return Wait(0.1f);
        Press();
        yield return Wait(0.4f);
        Check("Wall jump lets go and pushes away", !climb.IsClimbing && player.transform.position.z > zBefore + 0.5f,
            $"climbing={climb.IsClimbing}, z {zBefore:F2} -> {player.transform.position.z:F2}");
        yield return Wait(1f);

        // 5. Climb back down to the floor.
        yield return GrabClimbWallAndClimb(0.6f);
        Press(Key.S);
        yield return WaitUntil(() => !climb.IsClimbing, 3f);
        Press();
        yield return Wait(0.3f);
        Check("Climbing down onto floor lets go", !climb.IsClimbing && player.transform.position.y < 0.5f,
            $"climbing={climb.IsClimbing}, y={player.transform.position.y:F2}");

        // 5b. A/D move sideways along the wall (screen-right = world -X).
        yield return GrabClimbWallAndClimb(0.4f);
        Vector3 before = player.transform.position;
        Press(Key.D);
        yield return Wait(0.4f);
        Press();
        yield return Wait(0.1f);
        Vector3 afterRight = player.transform.position;
        Check("D moves right along the wall", climb.IsClimbing && afterRight.x < before.x - 0.5f && Mathf.Abs(afterRight.y - before.y) < 0.3f,
            $"climbing={climb.IsClimbing}, {before} -> {afterRight}");
        Press(Key.A);
        yield return Wait(0.4f);
        Press();
        yield return Wait(0.1f);
        Vector3 afterLeft = player.transform.position;
        Check("A moves left along the wall", climb.IsClimbing && afterLeft.x > afterRight.x + 0.5f,
            $"climbing={climb.IsClimbing}, {afterRight} -> {afterLeft}");
        yield return Wait(0.5f);

        // 6. Grey wall can't be climbed.
        yield return Teleport(new Vector3(WallClimbTestLevelBuilder.PlainWallX, 0.1f, lane));
        Press(Key.W);
        bool grabbedPlain = false;
        for (float end = Time.time + 1.5f; Time.time < end;) { grabbedPlain |= climb.IsClimbing; yield return null; }
        Press();
        Check("Plain wall can't be climbed", !grabbedPlain && player.transform.position.y < 0.5f,
            $"climbing={grabbedPlain}, y={player.transform.position.y:F2}");

        // 7. Wall across the lane: walk right into it, then climb with W.
        yield return Teleport(new Vector3(WallClimbTestLevelBuilder.SideWallFaceX + 2.5f, 0.1f, lane));
        Press(Key.D);
        yield return WaitUntil(() => climb.IsClimbing, 2f);
        Check("Grabs side wall by walking right", climb.IsClimbing, $"pos={player.transform.position}");
        // A/D move along this wall too, independent of the camera.
        // Facing this wall (world -X), the player's right is world +Z.
        Press(Key.W);
        yield return Wait(0.3f);
        Vector3 sideStart = player.transform.position;
        Press(Key.D);
        yield return Wait(0.4f);
        Press();
        yield return Wait(0.1f);
        Vector3 sideRight = player.transform.position;
        Check("D moves right along the side wall", climb.IsClimbing && sideRight.z > sideStart.z + 0.5f,
            $"climbing={climb.IsClimbing}, {sideStart} -> {sideRight}");
        Press(Key.A);
        yield return Wait(0.4f);
        Press();
        yield return Wait(0.1f);
        Vector3 sideLeft = player.transform.position;
        Check("A moves left along the side wall", climb.IsClimbing && sideLeft.z < sideRight.z - 0.5f,
            $"climbing={climb.IsClimbing}, {sideRight} -> {sideLeft}");
        Press(Key.W);
        yield return WaitUntil(() => !climb.IsClimbing, 5f);
        Press();
        yield return Wait(0.5f);
        p = player.transform.position;
        Check("Climbs side wall onto top", p.y > WallClimbTestLevelBuilder.SideWallHeight - 0.3f && p.x < WallClimbTestLevelBuilder.SideWallFaceX,
            $"pos={p}, climbing={climb.IsClimbing}");

        // 8. Locking the ability mid-climb drops the player.
        yield return GrabClimbWallAndClimb(0.5f);
        climb.Lock();
        yield return Wait(0.1f);
        Check("Lock() mid-climb lets go", !climb.IsClimbing && player.enabled, $"climbing={climb.IsClimbing}, controller enabled={player.enabled}");
        yield return Wait(1f);
        Check("Falls back to the floor after Lock()", player.transform.position.y < 0.5f, $"y={player.transform.position.y:F2}");
        climb.Unlock();
    }

    private static IEnumerator GrabClimbWallAndClimb(float climbSeconds)
    {
        yield return Teleport(new Vector3(WallClimbTestLevelBuilder.ClimbWallX, 0.1f, WallClimbTestLevelBuilder.LaneZ));
        Press(Key.W);
        yield return WaitUntil(() => climb.IsClimbing, 1.5f);
        yield return Wait(climbSeconds);
        Press();
        yield return null;
    }

    // ---------------------------------------------------------------- helpers

    private static IEnumerator Teleport(Vector3 position)
    {
        Press();
        bool wasUnlocked = climb.AbilityUnlocked;
        if (climb.IsClimbing)
        {
            climb.Lock(); // lets go of the wall
            yield return Wait(0.1f);
        }

        controller.enabled = false;
        player.transform.SetPositionAndRotation(position, Quaternion.identity);
        controller.enabled = true;
        Physics.SyncTransforms();
        player.ResetMotion();

        climb.AbilityUnlocked = wasUnlocked;
        yield return Wait(0.5f); // land + regrab cooldown
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
    {
        float end = Time.time + timeout;
        while (!condition() && Time.time < end) yield return null;
    }

    private static void SetupInput()
    {
        savedBackground = InputSystem.settings.backgroundBehavior;
        savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

        heldKeys = new Key[0];
        keyboard = InputSystem.AddDevice<Keyboard>("WallClimbTestKeyboard");
    }

    private static void RestoreInput()
    {
        if (keyboard != null)
        {
            InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            InputSystem.settings.backgroundBehavior = savedBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
        }
    }

    // Holds exactly these keys (none = release everything).
    private static void Press(params Key[] keys)
    {
        heldKeys = keys;
        SendHeldKeys();
    }

    // Re-sent every tick so the held keys survive input-device resets (e.g. focus changes).
    // Re-sending an unchanged state doesn't count as a new press.
    private static void SendHeldKeys()
    {
        if (keyboard == null) return;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(heldKeys));
    }

    private static void Check(string name, bool passed, string detail)
    {
        if (passed) results.Add("PASS  " + name);
        else Fail(name, detail);
    }

    private static void Fail(string name, string detail)
    {
        failures++;
        results.Add($"FAIL  {name}: {detail}");
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        routine.Clear();
        RestoreInput();
        SessionState.SetBool(RunningKey, false);

        var report = new StringBuilder();
        report.AppendLine(failures == 0 ? "WALL CLIMB SELF TEST: ALL PASSED" : $"WALL CLIMB SELF TEST: {failures} FAILED");
        foreach (string line in results) report.AppendLine(line);

        Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath));
        File.WriteAllText(ResultsPath, report.ToString());
        if (failures == 0) Debug.Log(report.ToString());
        else Debug.LogError(report.ToString());

        bool exit = SessionState.GetBool(ExitKey, false);
        EditorApplication.isPlaying = false;
        if (exit) EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    // Renders the main camera (works without a visible Game view, e.g. in batch mode).
    private static void CaptureGameView(string name)
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        Directory.CreateDirectory(ScreenshotFolder);
        RenderTexture renderTexture = new RenderTexture(1280, 720, 24);
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(ScreenshotFolder, name + ".png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            RenderTexture.active = null;
            camera.targetTexture = previousTarget;
            Object.DestroyImmediate(renderTexture);
        }
    }
}
