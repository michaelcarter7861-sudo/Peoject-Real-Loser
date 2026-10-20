#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using RealLoserStudio.Tactical.Core;
using RealLoserStudio.Tactical.Player;
using RealLoserStudio.Tactical.Stealth;
using RealLoserStudio.Tactical.Weapons;
using RealLoserStudio.Tactical.UI;
using RealLoserStudio.Tactical.Streaming;

namespace RealLoser.Editor
{
    /// <summary>
    /// Unity Editor Batchmode & Menu Builder (`Assets/_Project/Editor/RealLoserProjectBuilder.cs`).
    /// Exposes:
    /// - Menu Item: `Tools/Real Loser/Build All (Android 9+ Universal APK)`
    /// - CLI Entry Point: `-executeMethod RealLoser.Editor.RealLoserProjectBuilder.BuildAndroidAPK`
    /// </summary>
    public static class RealLoserProjectBuilder
    {
        private static readonly string[] MissionIds = new string[]
        {
            "M01", "M02", "M03", "M04", "M05", "M06", "M07", "M08", "M09", "M10"
        };

        [MenuItem("Tools/Real Loser/Build All (Android 9+ Universal APK)")]
        public static void BuildAllMenu()
        {
            BuildAndroidAPK();
        }

        [MenuItem("Tools/Real Loser/Sync StreamingAssets & Verify Scenes")]
        public static void SyncAndGenerateScenesOnly()
        {
            SyncAllStreamingAssets();
            EnsureAllCampaignScenesExist(forceRegenerate: false);
            ConfigureAndroidPlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RealLoserProjectBuilder] Synced StreamingAssets and verified all 12 campaign scenes.");
        }

