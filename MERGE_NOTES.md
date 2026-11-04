# Merge notes – branch `claude-merge`

This branch reorganizes the project, renames scripts to English, fixes bugs and adds the freeze ability.
All `.meta` files were kept, so **every reference in scenes and prefabs still works**. Unity imports the
branch without errors.

## Before you merge or pull this

1. **Commit or stash your own work first.** Almost every file moved to a new folder.
2. If you have your own branch (`scripts`, `3D_assets`, `user_interface`, ...): merge `main` into it
   after this branch is merged. Git recognizes the moved files; conflicts are only likely where you
   changed one of the renamed scripts listed below.
3. Close Unity before switching to this branch, then reopen. The first import takes a moment.

## New folder structure

```
Assets/
├─ _Project/      everything we made: Scenes, Scripts, Animation, Audio, Models, Materials,
│                 Textures, Terrain, UI, Shaders, Input, Resources
├─ ThirdParty/    asset-store packs (nature kits, trees, Yughues, terrain painter, texture packs)
├─ _Archive/      unused things kept just in case (old test scenes, crash-recovery scene,
│                 Blender .blend1 backups, old time-slow cone + pendulum scripts)
└─ Plugins, Settings, TextMesh Pro, Resources   (left in place - Unity/plugins expect them there)
```

Scenes: `_Project/Scenes/MainMenu`, `Levels/` (MainLevel1_V02, MainLevel1, BeginningArea), `Prototypes/`
(PrototypeTesting, ReptileTest, FreezeTest). The Build Settings point to the new paths.

## Renamed scripts (class name = file name)

| Old | New | Folder |
|---|---|---|
| `Pogo_Trigger_Vekoter_Jump` / `ExtendedTrigger` | `PogoTrigger` | Scripts/Environment |
| `PlayerMovement` / `SpielerMovement` | `SimplePlayerMovement` | Scripts/Player |
| `Player Controler Physik` | `PlayerControllerPhysics` | Scripts/Player |
| `audio_manager` | `AudioManager` | Scripts/Audio |
| `main_menu` | `MainMenu` (button method: `PlayButtonClickSound`) | Scripts/UI |
| `volumesettings` | `VolumeSettings` | Scripts/UI |
| `Animation_pause` | `AnimationSpeedChanger` | Scripts/Environment |
| `CameraMovement*`, `Camer_on_Rails` | `StableFollowCamera`, `SideViewDynamicCamera`, `SimpleStableCamera`, `StableCameraWithLookAhead`, `SmoothThirdPersonCamera`, `CameraRail` | Scripts/Camera |
| `selectable`, `line` | `SelectableObject`, `LineSelector` | Scripts/Selection |
| `Spiderclimb` | `SpiderClimbing` | Scripts/Abilities |
| `Reptile/kamera.py` | `orange_detection_camera.py` (unchanged content) | Scripts/Abilities/Reptile |
| `LightPoints`, `SunZone` | unchanged | Scripts/Abilities/Reptile |

Removed: two empty template scripts (`PlayerMovement1`, `PendulumPlatformCarrie`).
Archived: `TimeSlowAbility`, `PendulumMovement` (replaced by the freeze ability).

## Bug fixes

- You can now **jump off moving platforms** (grounded check was read after the platform movement).
- Pogo: no more pogo + normal jump on the same press; a pogo can only be used once until you land
  (`Recharge Mode` on each PogoTrigger).
- Pause: works again after returning to the menu; gameplay ignores input while paused.
- Outline/cone shaders are in *Always Included Shaders* (they would have been missing in builds).
- `PlayerControllerPhysics` used a tag that didn't exist (`Platform` -> `MovingPlatform`).
- All our scripts use the new Input System; null checks added in several places.

## New: freeze ability (test it in `Prototypes/FreezeTest`)

| Action | Controller | Keyboard/Mouse |
|---|---|---|
| Start choosing / freeze | RB | F / right mouse |
| Next target (front first, nearest to farthest, then behind) | LB | Tab / Q |
| Pick target in a direction | flick right stick | – |
| Cancel | B | Backspace |
| Hit a frozen crate (stores momentum) | X | left mouse / Enter |

- Make any platform freezable: add the **`TimeControllable`** component (works with Animator, Rigidbody
  and `PlatformMover`).
- **All numbers** (duration, charges, range, colours, sounds, ...) are in
  `Assets/_Project/Resources/FreezeSettings`. Checkpoint settings: `.../Resources/LevelSettings`.
- Checkpoints (`Checkpoint` component) + `PlayerRespawn`: falling reloads the level and respawns at the
  last checkpoint.
- Rebuild the test level any time: **Tools > Remote Game > Build Freeze Test Level**.
- New input actions in `InputSystem_Actions`: Freeze, CycleTarget, AimTarget, CancelTarget.

## Git: `.gitattributes` and Unity Smart Merge (one-time setup per person)

The new `.gitattributes` normalizes line endings and marks images/models/audio as binary.
It also asks git to merge scenes/prefabs with Unity's **Smart Merge** tool, which understands Unity files.
Git doesn't allow repositories to set that up automatically, so **each of us runs this once**
(adjust the path if your Unity is installed elsewhere):

```
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver "\"C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Data/Tools/UnityYAMLMerge.exe\" merge -h -p --force %O %B %A %A"
git config --global merge.unityyamlmerge.recursive binary
```

Without this setup nothing breaks - git just uses its normal merge for those files.
Smart Merge still can't solve two people changing the *same object in the same scene*: agree on who
edits which scene, or use prefabs.

Git LFS (for large files) is prepared in `.gitattributes` but switched off - that's a team decision.
