# Project Real Loser RC1.16 — Consolidated Engineering Pass

Date: 2026-10-20

## Purpose
This is a consolidated source-level pass over the existing Unity project, rather than another single-feature patch. It addresses several high-impact engine/runtime defects and wires in mission mechanics that were previously represented only by data or simplified interactions.

## Implemented in this pass

### GLB rendering and runtime assets
- The GLB loader now combines every primitive of a glTF mesh into Unity submeshes and assigns the corresponding material slots. The prior loader selected only primitive zero, which discarded geometry in most multi-primitive assets.
- Rigged mesh primitives now read `JOINTS_0`, `WEIGHTS_0`, and inverse bind matrices, build Unity bone weights/bindposes, and use `SkinnedMeshRenderer` where a valid skin is declared. This enables the existing procedural pose fallback to affect skinned geometry; it does not add authored animation clips.
- The inspected model bank has no authored UV coordinates and no embedded texture/image records. The loader now creates planar fallback UVs and applies cached procedural tiled surface textures by asset/material category. These are runtime-generated placeholder materials, not hand-authored PBR texture sets.
- Collider bounds now include `Renderer`/`SkinnedMeshRenderer` bounds and convert bounds to root-local space, rather than relying only on `MeshFilter` objects.
- Repeated GLB loads now instantiate cached hidden templates, sharing mesh/material data rather than reparsing and rebuilding every repeated prop.

### Android asset loading
- Added 124 `.bytes` Resources TextAssets: 80 GLBs, 34 WAVs, and 10 mission JSON files. The copies were byte-compared against their source files.
- Runtime asset lookup now checks `Resources/PRLRuntime` first. The synchronous `while (!UnityWebRequest.isDone)` loop was removed. URL-based StreamingAssets without an embedded Resources copy now logs a clear error and returns instead of potentially blocking the Unity main thread.

### Mission gameplay
- M03 now has a real two-step relay C4 route and a mutually exclusive fuel-tank route that skips relay detonation after the alternate target is destroyed.
- M04 escort NPC now has follow behavior, a health/death state, obstacle avoidance, and can be damaged by enemy fire when it crosses the line of fire. Losing the informant fails the mission; escort completion checks the actor has reached the dock area.
- M05 target truck charges create visible planted-charge props and target destruction spawns a brief blast effect.
- M06 manifold charges are recorded only when planted. The player must plant all configured charges and be outside the configured safe radius to detonate. E9 technicians can disarm a newly planted charge during the configured window, returning the objective to that charge for replanting.
- M08 now attaches a lightweight cargo-truck driving controller to the spawned cargo vehicle. It uses the mobile joystick/keyboard axes, aligns to the delivery route, blocks movement when a forward sphere cast finds an obstacle, and keeps the first-person camera attached while driving. This is simplified arcade vehicle movement, not full wheel/suspension simulation.

## Static verification performed
- Mission Anti-Cheat v3 validator: 10/10 JSON files pass.
- Character asset validator: 24/24 pass.
- Weapon asset validator: 26/26 pass.
- Audio validator: 34 WAV assets pass.
- Mission mechanic contracts: 17/17 pass.
- Consolidated release checks: 31/31 pass.
- C# delimiter sanity check: 28 source files, no unmatched delimiters.
- GLB parse: 80/80 valid files; 206 primitives; 72 skinned primitive records; zero authored UV-bearing primitives and zero embedded texture/image records.
- Resources byte sync: all 124 `.bytes` payloads match their source files.
- ZIP integrity checked after packaging.

## Not claimed / still requires Unity runtime verification
- Unity Editor/compiler and an Android device were not available in this environment. No Unity compile, Play Mode run, APK build, or device test is claimed.
- No original authored animation clips are present in the GLB bank. Skinned rendering and procedural posing are now wired, but authored locomotion, reload, recoil, and takedown animations remain absent.
- Procedural runtime textures are placeholders; detailed authored albedo/normal/metallic/roughness maps and visual art review remain absent.
- Vehicle movement is a simplified controller, not a complete simulated vehicle with suspension, wheel colliders, drivetrain, vehicle damage, or a separate driving HUD.
- Static checks cannot prove mission completion end-to-end, NavMesh quality, collision feel, performance, thermal behavior, memory usage, or Android stability.
- The project is not certified as visually or mechanically superior to IGI 1. That requires a rendered comparison and runtime gameplay testing.

## How to run the checks
From the Unity project root:

```bash
python3 validate_levels_v3.py M01.json M02.json M03.json M04.json M05.json M06.json M07.json M08.json M09.json M10.json
python3 validate_characters.py Assets/_Project/Art/Characters
python3 validate_weapons.py Assets/_Project/Art/Weapons
python3 validate_audio.py Assets/_Project/Audio
python3 validate_consolidated_mission_mechanics.py
python3 validate_consolidated_release.py
```

## Release stance
RC1.16 is a substantive consolidated source update and static-validated project package. It is **not** a runtime-verified final game. A final release designation must wait until Unity compilation, Play Mode campaign tests, Android build, and device gameplay tests pass.
