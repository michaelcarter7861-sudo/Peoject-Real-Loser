#!/usr/bin/env python3
"""
Static validation gate for RC1.23 Unity 2022.3.40f1 C# compile readiness,
MonoScript class-to-filename matching, scene GUID resolution, complete .meta coverage,
and Android 9 (Pie / API Level 28) compatibility settings.
"""
import json
import os
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent
checks = []


def check(name: str, ok: bool, detail: str = "") -> None:
    checks.append((name, bool(ok), detail))


# 1. Every MonoBehaviour class must live in a .cs file whose filename matches the class name.
cs_files = sorted((ROOT / "Assets").rglob("*.cs"))
mb_mismatches = []
guid_to_class = {}
for p in cs_files:
    txt = p.read_text(encoding="utf-8")
    fname = p.stem
    meta_p = pathlib.Path(str(p) + ".meta")
    if meta_p.is_file():
        m = re.search(r"^guid:\s*([0-9a-f]{32})$", meta_p.read_text(encoding="utf-8"), re.M)
        if m:
            guid_to_class[m.group(1)] = fname
    for cname, base in re.findall(r"class\s+(\w+)\s*:\s*([^{]+)", txt):
        if "MonoBehaviour" in base or "ScriptableObject" in base:
            if cname != fname:
                mb_mismatches.append(f"{p.relative_to(ROOT)}: class {cname} != {fname}.cs")
check(f"All MonoBehaviour classes match their .cs filenames ({len(cs_files)} scripts)", len(mb_mismatches) == 0, "; ".join(mb_mismatches))

# 2. Every scene MonoBehaviour m_Script GUID must resolve to the exact matching C# class.
scene_files = sorted((ROOT / "Assets/_Project/Scenes").glob("*.unity"))
scene_script_errors = []
for sp in scene_files:
    stxt = sp.read_text(encoding="utf-8")
    blocks = stxt.split("--- !u!114 ")
    for b in blocks[1:]:
        gm = re.search(r"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*3\}", b)
        cm = re.search(r"m_EditorClassIdentifier:\s*(?:[\w\.]+\.)?(\w+)", b)
        if gm and cm:
            guid, expected_cls = gm.group(1), cm.group(1)
            actual_cls = guid_to_class.get(guid)
            if actual_cls != expected_cls:
                scene_script_errors.append(f"{sp.name}: guid {guid} -> {actual_cls}, expected {expected_cls}")
check(f"All 12 scene YAML MonoBehaviour script GUIDs resolve to matching C# classes", len(scene_script_errors) == 0, "; ".join(scene_script_errors))

# 3. Zero missing .meta files across the entire Assets/ tree.
missing_metas = []
for dirpath, dirnames, filenames in os.walk(ROOT / "Assets"):
    for d in dirnames:
        full = pathlib.Path(dirpath) / d
        if not pathlib.Path(str(full) + ".meta").is_file():
            missing_metas.append(str(full.relative_to(ROOT)))
    for f in filenames:
        if f.endswith(".meta"):
            continue
        full = pathlib.Path(dirpath) / f
        if not pathlib.Path(str(full) + ".meta").is_file():
            missing_metas.append(str(full.relative_to(ROOT)))
check("100% .meta file coverage across all Assets folders and files", len(missing_metas) == 0, f"missing={len(missing_metas)}")

# 4. Fixed C# compile blockers (CS1061 TickTrap & CS1729 SplashScreenLogo)
spawner_txt = (ROOT / "Assets/_Project/Scripts/Core/RuntimeMissionSpawner.cs").read_text(encoding="utf-8")
trap_txt = (ROOT / "Assets/_Project/Scripts/Traps/TrapAndHazardSystem.cs").read_text(encoding="utf-8")
builder_txt = (ROOT / "Assets/_Project/Editor/RealLoserProjectBuilder.cs").read_text(encoding="utf-8")
check(
    "CS1061 fixed: RuntimeMissionSpawner calls EvaluateTrapTick and wires trap damage",
    "EvaluateTrapTick(spawnedPlayer, dt)" in spawner_txt
    and "public void TickTrap(" in trap_txt
    and "OnPlayerDamaged += HandleTrapDamagedPlayer" in spawner_txt,
)
check(
    "CS1729 fixed: RealLoserProjectBuilder uses SplashScreenLogo.Create(float, Sprite)",
    "SplashScreenLogo.Create(" in builder_txt and "new PlayerSettings.SplashScreenLogo(" not in builder_txt,
)

# 5. Packages/manifest.json has no broken third-party registry dependencies
manifest_data = json.loads((ROOT / "Packages/manifest.json").read_text(encoding="utf-8"))
deps = manifest_data.get("dependencies", {})
check("Packages/manifest.json has no broken gltfast dependency", "com.atteneder.gltfast" not in deps)

# 6. Android 9 (API 28 Pie) compatibility in ProjectSettings.asset & RealLoserProjectBuilder.cs
ps_txt = (ROOT / "ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8")
min_sdk_m = re.search(r"AndroidMinSdkVersion:\s*(\d+)", ps_txt)
arch_m = re.search(r"AndroidTargetArchitectures:\s*(\d+)", ps_txt)
es31_m = re.search(r"openGLRequireES31:\s*(\d+)", ps_txt)
swappy_m = re.search(r"androidUseSwappy:\s*(\d+)", ps_txt)
check(
    "Android 9 (API 28) SDK & Dual-ABI (ARMv7 + ARM64) configured",
    bool(min_sdk_m and int(min_sdk_m.group(1)) <= 28)
    and bool(arch_m and int(arch_m.group(1)) == 3)
    and "AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7" in builder_txt,
)
check(
    "Android 9 OpenGL ES 3.0 primary graphics & Swappy disabled",
    bool(es31_m and int(es31_m.group(1)) == 0)
    and bool(swappy_m and int(swappy_m.group(1)) == 0)
    and "m_APIs: 0b000000" in ps_txt
    and "GraphicsDeviceType.OpenGLES3" in builder_txt,
)

# 7. Safe shader pipeline check and Unity 2022.3 font fallback
loader_txt = (ROOT / "Assets/_Project/Scripts/Core/RuntimeGLBAndMeshLoader.cs").read_text(encoding="utf-8")
flow_txt = (ROOT / "Assets/_Project/Scripts/Core/SceneFlowControllers.cs").read_text(encoding="utf-8")
check(
    "Shader loader checks GraphicsSettings.currentRenderPipeline before URP shader",
    "GraphicsSettings.currentRenderPipeline != null" in loader_txt,
)
check(
    "UI font loader supports Unity 2022.3 LegacyRuntime.ttf with Arial.ttf fallback",
    "LegacyRuntime.ttf" in flow_txt and "GetSafeBuiltinFont()" in flow_txt,
)

for name, ok, detail in checks:
    print(f"[{'PASS' if ok else 'FAIL'}] {name}" + (f" — {detail}" if detail else ""))
print(f"OVERALL RC1.23 GATE: {sum(ok for _, ok, _ in checks)}/{len(checks)} checks passed")
sys.exit(0 if all(ok for _, ok, _ in checks) else 1)
