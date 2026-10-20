#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RealLoserStudio.Tactical.Editor
{
    /// <summary>Evidence-based asset and build-configuration audit. Reports findings; does not claim runtime validation.</summary>
    public static class RealLoserQualityAudit
    {
        [MenuItem("Real Loser/Quality Audit/Generate Project Audit Report")]
        public static void GenerateReport()
        {
            string[] all = AssetDatabase.GetAllAssetPaths();
            var models = all.Where(p => p.StartsWith("Assets/") && (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))).ToArray();
            var textures = all.Where(p => p.StartsWith("Assets/") && IsTexture(p)).ToArray();
            var materials = all.Where(p => p.StartsWith("Assets/") && p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)).ToArray();
            var prefabs = all.Where(p => p.StartsWith("Assets/") && p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).ToArray();
            var scenes = all.Where(p => p.StartsWith("Assets/") && p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)).ToArray();
            var scripts = all.Where(p => p.StartsWith("Assets/") && p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToArray();
            int clips = 0, skinned = 0, unreadable = 0;
            var noClipModels = new StringBuilder();
            foreach (string path in models)
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                int localClips = assets.OfType<AnimationClip>().Count(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
                clips += localClips;
                skinned += assets.OfType<GameObject>().SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true)).Count();
                if (localClips == 0) noClipModels.AppendLine("- " + path);
                if (AssetDatabase.LoadMainAssetAtPath(path) == null) unreadable++;
            }
            var report = new StringBuilder();
            report.AppendLine("PROJECT REAL LOSER — UNITY ASSET QUALITY AUDIT");
            report.AppendLine("Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            report.AppendLine("Scope: static Unity AssetDatabase inspection only; no scene play, device test, or build was performed.");
            report.AppendLine();
            report.AppendLine("COUNTS");
            report.AppendLine("Model files: " + models.Length);
            report.AppendLine("Texture files: " + textures.Length);
            report.AppendLine("Material files: " + materials.Length);
            report.AppendLine("Prefab files: " + prefabs.Length);
            report.AppendLine("Scene files: " + scenes.Length);
            report.AppendLine("C# scripts: " + scripts.Length);
            report.AppendLine("Animation clips discovered inside model imports: " + clips);
            report.AppendLine("SkinnedMeshRenderer components discovered: " + skinned);
            report.AppendLine("Model assets not loadable by AssetDatabase: " + unreadable);
            report.AppendLine();
            report.AppendLine("MODEL FILES WITH NO IMPORTED ANIMATION CLIP");
            report.Append(noClipModels.Length == 0 ? "None detected.\n" : noClipModels.ToString());
            report.AppendLine();
            report.AppendLine("SCENES IN BUILD SETTINGS");
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                report.AppendLine((scene.enabled ? "[enabled] " : "[disabled] ") + scene.path);
            report.AppendLine();
            report.AppendLine("REVIEW NOTES");
            report.AppendLine("- Texture/material counts do not prove visual quality, UV correctness, or correct shader assignment.");
            report.AppendLine("- Imported animation count does not prove that gameplay controllers actually play the clips.");
            report.AppendLine("- This report does not validate C# compilation, missing runtime references, mission completion, performance, or Android compatibility.");
            string output = "ProjectRealLoser_QualityAudit.txt";
            string fullPath = Path.Combine(Application.dataPath, "..", output);
            File.WriteAllText(fullPath, report.ToString());
            AssetDatabase.Refresh();
            Debug.Log("Real Loser quality audit written to: " + Path.GetFullPath(fullPath));
            EditorUtility.RevealInFinder(Path.GetFullPath(fullPath));
        }

        private static bool IsTexture(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".tif" || ext == ".tiff" || ext == ".psd" || ext == ".bmp" || ext == ".exr" || ext == ".hdr";
        }
    }
}
#endif
