#!/usr/bin/env python3
"""Broad static Unity-project integrity gate; does not replace Unity Editor/device testing."""
import json, pathlib, re, sys
ROOT=pathlib.Path(__file__).resolve().parent
checks=[]
def check(name, ok, detail=''):
    checks.append((name, bool(ok), detail))

# Build settings must point at existing scenes and exact scene meta GUIDs.
settings=(ROOT/'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf-8')
scene_entries=re.findall(r'- enabled: (\d+)\s+path: (Assets/[^\n]+\.unity)\s+guid: ([0-9a-f]{32})', settings)
scene_ok=bool(scene_entries)
for enabled, rel, guid in scene_entries:
    p=ROOT/rel; meta=pathlib.Path(str(p)+'.meta')
    if enabled=='1' and not p.is_file(): scene_ok=False
    if not meta.is_file() or not re.search(r'^guid: '+re.escape(guid)+r'$', meta.read_text(encoding='utf-8'), re.M): scene_ok=False
check(f'Build Settings scenes exist and GUIDs match ({len(scene_entries)} entries)', scene_ok)
check('Boot and MainMenu scenes are in build order', len(scene_entries)>=2 and scene_entries[0][1].endswith('/Boot.unity') and scene_entries[1][1].endswith('/MainMenu.unity'))

# Stable script metadata: all C# files must have MonoImporter metadata and unique GUIDs.
cs_files=list((ROOT/'Assets').rglob('*.cs'))
script_meta_ok=True; script_guids=[]
for p in cs_files:
    mp=pathlib.Path(str(p)+'.meta')
    if not mp.is_file(): script_meta_ok=False; continue
    txt=mp.read_text(encoding='utf-8')
    g=re.search(r'^guid: ([0-9a-f]{32})$',txt,re.M)
    if not g or 'MonoImporter:' not in txt: script_meta_ok=False
    else: script_guids.append(g.group(1))
check(f'All {len(cs_files)} C# scripts have stable MonoImporter metadata',script_meta_ok)
check('C# script GUIDs are unique',len(script_guids)==len(set(script_guids)))

# Required project scripts and runtime payload banks.
required=[
 'Assets/_Project/Scripts/Core/RuntimeMissionSpawner.cs',
 'Assets/_Project/Scripts/Core/MissionObjectiveRuntime.cs',
 'Assets/_Project/Scripts/Core/RuntimeGLBAndMeshLoader.cs',
 'Assets/_Project/Scripts/Player/PlayerTacticalController.cs',
 'Assets/_Project/Scripts/Player/PlayerDamageReceiver.cs',
 'Assets/_Project/Scripts/Weapons/WeaponAndBallisticsSystem.cs',
 'Assets/_Project/Scripts/AI/EnemyAIFSMSystem.cs',
 'Assets/_Project/Scripts/Stealth/StealthAndDetectionSystem.cs',
 'Assets/_Project/Scripts/UI/MobileTouchHUDAndMEMap.cs',
 'Assets/_Project/Scripts/UI/TacticalPauseSettings.cs',
 'Assets/_Project/Scripts/UI/PlayerDeathScreen.cs',
]
check('Core gameplay script set present',all((ROOT/p).is_file() for p in required))
resources=ROOT/'Assets/Resources/PRLRuntime'
mission_payloads={p.stem for p in (resources/'Missions').glob('*.bytes')} if (resources/'Missions').exists() else set()
check('Ten mission payloads bundled in Resources',mission_payloads=={f'M{i:02}' for i in range(1,11)})
check('Runtime audio payload bank present',len(list((resources/'Audio').glob('*.bytes')))>=30 if (resources/'Audio').exists() else False)
check('Runtime GLB payload bank present',len(list((resources/'Art').rglob('*.bytes')))>=40 if (resources/'Art').exists() else False)

