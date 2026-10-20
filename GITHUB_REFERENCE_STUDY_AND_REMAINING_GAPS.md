# Project Real Loser — GitHub Reference Study & Remaining Gaps

**Prepared:** 2026-10-20  
**Baseline inspected:** RC1.17 Authored Animations & Textures  
**Purpose:** Map the user's reference repositories to concrete engineering work. This document is a source/repository study, not a claim that the Unity project has compiled or run.

## Executive conclusion

RC1.17 is still a source-and-assets project that needs an actual Unity Editor import/compile and gameplay validation pass. The repository references are useful as patterns, but they are not all drop-in dependencies. Two user-supplied GitHub URLs currently return 404 at the supplied paths: `Whinarn/UnityOpenStealth` and `kinifi/Godot-FPS-Template`. Do not invent content from those unavailable repositories; use the surviving alternatives noted below only as inspiration.

## Reference review

### 1. Unity Technologies — FPS Sample
- URL: https://github.com/Unity-Technologies/FPSSample
- What it teaches: separation of game systems, weapon/player architecture, level authoring and build tooling. The project describes itself as a complete first-person multiplayer shooter sample.
- Important caveat: it targets Unity 2018.3.8f1, HDRP, ECS/hybrid patterns, Windows client and Linux server; it is no longer actively maintained. It is multiplayer-only and has no single-player mode. It is therefore a design/code reference, not a project to merge wholesale into this mobile, single-player tactical game.
- Apply selectively: weapon lifecycle/state boundaries, reproducible build/level tooling, explicit game-state transitions, and structured debug/validation tooling.

### 2. Unity Open Stealth — supplied URL unavailable
- URL supplied: https://github.com/Whinarn/UnityOpenStealth
- Current check: GitHub returned 404 at this path. Its contents cannot responsibly be treated as read or verified.
- Replacement study direction: implement the same general stealth concepts directly in the current project: vision cone + distance + occlusion raycast, suspicion accumulation/decay, last-known-position search, noise investigation, alarm propagation, camera detection, and patrol graph traversal. Keep the implementation project-native and testable rather than copying an unavailable repository.

### 3. Godot FPS Template — supplied URL unavailable
- URL supplied: https://github.com/kinifi/Godot-FPS-Template
- Current check: GitHub returned 404 at this path. The engine is also different: Godot GDScript/nodes are not drop-in Unity C# components.
- Alternative inspected: https://github.com/bukkbeek/GodotFPS-Template describes modular player movement, ADS, hitscan weapons, ammo/health, vehicles and a test level, but explicitly calls itself work in progress and prohibits resale of its originating assets. Use only architecture ideas; do not import its assets or code without a deliberate licensing/porting decision.
- Apply selectively: input abstraction, weapon interface, health/ammo ownership, vehicle controller separation and a minimal test scene.

### 4. Project I.G.I. Toolkit
- URL: https://github.com/heaven-hm/project-igi-toolkit
- What it teaches: mission editing, object position/orientation, texture replacement, patrol-pattern reuse, and graph/node traversal. These are useful references for IGI-like mission structure and navigation graph tooling.
- Apply selectively: explicit patrol waypoints/graphs, mission metadata editor, objective route visualization, consistent object transform tooling, and per-mission validation.
- Boundary: this is a toolkit for the original game's data. Treat it as a design/format reference only; do not copy proprietary IGI assets, maps, textures, audio, or executable content into Project Real Loser.

### 5. Tencent Hunyuan3D-2 and Hunyuan3D-2.1
- URLs: https://github.com/Tencent-Hunyuan/Hunyuan3D-2 and https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1
- What they do: image-conditioned mesh generation and texture generation; 2.1 focuses on PBR texture synthesis.
- Resource constraint: the 2.1 README lists approximately 10 GB VRAM for shape, 21 GB for texture and 29 GB for shape + texture together. The current build environment has no NVIDIA GPU (`nvidia-smi` returned no device), so local model inference cannot be claimed or run here.
- Licensing: Hunyuan has a dedicated community license and third-party notices. Review the exact current license and notice before distributing generated model assets; do not assume MIT terms.
- Best use: create a small, curated asset batch on a suitable GPU environment, then manually check topology, scale, UVs, material maps, collisions and mobile poly/texture budgets before importing into Unity.

