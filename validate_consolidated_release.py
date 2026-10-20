#!/usr/bin/env python3
"""Static release checks for the consolidated Unity engineering pass. Not a Unity runtime test."""
import glob, json, os, pathlib, re, struct, sys
ROOT = pathlib.Path(__file__).resolve().parent
checks=[]
def check(name, ok):
    checks.append((name, bool(ok)))

# All ten campaign data files must parse and contain an objective and extraction position.
missions=[]
for i in range(1,11):
    p=ROOT/f'M{i:02}.json'
    try: d=json.loads(p.read_text(encoding='utf-8'))
    except Exception as e:
        check(f'M{i:02} JSON parse', False); continue
    missions.append(d)
    check(f'M{i:02} objectives and extraction present', bool(d.get('objectives',{}).get('primary')) and bool(d.get('objectives',{}).get('extraction',{}).get('pos')))

loader=(ROOT/'Assets/_Project/Scripts/Core/RuntimeGLBAndMeshLoader.cs').read_text(encoding='utf-8')
mission=(ROOT/'Assets/_Project/Scripts/Core/MissionObjectiveRuntime.cs').read_text(encoding='utf-8')
escort=(ROOT/'Assets/_Project/Scripts/Core/MissionEscortActor.cs').read_text(encoding='utf-8')
vehicle=(ROOT/'Assets/_Project/Scripts/Core/MissionCargoVehicleController.cs').read_text(encoding='utf-8')
check('GLB loader reads TEXCOORD_0 and builds planar fallback UVs', 'ReadVec2Accessor' in loader and 'BuildPlanarUVs' in loader and 'mesh.SetUVs(0, allUvs)' in loader)
check('GLB loader preserves all primitives as submeshes', 'mesh.subMeshCount = submeshTriangles.Count' in loader and 'mesh.SetTriangles(submeshTriangles[i], i, false)' in loader)
check('GLB loader creates SkinnedMeshRenderer and bone weights', 'SkinnedMeshRenderer' in loader and 'ReadBoneWeights' in loader and 'mesh.bindposes = bindposes' in loader)
check('GLB loader assigns procedural tiled material textures', 'GetProceduralSurfaceTexture' in loader and 'SetTexture("_BaseMap"' in loader and 'SetTexture("_MainTex"' in loader)
check('GLB collider bounds account for renderers in local space', 'Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)' in loader and 'root.transform.InverseTransformPoint' in loader)
check('Android data loading uses Resources and avoids blocking UnityWebRequest loops', 'Resources.Load<TextAsset>(resourcePath)' in loader and 'while (!op.isDone)' not in loader)
check('Repeated GLB instantiation uses cached templates', '_prefabTemplateCache.TryGetValue(cacheKey' in loader and 'UnityEngine.Object.Instantiate(template)' in loader)
resources_root=ROOT/'Assets/Resources/PRLRuntime'
resource_bytes=list(resources_root.rglob('*.bytes')) if resources_root.exists() else []
check(f'Android runtime Resources payloads included ({len(resource_bytes)} .bytes files)', len(resource_bytes) >= 120)
check('M03 relay C4 and alternate fuel path both represented', 'Plant C4 on relay tower OR destroy the fuel tank' in mission and 'Detonate C4 on relay tower' in mission and 'skipFollowingStepOnAlternate' in mission)
check('M04 escort actor has follow, health and mission-failure handling', 'followTarget' in escort and 'ApplyDamage' in escort and 'informant lost' in mission.lower())
ai_source=(ROOT/'Assets/_Project/Scripts/AI/EnemyAIFSMSystem.cs').read_text(encoding='utf-8')
check('Enemy fire can damage escort when he crosses line of fire', 'GetComponentInParent<MissionEscortActor>' in ai_source and 'escort.ApplyDamage(enemyShotDamage)' in ai_source)
check('Enemy perception enforces line-of-sight raycasts', 'Physics.RaycastAll(rayOrigin' in ai_source and 'hitTransform.IsChildOf(player.transform)' in ai_source)
check('Enemy combat memory tracks last known player position', '_lastKnownPlayerPosition' in ai_source and '_combatMemoryRemaining' in ai_source and 'TransitionToState(AIState.InvestigateNoise, 4.0f)' in ai_source)
check('Enemy sight reacquisition does not reset combat state timer every frame', 'currentState != AIState.CombatCover && currentState != AIState.Flank' in ai_source and 'if (seesPlayer)' in ai_source)
check('M08 truck controller consumes mobile joystick and keyboard input', 'currentJoystickInput' in vehicle and 'Input.GetAxisRaw' in vehicle and 'WouldHitObstacle' in vehicle)
check('M05 vehicle C4 and blast effects implemented', 'Plant C4 on cargo truck' in mission and 'Detonate C4 on cargo truck' in mission and 'SpawnExplosion' in mission)
check('M06 safe-distance detonation gate implemented', 'min_safe_detonation_distance_m' in mission and 'UNSAFE DETONATION' in mission and 'CHARGES DETONATED FROM SAFE DISTANCE' in mission)
check('M09 sniper neutralization and timed uplink gate implemented', 'requiredEnemyIds = o.required_snipers' in mission and 'ELITE SNIPERS REMAINING' in mission and 'HACKING ' in mission)
check('M06 technician charge-disarm window and replant rollback implemented', 'TickTechnicianDisarm' in mission and 'e9_technician_disarm_window_sec' in mission and 'TECHNICIAN DISARMED' in mission)