# JSON and embedded runtime mission payloads must agree on parseability and mission identity.
mission_ok=True
for i in range(1,11):
    name=f'M{i:02}'
    try:
        d=json.loads((ROOT/f'{name}.json').read_text(encoding='utf-8'))
        payload=json.loads((resources/'Missions'/f'{name}.bytes').read_text(encoding='utf-8'))
        if not d.get('objectives',{}).get('primary') or not d.get('objectives',{}).get('extraction',{}).get('pos'): mission_ok=False
        if payload.get('mission_id') and payload['mission_id']!=name: mission_ok=False
    except Exception:
        mission_ok=False
check('Campaign JSON and runtime payloads parse for all ten missions',mission_ok)

# Source hygiene and key bug-fix regressions.
def source(rel): return (ROOT/rel).read_text(encoding='utf-8')
ai=source('Assets/_Project/Scripts/AI/EnemyAIFSMSystem.cs')
stealth=source('Assets/_Project/Scripts/Stealth/StealthAndDetectionSystem.cs')
pause=source('Assets/_Project/Scripts/UI/TacticalPauseSettings.cs')
check('Enemy AI retries noise subscription when scene initialization order changes','RefreshNoiseSubscription();' in ai and 'RemoveNoiseSubscription();' in ai)
check('Stealth singleton safely clears itself and guards duplicates','if (Instance == this) Instance = null;' in stealth and 'Multiple StealthAndDetectionSystem instances' in stealth)
check('Pause settings sensitivity slider uses correct normalized mapping','Mathf.InverseLerp(0.25f, 2f, savedSensitivity)' in pause and 'PlayerPrefs.Save()' in pause)
check('Pause overlay restores prior time scale on resume','previousTimeScale' in pause and 'Time.timeScale=previousTimeScale' in pause)
check('Audio and graphics settings persist across scenes','PRL_MasterVolume' in pause and 'PRL_GraphicsQuality' in pause and 'PlayerPrefs.Save()' in pause)
scene_flow=source('Assets/_Project/Scripts/Core/SceneFlowControllers.cs')
check('Boot applies saved audio and graphics settings','PRL_MasterVolume' in scene_flow and 'PRL_GraphicsQuality' in scene_flow)
death=source('Assets/_Project/Scripts/UI/PlayerDeathScreen.cs')
spawner=source('Assets/_Project/Scripts/Core/RuntimeMissionSpawner.cs')
check('Player death has retry/menu overlay wired to health event','OnPlayerDied += Show' in death and 'MISSION FAILED' in death and 'RETRY MISSION' in death)
check('Mission spawner attaches death screen to spawned player','PlayerDeathScreen>()' in spawner and 'AddComponent<RealLoserStudio.Tactical.UI.PlayerDeathScreen>()' in spawner)

# Every C# source must have balanced delimiters (token-level approximation only).
def balanced(s):
    s=re.sub(r'@?"(?:""|\\.|[^"\\])*"','""',s)
    s=re.sub(r"'(?:\\.|[^'\\])*'","''",s)
    s=re.sub(r'/\*.*?\*/',' ',s,flags=re.S); s=re.sub(r'//[^\n]*',' ',s)
    stack=[]; pair={')':'(',']':'[','}':'{'}
    for ch in s:
        if ch in '([{': stack.append(ch)
        elif ch in ')]}':
            if not stack or stack.pop()!=pair[ch]: return False
    return not stack
check(f'C# delimiter sanity ({len(cs_files)} files)',bool(cs_files) and all(balanced(p.read_text(encoding='utf-8')) for p in cs_files))

for name,ok,detail in checks:
    print(f"[{'PASS' if ok else 'FAIL'}] {name}"+(f' — {detail}' if detail else ''))
print(f'OVERALL: {sum(ok for _,ok,_ in checks)}/{len(checks)} static checks passed')
print('LIMIT: This script cannot compile Unity C#, import GLB assets, execute Play Mode, or validate Android hardware behavior.')
sys.exit(0 if all(ok for _,ok,_ in checks) else 1)