### 6. Stability AI Stable Fast 3D
- URL: https://github.com/Stability-AI/stable-fast-3d
- What it does: single-image mesh reconstruction with UV unwrapping, texture and material prediction; exports GLB.
- Constraints: the README reports around 6 GB VRAM for one image and model access is gated through Hugging Face; Windows support is marked experimental. License is the Stability AI Community License, not a blanket MIT license.
- Best use: props and static environment objects (crates, terminals, generators, barrels, furniture, small machinery). Validate GLB scale, UVs, normals, material maps, triangles and silhouettes before accepting output.

### 7. TripoSR
- URL: https://github.com/VAST-AI-Research/TripoSR
- What it does: fast single-image 3D reconstruction; optional baked texture output; GLB export. README notes roughly 6 GB VRAM for default inference.
- License: repository states MIT for source, pretrained model and demo. Preserve license/attribution requirements and verify the exact asset output policy at use time.
- Best use: fast blockout/static props and alternate silhouettes, not final animated humanoids or production-ready large architecture without cleanup.

### 8. Dream Textures for Blender
- URL: https://github.com/carson-katri/dream-textures
- What it does: Stable Diffusion texture/concept generation, seamless textures and scene texturing workflow inside Blender.
- Constraint: the repository is GPL-3.0. Treat it as an external content-creation tool, not code to embed into the Unity game. Check license obligations if redistributing modified addon code.
- Best use: generate concept sheets and seamless base-color ideas; then author/check PBR base color, normal and metallic/roughness/AO maps, tiling, texel density and color consistency in Blender before export.

## RC1.17 baseline facts rechecked from the local project folder

- 32 C# source files are present under the project Assets tree.
- 27 PNG texture files exist under `Assets/Resources/PRLTextures`.
- No `.anim`, `.controller`, `.mat`, or `.prefab` files were found in the inspected project folder. Therefore RC1.17's authored animation pipeline is source code that is expected to generate animation clips in the Unity Editor, not already-imported `.anim` assets. That generation path remains unverified until Editor execution.
- The current environment has no detected NVIDIA GPU, so Hunyuan3D / SF3D / TripoSR model inference was not performed here.
- Unity Editor/runtime compilation was not available for this review. Static file checks must not be represented as Unity compilation or in-device gameplay testing.

## Consolidated engineering backlog (do not split into cosmetic micro-releases)

### P0 — Unity project opens, compiles and builds
1. Open/import with the exact supported Unity version; capture the full Console log.
2. Run editor-generation commands, verify `.anim` outputs actually appear, and ensure `.meta` GUIDs are stable.
3. Fix every compile error, missing package, missing reference and shader/material import warning.
4. Create a reproducible command-line build and retain its log; then test a development Android build on a device.

### P0 — Runtime asset pipeline
1. Verify all GLB primitives, materials, UVs, skinned mesh weights, bind poses, skeleton transforms and bounds in Unity, not only in a custom parser.
2. Verify texture importer settings: normal maps marked as Normal Map, color space, compression, mipmaps, wrap mode, filtering and platform overrides.
3. Confirm every spawned enemy/weapon/object has appropriate collider, layer, material and lifecycle handling.
4. Add prefab/material/controller assets where they improve deterministic setup; avoid relying on fragile runtime heuristics for all content.

### P0 — Complete single-player mission loop
1. One common game-state machine: briefing -> active -> paused -> success/failure -> debrief/restart.
2. Mission-specific objective state with prerequisites, optional objectives, objective fail conditions, checkpoint/restart rules and extraction gates.
3. Verify every M01–M10 from scene start to completion and failure. JSON schema validation alone is insufficient.
4. Implement and test hostage escort (pathing, health, follow/stop, rescue failure), vehicle/convoy driving and destruction, planted charges/timers/blast radius, technician disarm, sniper/uplink timed objective, boss/target kill confirmation, and all extraction routes.