        [MenuItem("Tools/Real Loser/Force Regenerate All 12 Scenes")]
        public static void ForceRegenerateScenesMenu()
        {
            SyncAllStreamingAssets();
            EnsureAllCampaignScenesExist(forceRegenerate: true);
            ConfigureAndroidPlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RealLoserProjectBuilder] Regenerated all 12 campaign scenes.");
        }

        public static void BuildAndroidAPK()
        {
            Debug.Log("====================================================================");
            Debug.Log("[RealLoserProjectBuilder] Starting Android 9+ (ARM64 + ARMv7) APK Build");
            Debug.Log("====================================================================");

            SyncAllStreamingAssets();
            ConfigureAndroidPlayerSettings();
            List<string> scenePaths = EnsureAllCampaignScenesExist(forceRegenerate: false);

            EditorBuildSettingsScene[] buildScenes = new EditorBuildSettingsScene[scenePaths.Count];
            for (int i = 0; i < scenePaths.Count; i++)
            {
                buildScenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            }
            EditorBuildSettings.scenes = buildScenes;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android");
            Directory.CreateDirectory(buildDir);
            string apkOutputPath = Path.Combine(buildDir, "ProjectRealLoser_v1.23_Android9.apk");

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenePaths.ToArray(),
                locationPathName = apkOutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string legacyPath = Path.Combine(buildDir, "ProjectRealLoser_v1.0.apk");
                if (File.Exists(apkOutputPath) && !string.Equals(apkOutputPath, legacyPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(apkOutputPath, legacyPath, true);
                }
                Debug.Log($"[RealLoserProjectBuilder] Android APK Build SUCCEEDED: {apkOutputPath} ({summary.totalSize} bytes)");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            else
            {
                Debug.LogError($"[RealLoserProjectBuilder] Android APK Build FAILED: {summary.result} (Errors: {summary.totalErrors})");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        public static void ConfigureAndroidPlayerSettings()
        {
            PlayerSettings.companyName = "Real Loser Studio";
            PlayerSettings.productName = "Project Real Loser";
            PlayerSettings.bundleVersion = "1.2.3";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.realloserstudio.projectrealloser");

            Texture2D launcherIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/UI/ProjectRealLoser_Icon.png");
            if (launcherIcon != null)
            {
                int[] iconSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Android);
                int slotCount = (iconSizes != null && iconSizes.Length > 0) ? iconSizes.Length : 1;
                Texture2D[] iconArray = new Texture2D[slotCount];
                for (int i = 0; i < slotCount; i++)
                {
                    iconArray[i] = launcherIcon;
                }
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, iconArray);
            }

            string splashPath = "Assets/_Project/Art/UI/ProjectRealLoser_Splash.jpg";
            TextureImporter splashImporter = AssetImporter.GetAtPath(splashPath) as TextureImporter;
            if (splashImporter != null && (splashImporter.textureType != TextureImporterType.Sprite || splashImporter.spriteImportMode != SpriteImportMode.Single))
            {
                splashImporter.textureType = TextureImporterType.Sprite;
                splashImporter.spriteImportMode = SpriteImportMode.Single;
                splashImporter.SaveAndReimport();
            }
            Sprite splashSprite = AssetDatabase.LoadAssetAtPath<Sprite>(splashPath);
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.025f, 0.04f, 0.035f, 1f);
            if (splashSprite != null)
            {
                PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[]
                {
                    PlayerSettings.SplashScreenLogo.Create(2.5f, splashSprite)
                };
            }

            // Android 9 (API 28 Pie) + modern Android compatibility:
            // Support both 32-bit (ARMv7) and 64-bit (ARM64) Android 9 devices, OpenGL ES 3.0+, and auto target SDK.
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.optimizedFramePacing = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.openGLRequireES31 = false;
            PlayerSettings.openGLRequireES31AEP = false;
            PlayerSettings.openGLRequireES32 = false;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        public static void SyncAllStreamingAssets()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string streamingRoot = Path.Combine(Application.dataPath, "StreamingAssets");
            Directory.CreateDirectory(streamingRoot);

            string missionsDest = Path.Combine(streamingRoot, "Missions");
            Directory.CreateDirectory(missionsDest);
            for (int i = 0; i < MissionIds.Length; i++)
            {
                string mFile = MissionIds[i] + ".json";
                string srcA = Path.Combine(Application.dataPath, "_Project", "Missions", mFile);
                string srcB = Path.Combine(projectRoot, mFile);
                string dst = Path.Combine(missionsDest, mFile);
                if (File.Exists(srcA)) File.Copy(srcA, dst, true);
                else if (File.Exists(srcB)) File.Copy(srcB, dst, true);
            }

            string[] artSubfolders = new string[] { "Characters", "Weapons", "Traps", "Environment" };
            for (int i = 0; i < artSubfolders.Length; i++)
            {
                string sub = artSubfolders[i];
                string srcDir = Path.Combine(Application.dataPath, "_Project", "Art", sub);
                string dstDir = Path.Combine(streamingRoot, "Art", sub);
                Directory.CreateDirectory(dstDir);
                if (Directory.Exists(srcDir))
                {
                    foreach (string glbPath in Directory.GetFiles(srcDir, "*.glb"))
                    {
                        File.Copy(glbPath, Path.Combine(dstDir, Path.GetFileName(glbPath)), true);
                    }
                }
            }

            string audioSrc = Path.Combine(Application.dataPath, "_Project", "Audio");
            string audioDst = Path.Combine(streamingRoot, "Audio");
            Directory.CreateDirectory(audioDst);
            if (Directory.Exists(audioSrc))
            {
                foreach (string wavPath in Directory.GetFiles(audioSrc, "*.wav"))
                {
                    File.Copy(wavPath, Path.Combine(audioDst, Path.GetFileName(wavPath)), true);
                }
                string manifestPath = Path.Combine(audioSrc, "audio_manifest.json");
                if (File.Exists(manifestPath))
                {
                    File.Copy(manifestPath, Path.Combine(audioDst, "audio_manifest.json"), true);
                }
            }
        }

        public static List<string> EnsureAllCampaignScenesExist(bool forceRegenerate = false)
        {
            string scenesFolder = "Assets/_Project/Scenes";
            string fullScenesDir = Path.Combine(Directory.GetCurrentDirectory(), scenesFolder);
            Directory.CreateDirectory(fullScenesDir);

            List<string> expectedPaths = new List<string>
            {
                scenesFolder + "/Boot.unity",
                scenesFolder + "/MainMenu.unity"
            };
            for (int i = 0; i < MissionIds.Length; i++)
            {
                expectedPaths.Add(scenesFolder + "/" + MissionIds[i] + ".unity");
            }

            if (!forceRegenerate)
            {
                bool allExist = true;
                for (int i = 0; i < expectedPaths.Count; i++)
                {
                    if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), expectedPaths[i])))
                    {
                        allExist = false;
                        break;
                    }
                }
                if (allExist) return expectedPaths;
            }

            return GenerateAllCampaignScenes();
        }

        public static List<string> GenerateAllCampaignScenes()
        {
            string scenesFolder = "Assets/_Project/Scenes";
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), scenesFolder));
            List<string> orderedScenePaths = new List<string>();

            // 1. Boot.unity
            string bootPath = scenesFolder + "/Boot.unity";
            Scene bootScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSceneCameraAndLight("BootCamera", new Color(0.04f, 0.06f, 0.08f));
            GameObject bootRoot = new GameObject("Boot_ControllerRoot");
            bootRoot.AddComponent<BootSceneController>();
            EditorSceneManager.SaveScene(bootScene, bootPath);
            orderedScenePaths.Add(bootPath);

            // 2. MainMenu.unity
            string menuPath = scenesFolder + "/MainMenu.unity";
            Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSceneCameraAndLight("MainMenuCamera", new Color(0.06f, 0.08f, 0.10f));
            CreateEventSystemObject();
            GameObject menuRoot = new GameObject("MainMenu_ControllerRoot");
            menuRoot.AddComponent<MainMenuController>();
            EditorSceneManager.SaveScene(menuScene, menuPath);
            orderedScenePaths.Add(menuPath);

            // 3. M01.unity - M10.unity
            for (int i = 0; i < MissionIds.Length; i++)
            {
                string mId = MissionIds[i];
                string mScenePath = scenesFolder + "/" + mId + ".unity";
                Scene mScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                CreateSceneCameraAndLight("Main Camera", new Color(0.10f, 0.14f, 0.12f));
                CreateEventSystemObject();

                GameObject bootstrapper = new GameObject("RuntimeMissionBootstrapper_" + mId);
                RuntimeMissionSpawner spawner = bootstrapper.AddComponent<RuntimeMissionSpawner>();
                spawner.missionId = mId;
                bootstrapper.AddComponent<TacticalWorldBuilder>();
                bootstrapper.AddComponent<TacticalPauseSettings>();

                AttachMissionSpecificInstaller(bootstrapper, mId);

                EditorSceneManager.SaveScene(mScene, mScenePath);
                orderedScenePaths.Add(mScenePath);
            }

            return orderedScenePaths;
        }

        private static void CreateSceneCameraAndLight(string camName, Color bgColor)
        {
            GameObject camObj = new GameObject(camName);
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bgColor;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 450f;
            camObj.AddComponent<AudioListener>();

            GameObject lightObj = new GameObject("Directional Light");
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            l.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateEventSystemObject()
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static void AttachMissionSpecificInstaller(GameObject root, string mId)
        {
            switch (mId)
            {
                case "M01": root.AddComponent<M01_BootCampSceneInstaller>(); break;
                case "M02": root.AddComponent<M02_DisavowedSceneInstaller>(); break;
                case "M03": root.AddComponent<M03_RadioSilenceSceneInstaller>(); break;
                case "M04": root.AddComponent<M04_WarehouseSceneInstaller>(); break;
                case "M05": root.AddComponent<M05_BridgeCrossingSceneInstaller>(); break;
                case "M06": root.AddComponent<M06_SandAndSteelSceneInstaller>(); break;
                case "M07": root.AddComponent<M07_PortAuthoritySceneInstaller>(); break;
                case "M08": root.AddComponent<M08_ConvoySceneInstaller>(); break;
                case "M09": root.AddComponent<M09_EyesAboveSceneInstaller>(); break;
                case "M10": root.AddComponent<M10_CompoundZeroSceneInstaller>(); break;
            }
        }
    }
}
#endif
