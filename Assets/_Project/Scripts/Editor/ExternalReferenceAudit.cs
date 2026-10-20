#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Static Unity Editor audit for asset and integration readiness.
/// Does not modify scenes or project assets; writes a text report beside the Unity project.
/// </summary>
public static class ExternalReferenceAudit
{
    [MenuItem("Project Real Loser/External Reference Audit/Generate Project Report")]
    public static void GenerateReport()
    {
        var report = new StringBuilder();
        report.AppendLine("PROJECT REAL LOSER — EXTERNAL REFERENCE / ASSET READINESS AUDIT");
        report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine("Unity version: " + Application.unityVersion);
        report.AppendLine("Scope: static AssetDatabase inspection only; this is not a runtime or Android test.");
        report.AppendLine(new string('=', 76));

        AddAssetSection(report, "Scenes", "t:SceneAsset");
        AddAssetSection(report, "Prefabs", "t:Prefab");
        AddAssetSection(report, "Materials", "t:Material");
        AddAssetSection(report, "Animation clips", "t:AnimationClip");
        AddAssetSection(report, "Animator controllers", "t:AnimatorController");
        AddAssetSection(report, "Textures", "t:Texture2D");
        AddAssetSection(report, "Audio clips", "t:AudioClip");
        AddAssetSection(report, "Models / meshes", "t:Model");

        report.AppendLine("\nBUILD SETTINGS SCENES");
        var buildScenes = EditorBuildSettings.scenes;
        report.AppendLine("Configured scene count: " + buildScenes.Length);
        foreach (var scene in buildScenes)
            report.AppendLine((scene.enabled ? "[ENABLED] " : "[DISABLED] ") + scene.path);

        report.AppendLine("\nPREFAB MISSING-SCRIPT CHECK");
        int prefabCount = 0, prefabMissingScripts = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            prefabCount++;
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            if (missing > 0)
            {
                prefabMissingScripts += missing;
                report.AppendLine(path + " — missing scripts: " + missing);
            }
        }
        report.AppendLine("Prefabs inspected: " + prefabCount);
        report.AppendLine("Missing-script components found: " + prefabMissingScripts);
        if (prefabMissingScripts == 0) report.AppendLine("No missing scripts found on prefab root objects; nested prefab/scene instances still require review.");

        report.AppendLine("\nTEXTURE DIMENSION SUMMARY");
        int textureCount = 0, over2k = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;
            textureCount++;
            if (tex.width > 2048 || tex.height > 2048) over2k++;
        }
        report.AppendLine("Readable/imported Texture2D assets: " + textureCount);
        report.AppendLine("Textures with width or height > 2048: " + over2k);

        report.AppendLine("\nLIMITATIONS / NEXT TESTS");
        report.AppendLine("- Does not compile scripts, enter Play Mode, evaluate Animator transitions, or build APKs.");
        report.AppendLine("- Does not prove that texture maps are assigned to the correct shader slots.");
        report.AppendLine("- Does not prove character retargeting, animation quality, mission completion, AI behavior, or performance.");
        report.AppendLine("- Run Unity Test Runner, open each mission scene, then build and test on target Android hardware.");

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(projectRoot, "PRL_EXTERNAL_REFERENCE_AUDIT.txt");
        File.WriteAllText(output, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log("Project Real Loser audit written to: " + output);
        EditorUtility.RevealInFinder(output);
    }

    private static void AddAssetSection(StringBuilder report, string label, string filter)
    {
        var paths = AssetDatabase.FindAssets(filter)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        report.AppendLine("\n" + label.ToUpperInvariant() + ": " + paths.Length);
        foreach (var path in paths) report.AppendLine("  " + path);
    }
}
#endif