### P0 — Player/combat
1. CharacterController/camera/input ownership and collision tests on mobile aspect ratios.
2. Weapon states (ready/firing/reloading/switching), magazine/reserve, hit detection, damage ownership, muzzle/audio/VFX, recoil and melee constraints.
3. Player health/death, mission fail, restart without duplicate managers/events; enemy health/death and cleanup.
4. Test LOS occlusion, own-collider exclusion, cover, friendly/self damage policy, hit layers, range falloff and shotgun pellet behavior.

### P1 — Stealth AI and navigation
1. Use Unity AI Navigation/NavMesh (or a verified project-native graph) for patrol, chase, investigation and return routes; validate reachability per mission.
2. Vision should combine FOV, range, height/obstacles, raycast occlusion, crouch/sprint modifiers and suspicion build/decay.
3. Hearing should use emitted noise events with type/radius and investigate last heard position.
4. Add last-known-player position, search timeout, alarm escalation, guard-to-guard alert, camera/searchlight/sensor alarm logic, and debug gizmos.
5. Add deterministic Play Mode tests for each state transition; do not infer AI quality from source structure alone.

### P1 — Art pipeline and visual quality
1. Generate or author environment kit assets (walls, corners, doors, window frames, roofs, fences, sandbags, industrial modules, terrain and vegetation) with clean pivots/colliders and LODs.
2. Build 3–5 high-value hero props first with SF3D/Hunyuan/TripoSR as appropriate, manually retopologize/UV-check, then reuse as modular pieces.
3. Use consistent PBR texture sets, trim sheets/atlases, terrain layers, decals, lightmaps and baked/real-time lighting choices appropriate for Android.
4. Add authored character locomotion/aim/fire/reload/death clips or verified generated clips, then retarget and inspect actual playback. Procedural posing alone is not a replacement.
5. Optimize for mobile: texture budgets, mesh LODs, batching/instancing, shadow distance, baked lighting, occlusion, reduced overdraw and profiler measurements.

### P1 — Mission authoring and test tooling
1. Mission editor/data schema with validated object IDs, objective dependencies, spawn points, extraction, patrol graph and encounter budgets.
2. Editor gizmos to visualize routes, vision cones, noise ranges, objective markers, interaction radii and navmesh reachability.
3. Automated Edit Mode and Play Mode tests for mission completion/failure/restart and all major systems.
4. A per-mission QA matrix that records tested Unity version, device, frame rate, crashes and screenshots/video captured from the actual game.

### P2 — Release quality
1. Main menu/mission selection/settings/pause/HUD/minimap/audio subtitles and input rebinding checks.
2. Device performance target and profiling on at least one low/mid-range Android device.
3. Asset license inventory and third-party notices.
4. Final APK only after build + smoke tests + all mission flows pass; keep the Unity project as the source of truth.

## Suggested implementation order

**Do not add more isolated cosmetic features until the P0 gate passes.** First run Unity import/compile and repair all errors; second verify the asset pipeline and animation generation; third stabilize common player/combat/mission state; fourth complete each mission's unique mechanics; fifth improve stealth/navigation; sixth upgrade art with a curated AI-assisted pipeline; finally profile and test Android. Keep one master checklist and only call a feature `Pass` after an executable test, not merely after checking that source files exist.

## Sources

- Unity FPS Sample: https://github.com/Unity-Technologies/FPSSample
- Unity Open Stealth supplied URL (404 at review time): https://github.com/Whinarn/UnityOpenStealth
- Godot FPS template supplied URL (404 at review time): https://github.com/kinifi/Godot-FPS-Template
- Alternative Godot FPS template: https://github.com/bukkbeek/GodotFPS-Template
- Project I.G.I. Toolkit: https://github.com/heaven-hm/project-igi-toolkit
- Hunyuan3D-2: https://github.com/Tencent-Hunyuan/Hunyuan3D-2
- Hunyuan3D-2.1: https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1
- Stable Fast 3D: https://github.com/Stability-AI/stable-fast-3d
- TripoSR: https://github.com/VAST-AI-Research/TripoSR
- Dream Textures: https://github.com/carson-katri/dream-textures