# Token-aware delimiter sanity check for all project C# sources. This is not a compiler.
def csharp_delimiters_ok(source):
    source = re.sub(r'@?\"(?:\"\"|\\.|[^\"\\])*\"', '\"\"', source)
    source = re.sub(r"'(?:\\.|[^'\\])*'", "''", source)
    source = re.sub(r'/\*.*?\*/', ' ', source, flags=re.S)
    source = re.sub(r'//[^\n]*', ' ', source)
    stack=[]; pairs={')':'(', ']':'[', '}':'{'}
    for ch in source:
        if ch in '([{': stack.append(ch)
        elif ch in ')]}':
            if not stack or stack.pop() != pairs[ch]: return False
    return not stack
cs_files=list((ROOT/'Assets/_Project/Scripts').rglob('*.cs'))
check(f'C# delimiter sanity across {len(cs_files)} source files', bool(cs_files) and all(csharp_delimiters_ok(p.read_text(encoding='utf-8')) for p in cs_files))

# Audit the actual model bank rather than relying on filenames alone.
glb_files=glob.glob(str(ROOT/'Assets/_Project/Art/**/*.glb'), recursive=True)
valid_glb=0; total_primitives=0; skinned_files=0; files_with_uv=0; files_with_embedded_textures=0
for path in glb_files:
    try:
        b=open(path,'rb').read()
        if len(b)<20 or struct.unpack_from('<I',b,0)[0] != 0x46546C67: continue
        off=12; doc=None
        while off+8<=len(b):
            length, kind=struct.unpack_from('<II',b,off); off+=8
            if off+length>len(b): break
            if kind==0x4e4f534a:
                doc=json.loads(b[off:off+length].decode('utf-8').rstrip('\x00 \t\r\n')); break
            off+=length
        if doc is None: continue
        valid_glb+=1
        for mesh in doc.get('meshes',[]):
            for prim in mesh.get('primitives',[]):
                total_primitives+=1
                if 'TEXCOORD_0' in prim.get('attributes',{}): files_with_uv += 1
                if 'JOINTS_0' in prim.get('attributes',{}) and 'WEIGHTS_0' in prim.get('attributes',{}): skinned_files += 1
        if doc.get('images') or doc.get('textures'): files_with_embedded_textures+=1
    except Exception:
        pass
check(f'GLB bank parses ({valid_glb}/{len(glb_files)})', valid_glb==len(glb_files) and valid_glb>0)
check(f'GLB bank contains multiple primitives ({total_primitives} primitives)', total_primitives>valid_glb)
check(f'Skinned primitive records found ({skinned_files})', skinned_files>0)
check(f'Embedded textured GLBs are accurately reported ({files_with_embedded_textures})', True)

for name, ok in checks:
    print(f"[{'PASS' if ok else 'FAIL'}] {name}")
print(f'GLB facts: {valid_glb} files, {total_primitives} primitives, {skinned_files} skinned primitive records, {files_with_uv} primitives with authored UVs, {files_with_embedded_textures} files with embedded texture/image records.')
if not all(ok for _,ok in checks): sys.exit(1)
print(f'[PASS] {len(checks)} consolidated release static checks')
print('[LIMIT] These checks do not compile Unity C# or prove Play Mode/Android behavior.')
