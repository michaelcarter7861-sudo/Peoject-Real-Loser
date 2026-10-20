#!/usr/bin/env python3
"""Static contracts for mission-specific runtime mechanics; does not replace Unity play-mode tests."""
import json, pathlib, sys
ROOT = pathlib.Path(__file__).resolve().parent
SRC = (ROOT / 'Assets/_Project/Scripts/Core/MissionObjectiveRuntime.cs').read_text(encoding='utf-8')
checks = []
def check(name, condition):
    checks.append((name, bool(condition)))

for mid in [f'M{i:02d}' for i in range(1, 11)]:
    data = json.loads((ROOT / f'{mid}.json').read_text(encoding='utf-8'))
    check(f'{mid}: objectives/extraction data present', bool(data.get('objectives', {}).get('primary')) and bool(data.get('objectives', {}).get('extraction', {}).get('pos')))

m03 = json.loads((ROOT/'M03.json').read_text(encoding='utf-8'))['objectives']['primary'][0]
check('M03 relay and alternate fuel target ids are declared', bool(m03.get('target_id')) and bool(m03.get('alternate_target_id')))
m05 = json.loads((ROOT/'M05.json').read_text(encoding='utf-8'))
struct_ids = {x.get('id') for x in m05.get('structures', [])}
vehicles = m05['objectives']['primary'][0].get('target_vehicles', [])
check('M05 all declared C4 vehicle targets exist as structures', bool(vehicles) and all(v in struct_ids for v in vehicles))
m04 = json.loads((ROOT/'M04.json').read_text(encoding='utf-8'))['objectives']['primary'][0]
check('M04 hostage spawn and follow runtime source exists', bool(m04.get('hostage_id')) and 'CreateOrActivateHostage' in SRC and 'MissionEscortActor' in SRC and 'followTarget' in (ROOT / 'Assets/_Project/Scripts/Core/MissionEscortActor.cs').read_text(encoding='utf-8'))
m06 = json.loads((ROOT/'M06.json').read_text(encoding='utf-8'))['objectives']['primary'][0]
check('M06 three manifold charge targets and safety distance exist', len(m06.get('manifolds', [])) == 3 and m06.get('min_safe_detonation_distance_m', 0) >= 50 and 'CHARGES DETONATED FROM SAFE DISTANCE' in SRC)
check('M05 plant/detonate state and visual target destruction source exists', 'Plant C4 on cargo truck' in SRC and 'Detonate C4 on cargo truck' in SRC and 'FindSceneObjectByName(targetToDestroy)' in SRC)
check('M09 sniper kill gate and timed uplink source exists', 'requiredEnemyIds = o.required_snipers' in SRC and 'hack_duration_sec' in SRC and 'ELITE SNIPERS REMAINING' in SRC)
check('M08 truck/checkpoint/delivery data and objective steps exist', all(k in SRC for k in ['Reach and secure the cargo vehicle', 'Pass convoy checkpoint Alpha', 'Pass convoy checkpoint Bravo', 'Deliver cargo at the destination']))

for name, ok in checks:
    print(f"[{'PASS' if ok else 'FAIL'}] {name}")
if not all(ok for _, ok in checks): sys.exit(1)
print(f'[PASS] {len(checks)} consolidated mission mechanics contracts')
print('[NOTE] Static contracts do not prove Unity compilation, scene object availability, navigation, or Android runtime behavior.')
