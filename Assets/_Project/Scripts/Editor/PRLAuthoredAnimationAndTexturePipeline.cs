#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RealLoserStudio.Tactical.Editor
{
    /// <summary>Creates project-owned keyframed clips and applies correct texture import settings.</summary>
    [InitializeOnLoad]
    public sealed class PRLAuthoredAnimationAndTexturePipeline : IPreprocessBuildWithReport
    {
        private const string AnimationFolder = "Assets/Resources/PRLAnimations";
        private const string TextureFolder = "Assets/Resources/PRLTextures";
        public int callbackOrder => -1000;

        static PRLAuthoredAnimationAndTexturePipeline()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                    GenerateAnimationAssets(false);
                ConfigureTextureImports(false);
            };
        }

        [MenuItem("Real Loser/Art Pipeline/Generate Authored Character Clips and Configure Textures")]
        public static void GenerateAll()
        {
            GenerateAnimationAssets(true);
            ConfigureTextureImports(true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PRL] Authored animation clips generated and texture import settings configured.");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            GenerateAnimationAssets(false);
            ConfigureTextureImports(false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void GenerateAnimationAssets(bool force)
        {
            if (!AssetDatabase.IsValidFolder(AnimationFolder))
            {
                Directory.CreateDirectory(AnimationFolder);
                AssetDatabase.Refresh();
            }
            CreateClip("PRL_Idle", 2.4f, true, new PoseKind[] { PoseKind.Idle });
            CreateClip("PRL_Walk", 1.0f, true, new PoseKind[] { PoseKind.Walk });
            CreateClip("PRL_AimFire", 1.1f, true, new PoseKind[] { PoseKind.Aim });
            CreateClip("PRL_Reload", 1.45f, false, new PoseKind[] { PoseKind.Reload });
            CreateClip("PRL_Takedown", 0.9f, false, new PoseKind[] { PoseKind.Takedown });
        }

        private enum PoseKind { Idle, Walk, Aim, Reload, Takedown }

        private static void CreateClip(string name, float duration, bool loop, PoseKind[] kinds)
        {
            string path = AnimationFolder + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return; // Preserve hand-tuned edits after first generation.
            clip = new AnimationClip { name = name, legacy = true, frameRate = 30f, wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever };
            AddBoneCurve(clip, "RigRoot/Hips", "localPosition.y", duration, t => 0.95f + (kinds[0] == PoseKind.Walk ? 0.025f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) : 0f));
            switch (kinds[0])
            {
                case PoseKind.Idle:
                    AddRotation(clip, "RigRoot/Spine", 'y', duration, t => 1.5f * Mathf.Sin(t * Mathf.PI * 2f / duration));
                    AddRotation(clip, "RigRoot/Head", 'y', duration, t => 4f * Mathf.Sin(t * Mathf.PI * 2f / duration));
                    AddRotation(clip, "RigRoot/Chest", 'x', duration, t => 1.2f * Mathf.Sin(t * Mathf.PI * 4f / duration));
                    break;
                case PoseKind.Walk:
                    AddRotation(clip, "RigRoot/UpperLegL", 'x', duration, t => 27f * Mathf.Sin(t * Mathf.PI * 2f));
                    AddRotation(clip, "RigRoot/UpperLegR", 'x', duration, t => -27f * Mathf.Sin(t * Mathf.PI * 2f));
                    AddRotation(clip, "RigRoot/LowerLegL", 'x', duration, t => Mathf.Max(0f, -32f * Mathf.Sin(t * Mathf.PI * 2f)));
                    AddRotation(clip, "RigRoot/LowerLegR", 'x', duration, t => Mathf.Max(0f, 32f * Mathf.Sin(t * Mathf.PI * 2f)));
                    AddRotation(clip, "RigRoot/UpperArmL", 'y', duration, t => 18f * Mathf.Sin(t * Mathf.PI * 2f));
                    AddRotation(clip, "RigRoot/UpperArmR", 'y', duration, t => -18f * Mathf.Sin(t * Mathf.PI * 2f));
                    AddRotation(clip, "RigRoot/Chest", 'y', duration, t => 2f * Mathf.Sin(t * Mathf.PI * 2f));
                    break;
                case PoseKind.Aim:
                    AddRotation(clip, "RigRoot/Spine", 'x', duration, t => -7f);
                    AddRotation(clip, "RigRoot/Chest", 'x', duration, t => -9f + 1.5f * Mathf.Sin(t * Mathf.PI * 4f));
                    AddRotation(clip, "RigRoot/UpperArmL", 'y', duration, t => 68f);
                    AddRotation(clip, "RigRoot/UpperArmR", 'y', duration, t => -68f);
                    AddRotation(clip, "RigRoot/UpperArmL", 'z', duration, t => -22f);
                    AddRotation(clip, "RigRoot/UpperArmR", 'z', duration, t => 22f);
                    AddRotation(clip, "RigRoot/LowerArmL", 'y', duration, t => -12f);
                    AddRotation(clip, "RigRoot/LowerArmR", 'y', duration, t => 12f);
                    AddRotation(clip, "RigRoot/Head", 'y', duration, t => 1.5f * Mathf.Sin(t * Mathf.PI * 2f));
                    break;
                case PoseKind.Reload:
                    AddRotation(clip, "RigRoot/UpperArmL", 'z', duration, t => -22f - 18f * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI));
                    AddRotation(clip, "RigRoot/UpperArmR", 'z', duration, t => 24f + 12f * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI));
                    AddRotation(clip, "RigRoot/Chest", 'x', duration, t => -5f * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI));
                    break;
                case PoseKind.Takedown:
                    AddRotation(clip, "RigRoot/UpperLegL", 'x', duration, t => 65f * Mathf.Clamp01(t / duration));
                    AddRotation(clip, "RigRoot/UpperLegR", 'x', duration, t => 58f * Mathf.Clamp01(t / duration));
                    AddRotation(clip, "RigRoot/Spine", 'x', duration, t => 48f * Mathf.Clamp01(t / duration));
                    AddRotation(clip, "RigRoot/Head", 'x', duration, t => 25f * Mathf.Clamp01(t / duration));
                    break;
            }
            AnimationEvent[] events = new AnimationEvent[0];
            clip.events = events;
            AssetDatabase.CreateAsset(clip, path);
        }

        private static void AddRotation(AnimationClip clip, string path, char axis, float duration, Func<float, float> value)
        {
            string prop = "localEulerAnglesRaw." + axis;
            AddBoneCurve(clip, path, prop, duration, value);
        }

        private static void AddBoneCurve(AnimationClip clip, string path, string property, float duration, Func<float, float> value)
        {
            AnimationCurve curve = new AnimationCurve();
            int samples = 30;
            for (int i = 0; i <= samples; i++)
            {
                float t = duration * i / samples;
                curve.AddKey(new Keyframe(t, value(t), 0f, 0f));
            }
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), property);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static void ConfigureTextureImports(bool force)
        {
            if (!AssetDatabase.IsValidFolder(TextureFolder)) return;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                bool normal = file.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase);
                bool mask = file.EndsWith("_Mask", StringComparison.OrdinalIgnoreCase);
                TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                bool srgb = !normal && !mask;
                bool changed = importer.textureType != type || importer.sRGBTexture != srgb || importer.mipmapEnabled != !mask || importer.wrapMode != TextureWrapMode.Repeat;
                if (changed || force)
                {
                    importer.textureType = type;
                    importer.sRGBTexture = srgb;
                    importer.mipmapEnabled = !mask;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 8;
                    importer.maxTextureSize = 1024;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
#endif
