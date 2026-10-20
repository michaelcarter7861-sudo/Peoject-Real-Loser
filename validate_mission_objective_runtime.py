#!/usr/bin/env python3
"""Static checks for the Unity mission objective flow and mission-specific gates."""
import glob, json, os, sys
ROOT = os.path.dirname(__file__)
SUPPORTED = ('target_pos','cell_pos','checkpoint_pos','boss_pos','truck_pos','uplink_pos','delivery_pos','extraction_pos','alternate_pos','manifolds')
errors=[]
mission_data={}
for path in sorted(glob.glob(os.path.join(ROOT,'M[0-9][0-9].json'))):
    with open(path,encoding='utf-8') as f: data=json.load(f)
    mission=data.get('mission_id',os.path.basename(path)[:3])
    mission_data[mission]=data
    objectives=data.get('objectives') or {}
    primary=objectives.get('primary') or []
    extraction=objectives.get('extraction') or {}
    if not primary: errors.append(f'{mission}: no primary objectives')
    if not extraction.get('pos') or len(extraction['pos']) < 2: errors.append(f'{mission}: missing extraction coordinates')
    targets=0
    for obj in primary:
        for key in SUPPORTED:
            value=obj.get(key)
            if key=='manifolds' and value:
                targets += sum(1 for m in value if m.get('pos') and len(m['pos'])>=2)
            elif isinstance(value,list) and len(value)>=2 and all(isinstance(v,(int,float)) for v in value[:2]):
                targets += 1
    if targets == 0: errors.append(f'{mission}: no objective target coordinates supported by runtime')
    else: print(f'[PASS] {mission}: {targets} coordinate-bearing objective target(s), extraction configured')

def first_primary(mid):
    return (mission_data.get(mid,{}).get('objectives',{}).get('primary') or [{}])[0]
# Mission-specific contracts the runtime must implement.
if not first_primary('M03').get('alternate_pos'): errors.append('M03: alternate relay/fuel-tank path missing')
if len(first_primary('M06').get('manifolds') or []) != 3: errors.append('M06: expected three manifold charges')
if len(first_primary('M09').get('required_snipers') or []) != 5: errors.append('M09: expected five required sniper IDs')
if not first_primary('M09').get('hack_duration_sec'): errors.append('M09: uplink hack duration missing')
if not first_primary('M08').get('checkpoint_alpha_x') or not first_primary('M08').get('checkpoint_bravo_x'): errors.append('M08: convoy checkpoint coordinates missing')
# Verify code integration and required guardrails.
checks = {
 'Assets/_Project/Scripts/Core/MissionObjectiveRuntime.cs': ['TryInteract()', 'RestartMission()', 'MISSION COMPLETE', 'RequiredEnemiesNeutralized', '_chargesDetonated', '_safeDetonationDistance', 'holdDuration', 'alternatePosition'],
 'Assets/_Project/Scripts/Core/RuntimeMissionSpawner.cs': ['BuildMissionObjectiveRuntime()', 'missionFlow.Initialize'],
 'Assets/_Project/Scripts/UI/MobileTouchHUDAndMEMap.cs': ['BtnObjectiveInteract', 'BtnRestartMission', 'OnObjectiveInteract']
}
for rel,needles in checks.items():
    with open(os.path.join(ROOT,rel),encoding='utf-8') as f: text=f.read()
    for needle in needles:
        if needle not in text: errors.append(f'{rel}: missing integration token {needle}')
if errors:
    print('\n'.join('[FAIL] '+e for e in errors)); sys.exit(1)
print('[PASS] mission objective static integration checks')
print('[PASS] M03 alternate route, M06 three-charge safety gate, M08 checkpoint coordinates, M09 five-sniper/hack contract')
print('[NOTE] Static checks do not prove Unity compilation or runtime behavior.')
