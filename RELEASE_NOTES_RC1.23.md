# Project Real Loser RC1.23 — Unity Compile Repair & Android 9 (Pie / API 28) Readiness Pass

## Summary of Fixes Applied in RC1.23

1. **Fixed C# Compile Error (`CS1061`) in `RuntimeMissionSpawner.cs` & Wired Trap Damage**:
   - Fixed `_spawnedTrapList[i].TickTrap(spawnedPlayer, dt)` calling a non-existent method on `TrapAndHazardSystem` (changed to `EvaluateTrapTick(spawnedPlayer, dt)` and added a `TickTrap` forwarding alias on `TrapAndHazardSystem`).
   - Wired `TrapAndHazardSystem.OnPlayerDamaged` to `PlayerDamageReceiver.ApplyDamage` so mines (`T2`–`T4`), sentry turrets (`T9`), electric fences (`T12`), booby-trapped doors (`T14`), guard dogs (`T15`), and environmental hazards (`T16`) inflict real player damage.

2. **Fixed Unity Editor Build Script Compile Error (`CS1729`) in `RealLoserProjectBuilder.cs`**:
   - Replaced invalid `new PlayerSettings.SplashScreenLogo(splashArt, 2.5f)` (`Texture2D` passed to non-existent 2-arg struct constructor) with `PlayerSettings.SplashScreenLogo.Create(2.5f, splashSprite)` and configured `ProjectRealLoser_Splash.jpg` importer as `Sprite` (`textureType: 8`, `spriteMode: 1`).
   - Populated all Android launcher icon slots required by `PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Android)`.

3. **Fixed Unity MonoScript Class-to-Filename Serialization Mismatch (`Boot.unity`, `MainMenu.unity`, & Runtime Components)**:
   - Extracted `BootSceneController` into `Assets/_Project/Scripts/Core/BootSceneController.cs` (matching `Boot.unity` GUID `b2c3d4e5f60718293a4b5c6d7e8f9001`) and `MainMenuController` into `Assets/_Project/Scripts/Core/MainMenuController.cs` (matching `MainMenu.unity` GUID `b2c3d4e5f60718293a4b5c6d7e8f9002`), while keeping `SceneFlowControllers.cs` as the shared static settings/font helper.
   - Extracted `HidingSpotContainer.cs`, `PowerSourceNode.cs`, and `ArticulatedWeaponRig.cs` into dedicated `.cs` files so every `MonoBehaviour` class matches its `.cs` filename 1:1.

4. **Configured Full Android 9 (Pie / API Level 28) Hardware & Graphics Compatibility**:
   - Enabled **Dual-ABI Universal Android Build (`ARM64` + `ARMv7`, `AndroidTargetArchitectures: 3`)** so both 32-bit (`armeabi-v7a`) and 64-bit (`arm64-v8a`) Android 9 devices can install and run the APK without `INSTALL_FAILED_NO_MATCHING_ABIS`.
   - Set **OpenGL ES 3.0 (`OpenGLES3`)** as the primary Android Graphics API (`openGLRequireES31: 0`, `androidUseSwappy: 0`) to prevent early Vulkan 1.0/1.1 driver black-screens and `libswappy.so` crashes on Android 9 GPUs.
   - Set `minSdkVersion = 24` (Android 7.0+, covering Android 9 API 28) and `targetSdkVersion = Auto` (`0`) so Unity builds cleanly with any installed Android SDK (28–34).
   - Removed the broken `"com.atteneder.gltfast": "6.0.1"` package entry from `Packages/manifest.json` (which caused UPM resolution failure without an OpenUPM registry).

5. **Fixed Shader Pipeline Fallback, Unity 2022.3 UI Font Loading, Multi-Story Missions (`M07`/`M10`), and Touch Controls**:
   - Updated `RuntimeGLBAndMeshLoader.GetSafeLitShader()`, `TacticalWorldBuilder`, and `MissionObjectiveRuntime` to check `GraphicsSettings.currentRenderPipeline != null` before choosing `"Universal Render Pipeline/Lit"`, defaulting to `"Standard"` / `"Mobile/Diffuse"` when running on the Built-in Render Pipeline so materials never render magenta/invisible.
   - Added `SceneFlowControllers.GetSafeBuiltinFont()` (`LegacyRuntime.ttf` with fallback to `Arial.ttf`) across all UI scripts (`MainMenuController`, `MobileTouchHUDAndMEMap`, `TacticalPauseSettings`, `PlayerDeathScreen`).
   - Fixed `MissionObjectiveRuntime.FindSceneObjectByName` prefix matching (`M02`, `M03`, `M07`), fixed 3-story building floor elevation math (`s.height / floors`) with walkable inter-floor ramps in `M07` and `M10`, and added auto-checkpoint progression when driving the `M08` cargo truck.
   - Upgraded `MobileTouchHUDAndMEMap` with press-and-hold directional buttons, analog drag virtual joystick, on-screen `JUMP` and `PAUSE` buttons, and dynamic 3D weapon viewmodel switching.
   - Generated all 319 previously missing `.meta` files across `Assets/` so every folder, texture, audio file, JSON blueprint, and script has deterministic Unity metadata.
