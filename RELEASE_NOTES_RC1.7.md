# Project Real Loser RC1.7 — Quality Audit Tool

## Added
- Unity Editor menu command: `Real Loser > Quality Audit > Generate Project Audit Report`.
- Static AssetDatabase report for model files, texture files, material files, prefabs, scenes, scripts, imported animation clips, and SkinnedMeshRenderer components.
- Lists model imports with no discovered animation clips and reports enabled/disabled scenes in Editor Build Settings.
- Writes `ProjectRealLoser_QualityAudit.txt` at the Unity project root.

## Scope and limitations
- This is a static inspection aid; it does not compile the project, enter Play Mode, verify missing references, assess visual quality, or build/test Android.
- The report must be generated inside Unity Editor to reflect the imported asset database for that Unity version.
- A zero texture count is a warning to investigate, not proof that every rendered object is untextured; procedural materials and external/runtime-loaded assets may not appear as imported texture files.
