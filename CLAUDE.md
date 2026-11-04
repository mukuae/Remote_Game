# Remote_Game – notes for Claude Code

Unity **6000.3.10f1**, URP, 3D platformer with abilities (freeze, pogo, reptile/light points).
Team project on GitHub (`mukuae/Remote_Game`). Several people use Claude Code on it.

## Rules for Claude (whole team)

- **Propose first, then change.** Before editing, moving or deleting anything, describe the plan and
  wait for the user's OK. Reading and investigating is fine without asking.
- **Never move a Unity file without its `.meta` file.** The `.meta` holds the GUID that scenes and
  prefabs use; losing it breaks references. Move both together.
- **Class name = file name**, English names and comments only.
- **Numbers go into settings assets**, not hard-coded: `FreezeSettings`, `LevelSettings`
  (`Assets/_Project/Resources/`). Per-object values (e.g. a platform's speed) stay in the Inspector.
- New content goes into `Assets/_Project/...`, never into the `Assets` root.
- Scenes are text (Force Text). Don't hand-edit scene YAML unless necessary; prefer editor scripts.
- Never commit secrets (API keys) and never push without the user's OK.
- Uncommitted changes in the working tree may belong to the user - don't include them in a commit
  unless asked.

## Project layout

```
Assets/
├─ _Project/     own content
│  ├─ Scenes/    MainMenu, Levels/ (MainLevel1_V02 = shipping level, MainLevel1, BeginningArea),
│  │             Prototypes/ (PrototypeTesting, ReptileTest, FreezeTest)
│  ├─ Scripts/   Player, Camera, Abilities (Freeze, Reptile, SpiderClimbing), Environment,
│  │             Selection, UI, Audio, Editor
│  ├─ Resources/ FreezeSettings.asset, LevelSettings.asset (loaded automatically)
│  └─ Animation, Audio, Input, Materials, Models, Shaders, Terrain, Textures, UI
├─ ThirdParty/   asset-store packs - don't modify
├─ _Archive/     unused old scenes/scripts, kept for reference
└─ Plugins, Settings, TextMesh Pro, Resources   (must stay in place)
```

Build Settings: `MainMenu` (0), `Levels/MainLevel1_V02` (1).

## Main systems

- **PlayerController** (CharacterController): coyote time, jump buffer, moving platforms (tag
  `MovingPlatform` or a Rigidbody), optional 2D lock. Grounded state is cached after the main Move()
  (the platform Move() would otherwise break jumping off platforms).
- **PogoTrigger**: registers itself with the player on enter; when Jump is pressed inside it,
  `PlayerController` calls `PogoTrigger.TryLaunch` instead of jumping. Usable once per `RechargeMode`.
- **Freeze ability** (`Scripts/Abilities/Freeze`): `FreezeAbility` on the player, `TimeControllable`
  on anything freezable (Animator, Rigidbody, or scripts reading `TimeControllable.DeltaTime` like
  `PlatformMover`). Press Freeze -> nearest target in front, Tab/LB next (front first, then behind),
  right-stick flick picks a direction, Freeze again to freeze, Cancel exits. Target never switches by
  itself (greys out out of range). Range is a cylinder (height ignored). Visible = player OR camera
  can see it. Charges, momentum (Zelda-style), HUD + tutorial prompt, debug gizmos.
- **Checkpoints**: `Checkpoint` triggers + `PlayerRespawn`; falling reloads the scene and respawns at
  the last checkpoint.
- **Reptile** (`Scripts/Abilities/Reptile`): `LightPoints` (drain/refill bar, death), `SunZone` marker.
  `orange_detection_camera.py` is a standalone OpenCV tool, not part of the Unity build.
- **Input**: new Input System only, project-wide actions in `_Project/Input/InputSystem_Actions`
  (Player map: Move, Jump, Attack, Freeze, CycleTarget, AimTarget, CancelTarget, ...).
- **UI/Audio**: `MainMenu`, `PauseMenu` (static `GameIsPaused` - gameplay scripts check it),
  `VolumeSettings`, `AudioManager`, `ScreenMessage.Show(text, seconds)`.

## Working with Unity from Claude

- Headless compile/import check (Unity must be **closed** for this project):
  `& "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -projectPath "<project>" -logFile "<log>"`
  then search the log for `error CS`.
- Rebuild the freeze test level: menu **Tools > Remote Game > Build Freeze Test Level**, or headless
  with `-executeMethod FreezeTestLevelBuilder.BuildFromCommandLine` (also writes screenshots to
  `Screenshots/` in the project root, which can be viewed to check the layout).
- If Unity is open, it recompiles when its window gets focus; check
  `%LOCALAPPDATA%\Unity\Editor\Editor.log` for errors.

## History (what was done with Claude, Oct–Nov 2026)

See `MERGE_NOTES.md` for details. In short: assets reorganized into `_Project/ThirdParty/_Archive`;
scripts renamed to English (class = file); bug fixes (pogo double-jump, pause state, jumping off
platforms, shaders in Always Included, invalid tag, null checks); all scripts moved to the new Input
System; old time-slow cone replaced by the freeze ability; checkpoints/respawn; FreezeTest level;
`.gitattributes` for Unity.

## Known open issues

- `LightPoints.cs` adds `Outline` to its UI text meaning `UnityEngine.UI.Outline`, but the project's
  own global `Outline` class (freeze highlight) wins -> text outline missing + warnings.
  Fix idea: rename our class to `MeshOutline`.
- `LightPoints` relies on `OnCollisionEnter` for solid sun zones; a CharacterController player gets no
  collision events -> only trigger sun zones work.
- `LightPoints` death is permanent (no respawn); could call `PlayerRespawn.Respawn()`.
- On some PCs Windows Smart App Control blocks Burst's JIT cache dll (error 4551). Not a code problem.

## Team decisions (shared memory)

Add important decisions here (one line each, with date and name) so everyone's Claude follows them.

- 2026-11-04 (Kaloyan): Claude proposes changes and waits for confirmation before editing.
- 2026-11-04 (Kaloyan): Git LFS prepared in `.gitattributes` but off until the team decides.
