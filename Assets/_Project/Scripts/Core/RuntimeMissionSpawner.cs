using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RealLoserStudio.Tactical.Player;
using RealLoserStudio.Tactical.Stealth;
using RealLoserStudio.Tactical.Weapons;
using RealLoserStudio.Tactical.Traps;
using RealLoserStudio.Tactical.AI;
using RealLoserStudio.Tactical.Characters;
using RealLoserStudio.Tactical.UI;
using RealLoserStudio.Tactical.Streaming;

namespace RealLoserStudio.Tactical.Core
{
    /// <summary>
    /// Unified Runtime Mission JSON Spawner (`RuntimeMissionSpawner.cs`).
    /// Parses `StreamingAssets/Missions/MXX.json`, constructs the terrain & zone floor grid,
    /// spawns every structure, wall/fence segment, multi-story building, prop, cover, vegetation,
    /// point light, hiding spot, pickup, trap (`T1`–`T16` with `TrapAndHazardSystem`), security camera,
    /// enemy (`E1`–`E14` with `EnemyAIFSMSystem` & patrol waypoints), player (`PlayerTacticalController`
    /// with first-person `.glb` weapon viewmodel & `WeaponAndBallisticsSystem`), and interactive
    /// `MobileTouchHUDAndMEMap` (Virtual Joystick, Touch Look, Fire/Scope/Crouch/Prone/Lean/Interact buttons,
    /// and live Top-Down Satellite ME Map).
    /// </summary>
    public class RuntimeMissionSpawner : MonoBehaviour
    {
        [Serializable]
        public class MissionBlueprintData
        {
            public string mission_id;
            public string title;
            public int chapter;
            public string scene_name;
            public float width = 300f;
            public float height = 300f;
            public string biome = "forest";
            public string time_of_day = "day";
            public string weather = "clear";
            public PlayerStartData player_start;
            public ObjectiveBundleData objectives;
            public List<ZoneData> zones;
            public List<StructureData> structures;
            public List<PropData> props;
            public List<PropData> cover;
            public List<PropData> vegetation;
            public List<LightData> lights;
            public List<PropData> hiding_spots;
            public List<EnemyData> enemies;
            public List<TrapData> traps;
            public List<CameraData> cameras;
            public List<PickupData> pickups;
        }

        [Serializable]
        public class ObjectiveBundleData
        {
            public List<ObjectiveRecordData> primary;
            public ExtractionRecordData extraction;
        }

        [Serializable]
        public class ObjectiveRecordData
        {
            public string id;
            public string title;
            public float[] target_pos;
            public float[] alternate_pos;
            public float[] cell_pos;
            public float[] extraction_pos;
            public float[] checkpoint_pos;
            public float[] boss_pos;
            public float[] truck_pos;
            public float[] delivery_pos;
            public float[] uplink_pos;
            public List<ManifoldRecordData> manifolds;
        }

        [Serializable]
        public class ManifoldRecordData { public string id; public float[] pos; }
        [Serializable]
        public class ExtractionRecordData { public string id; public string title; public float[] pos; public float radius = 10f; }

        [Serializable]
        public class PlayerStartData
        {
            public float[] pos;
            public float facing_deg;
            public string[] starting_loadout;
        }

        [Serializable]
        public class ZoneData
        {
            public string id;
            public string name;
            public string zone_type;
            public float[] rect;
            public float[] bounds;
            public string description;
        }

        [Serializable]
        public class RoomData
        {
            public string id;
            public string name;
            public float[] bounds;
            public int floor = 1;
        }

        [Serializable]
        public class StructureData
        {
            public string id;
            public string type;
            public float[] pos;
            public float[] bounds;
            public float[] start;
            public float[] end;
            public float rot_y;
            public float height = 3.8f;
            public int floors = 1;
            public bool cuttable;
            public List<RoomData> rooms;
        }

        [Serializable]
        public class PropData
        {
            public string id;
            public string type;
            public string name;
            public float[] pos;
            public string zone;
            public string controls_camera_id;
        }

        [Serializable]
        public class LightData
        {
            public string id;
            public string type;
            public float[] pos;
            public float range_m = 14f;
        }

        [Serializable]
        public class EnemyData
        {
            public string id;
            public string asset_id;
            public string name;
            public string weapon;
            public int hp = 100;
            public bool has_radio;
            public bool radio;
            public bool sleeper;
            public float[] pos;
            public float elevation;
            public string zone;
        }

        [Serializable]
        public class TrapData
        {
            public string id;
            public string type;
            public string name;
            public float[] pos;
            public string zone;
            public float noise_radius_m = 40f;
            public float damage = 65f;
            public string linked_monitor_id;
            public string power_source_id;
        }

        [Serializable]
        public class CameraData
        {
            public string id;
            public string type;
            public float[] pos;
            public float yaw_min = 135f;
            public float yaw_max = 225f;
            public float fov_deg = 70f;
            public float range_m = 35f;
            public string monitor_id;
        }

        [Serializable]
        public class PickupData
        {
            public string id;
            public string type;
            public string name;
            public float[] pos;
            public string zone;
        }

        [Header("Mission Configuration")]
        public string missionId = "M01";
        public MissionBlueprintData loadedBlueprint;

        [Header("Runtime Systems")]
        public PlayerTacticalController spawnedPlayer;
        public StealthAndDetectionSystem stealthSystem;
        public WeaponAndBallisticsSystem weaponSystem;
        public MissionEventAndStreamingSystem streamingSystem;
        public MobileTouchHUDAndMEMap touchHud;
        public AudioSource globalSfxSource;

        private readonly Dictionary<string, TrapAndHazardSystem> _spawnedTrapsById = new Dictionary<string, TrapAndHazardSystem>();
        private readonly Dictionary<string, PowerSourceNode> _powerNodesById = new Dictionary<string, PowerSourceNode>();
        private readonly List<TrapAndHazardSystem> _spawnedTrapList = new List<TrapAndHazardSystem>();
        private readonly List<EnemyAIFSMSystem> _spawnedEnemies = new List<EnemyAIFSMSystem>();
        private Transform _worldRoot;
        private string _rawJsonText;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (streamingSystem != null)
            {
                streamingSystem.TickStreaming(dt);
            }
            if (spawnedPlayer != null)
            {
                for (int i = 0; i < _spawnedTrapList.Count; i++)
                {
                    if (_spawnedTrapList[i] != null && _spawnedTrapList[i].gameObject.activeInHierarchy)
                    {
                        _spawnedTrapList[i].EvaluateTrapTick(spawnedPlayer, dt);
                    }
                }
                for (int i = 0; i < _spawnedEnemies.Count; i++)
                {
                    if (_spawnedEnemies[i] != null && _spawnedEnemies[i].gameObject.activeInHierarchy)
                    {
                        _spawnedEnemies[i].TickFSM(spawnedPlayer, _spawnedEnemies, _spawnedTrapList, dt);
                    }
                }
            }
        }

        public void LoadAndSpawnMission(string targetMissionId)
        {
            missionId = targetMissionId;
            string relJsonPath = Path.Combine("Missions", missionId + ".json");
            _rawJsonText = RuntimeGLBAndMeshLoader.ReadStreamingOrProjectText(relJsonPath);
            if (string.IsNullOrEmpty(_rawJsonText))
            {
                _rawJsonText = RuntimeGLBAndMeshLoader.ReadStreamingOrProjectText(missionId + ".json");
            }

            if (string.IsNullOrEmpty(_rawJsonText))
            {
                Debug.LogError("[RuntimeMissionSpawner] Could not locate mission JSON for " + missionId);
                return;
            }

            loadedBlueprint = JsonUtility.FromJson<MissionBlueprintData>(_rawJsonText);
            if (loadedBlueprint == null)
            {
                Debug.LogError("[RuntimeMissionSpawner] Failed to parse JSON for " + missionId);
                return;
            }

            if (_worldRoot != null)
            {
                Destroy(_worldRoot.gameObject);
            }
            _worldRoot = new GameObject(missionId + "_WorldGeometry").transform;
            _worldRoot.SetParent(transform, false);

            SetupAtmosphereAndLighting();
            EnsureCoreSingletons();
            SpawnTerrainAndZones();
            SpawnStructuresAndWalls();
            SpawnPropsCoverAndVegetation();
            SpawnPointLightsAndPickups();
            SpawnTrapsAndCameras();
            SpawnEnemiesWithPatrols();
            SpawnOrPositionPlayer();
            BuildRuntimeMobileTouchHUD();
            BuildMissionObjectiveRuntime();

            Debug.Log($"[RuntimeMissionSpawner] Spawned {missionId} ('{loadedBlueprint.title}'): " +
                      $"Structures={loadedBlueprint.structures?.Count ?? 0}, Props={loadedBlueprint.props?.Count ?? 0}, " +
                      $"Traps={loadedBlueprint.traps?.Count ?? 0}, Enemies={loadedBlueprint.enemies?.Count ?? 0}");
        }

        private void HandleTrapDamagedPlayer(TrapAndHazardSystem trap, float damage)
        {
            if (spawnedPlayer == null || damage <= 0f) return;
            PlayerDamageReceiver receiver = spawnedPlayer.GetComponent<PlayerDamageReceiver>();
            if (receiver != null)
            {
                receiver.ApplyDamage(damage, trap != null ? trap.gameObject : null);
            }
        }

        public TrapAndHazardSystem GetTrapById(string trapId)
        {
            if (!string.IsNullOrEmpty(trapId) && _spawnedTrapsById.TryGetValue(trapId, out TrapAndHazardSystem t))
            {
                return t;
            }
            return null;
        }

        private void SetupAtmosphereAndLighting()
        {
            bool isNight = string.Equals(loadedBlueprint.time_of_day, "night", StringComparison.OrdinalIgnoreCase);
            bool isDusk = string.Equals(loadedBlueprint.time_of_day, "dusk", StringComparison.OrdinalIgnoreCase);
            bool isDesert = loadedBlueprint.biome != null && loadedBlueprint.biome.Contains("desert");

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            if (isNight)
            {
                RenderSettings.fogColor = new Color(0.05f, 0.08f, 0.12f, 1f);
                RenderSettings.ambientSkyColor = new Color(0.14f, 0.18f, 0.25f, 1f);
                RenderSettings.fogDensity = 0.0038f;
            }
            else if (isDusk)
            {
                RenderSettings.fogColor = new Color(0.58f, 0.36f, 0.26f, 1f);
                RenderSettings.ambientSkyColor = new Color(0.46f, 0.34f, 0.28f, 1f);
                RenderSettings.fogDensity = 0.0028f;
            }
            else if (isDesert)
            {
                RenderSettings.fogColor = new Color(0.82f, 0.72f, 0.56f, 1f);
                RenderSettings.ambientSkyColor = new Color(0.68f, 0.62f, 0.52f, 1f);
                RenderSettings.fogDensity = 0.0022f;
            }
            else
            {
                RenderSettings.fogColor = new Color(0.55f, 0.64f, 0.58f, 1f);
                RenderSettings.ambientSkyColor = new Color(0.45f, 0.52f, 0.48f, 1f);
                RenderSettings.fogDensity = 0.0032f;
            }

            Light dirLight = FindObjectOfType<Light>();
            if (dirLight == null || dirLight.type != LightType.Directional)
            {
                GameObject dlObj = new GameObject("DirectionalSunMoonLight");
                dlObj.transform.SetParent(_worldRoot, false);
                dirLight = dlObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.transform.rotation = Quaternion.Euler(isNight ? 35f : (isDusk ? 16f : 52f), -35f, 0f);
            dirLight.intensity = isNight ? 0.35f : (isDusk ? 0.75f : 1.15f);
            dirLight.color = isNight ? new Color(0.55f, 0.68f, 0.92f) : (isDusk ? new Color(1.0f, 0.68f, 0.46f) : new Color(1.0f, 0.96f, 0.88f));
            dirLight.shadows = LightShadows.Soft;
        }

        private void EnsureCoreSingletons()
        {
            stealthSystem = FindObjectOfType<StealthAndDetectionSystem>();
            if (stealthSystem == null)
            {
                GameObject stObj = new GameObject("StealthAndDetectionSystem");
                stObj.transform.SetParent(transform, false);
                stealthSystem = stObj.AddComponent<StealthAndDetectionSystem>();
            }

            streamingSystem = FindObjectOfType<MissionEventAndStreamingSystem>();
            if (streamingSystem == null)
            {
                GameObject strObj = new GameObject("MissionEventAndStreamingSystem");
                strObj.transform.SetParent(transform, false);
                streamingSystem = strObj.AddComponent<MissionEventAndStreamingSystem>();
            }

            if (FindObjectOfType<TacticalPauseSettings>() == null)
            {
                gameObject.AddComponent<TacticalPauseSettings>();
            }
            if (FindObjectOfType<TacticalWorldBuilder>() == null)
            {
                gameObject.AddComponent<TacticalWorldBuilder>();
            }
            globalSfxSource = GetComponent<AudioSource>();
            if (globalSfxSource == null)
            {
                globalSfxSource = gameObject.AddComponent<AudioSource>();
                globalSfxSource.spatialBlend = 0f;
            }
        }

        private void SpawnTerrainAndZones()
        {
            float w = Mathf.Max(100f, loadedBlueprint.width);
            float h = Mathf.Max(100f, loadedBlueprint.height);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TerrainBaseGrid";
            ground.transform.SetParent(_worldRoot, false);
            ground.transform.position = new Vector3(w * 0.5f, -0.25f, h * 0.5f);
            ground.transform.localScale = new Vector3(w + 40f, 0.5f, h + 40f);

            bool isDesert = loadedBlueprint.biome != null && (loadedBlueprint.biome.Contains("desert") || loadedBlueprint.biome.Contains("arid"));
            bool isUrban = loadedBlueprint.biome != null && (loadedBlueprint.biome.Contains("urban") || loadedBlueprint.biome.Contains("port") || loadedBlueprint.biome.Contains("industrial"));
            Color groundCol = isDesert ? new Color(0.68f, 0.58f, 0.42f, 1f) : (isUrban ? new Color(0.28f, 0.30f, 0.32f, 1f) : new Color(0.24f, 0.34f, 0.22f, 1f));
            ground.GetComponent<MeshRenderer>().sharedMaterial = RuntimeGLBAndMeshLoader.CreateSafeMaterial("TerrainMat", groundCol, 0.05f, 0.15f);

            if (loadedBlueprint.zones != null)
            {
                for (int i = 0; i < loadedBlueprint.zones.Count; i++)
                {
                    var z = loadedBlueprint.zones[i];
                    float[] b = (z.bounds != null && z.bounds.Length == 4) ? z.bounds : z.rect;
                    if (b == null || b.Length < 4) continue;
                    float zx = (b[0] + b[2]) * 0.5f;
                    float zz = (b[1] + b[3]) * 0.5f;
                    float zw = Mathf.Abs(b[2] - b[0]);
                    float zh = Mathf.Abs(b[3] - b[1]);

                    GameObject zSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    zSlab.name = "Zone_" + z.id + "_" + z.name;
                    zSlab.transform.SetParent(_worldRoot, false);
                    zSlab.transform.position = new Vector3(zx, 0.01f * (i + 1), zz);
                    zSlab.transform.localScale = new Vector3(zw, 0.02f, zh);

                    Color zCol = groundCol * 1.08f;
                    if (z.zone_type == "interior") zCol = new Color(0.36f, 0.38f, 0.40f, 1f);
                    else if (z.zone_type == "perimeter" || z.zone_type == "urban") zCol = new Color(0.32f, 0.33f, 0.35f, 1f);
                    zSlab.GetComponent<MeshRenderer>().sharedMaterial = RuntimeGLBAndMeshLoader.CreateSafeMaterial("ZoneMat_" + z.id, zCol, 0.1f, 0.25f);
                }
            }
        }

        private void SpawnStructuresAndWalls()
        {
            if (loadedBlueprint.structures == null) return;
            Transform structGroup = new GameObject("Structures").transform;
            structGroup.SetParent(_worldRoot, false);

            for (int i = 0; i < loadedBlueprint.structures.Count; i++)
            {
                var s = loadedBlueprint.structures[i];
                Vector3 pos = ExtractVector3(s.pos);

                if (s.start != null && s.start.Length >= 2 && s.end != null && s.end.Length >= 2)
                {
                    Vector3 p1 = new Vector3(s.start[0], pos.y, s.start[1]);
                    Vector3 p2 = new Vector3(s.end[0], pos.y, s.end[1]);
                    Vector3 mid = (p1 + p2) * 0.5f;
                    float segLen = Vector3.Distance(p1, p2);
                    float yaw = Mathf.Atan2(p2.z - p1.z, p2.x - p1.x) * Mathf.Rad2Deg;

                    string glbRel = Path.Combine("Art/Environment", s.type + ".glb");
                    GameObject wallObj = RuntimeGLBAndMeshLoader.InstantiateGLB(
                        glbRel,
                        mid,
                        Quaternion.Euler(0f, -yaw, 0f),
                        structGroup,
                        addCollider: true
                    );
                    wallObj.name = s.id;
                    if (segLen > 0.2f)
                    {
                        wallObj.transform.localScale = new Vector3(segLen / 4.0f, 1.0f, 1.0f);
                    }
                    streamingSystem.RegisterWorldObjectToTile(wallObj);
                }
                else if (s.bounds != null && s.bounds.Length == 4)
                {
                    GameObject bldgObj = BuildBuildingShellWithDoorways(s, structGroup);
                    streamingSystem.RegisterWorldObjectToTile(bldgObj);
                }
                else
                {
                    string glbRel = Path.Combine("Art/Environment", s.type + ".glb");
                    GameObject sObj = RuntimeGLBAndMeshLoader.InstantiateGLB(
                        glbRel,
                        pos,
                        Quaternion.Euler(0f, s.rot_y, 0f),
                        structGroup,
                        addCollider: true
                    );
                    sObj.name = s.id;
                    streamingSystem.RegisterWorldObjectToTile(sObj);
                }
            }
        }

        private GameObject BuildBuildingShellWithDoorways(StructureData s, Transform parent)
        {
            float minX = s.bounds[0];
            float minZ = s.bounds[1];
            float maxX = s.bounds[2];
            float maxZ = s.bounds[3];
            float bw = Mathf.Max(2f, maxX - minX);
            float bd = Mathf.Max(2f, maxZ - minZ);
            int floors = Mathf.Max(1, s.floors);
            float floorH = (floors > 1 && s.height > 6.0f) ? Mathf.Max(3.6f, s.height / floors) : Mathf.Max(3.6f, s.height);

            GameObject root = new GameObject(s.id + "_" + s.type);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);

            Material wallMat = RuntimeGLBAndMeshLoader.CreateSafeMaterial(s.id + "_WallMat", new Color(0.54f, 0.53f, 0.50f, 1f), 0.15f, 0.3f);
            Material floorMat = RuntimeGLBAndMeshLoader.CreateSafeMaterial(s.id + "_FloorMat", new Color(0.32f, 0.34f, 0.36f, 1f), 0.2f, 0.4f);

            for (int f = 0; f < floors; f++)
            {
                float yBase = f * floorH;
                GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "Floor_" + (f + 1);
                slab.transform.SetParent(root.transform, false);
                slab.transform.position = new Vector3((minX + maxX) * 0.5f, yBase + 0.1f, (minZ + maxZ) * 0.5f);
                slab.transform.localScale = new Vector3(bw, 0.2f, bd);
                slab.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

                Vector3[] pillarPositions = new Vector3[]
                {
                    new Vector3(minX + 0.4f, yBase + floorH * 0.5f, minZ + 0.4f),
                    new Vector3(maxX - 0.4f, yBase + floorH * 0.5f, minZ + 0.4f),
                    new Vector3(maxX - 0.4f, yBase + floorH * 0.5f, maxZ - 0.4f),
                    new Vector3(minX + 0.4f, yBase + floorH * 0.5f, maxZ - 0.4f),
                };
                for (int p = 0; p < pillarPositions.Length; p++)
                {
                    GameObject pil = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pil.name = "Pillar_" + f + "_" + p;
                    pil.transform.SetParent(root.transform, false);
                    pil.transform.position = pillarPositions[p];
                    pil.transform.localScale = new Vector3(0.8f, floorH, 0.8f);
                    pil.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
                }

                if (f < floors - 1)
                {
                    // Walkable tactical access ramp connecting floor f to floor f+1
                    float rampLen = Mathf.Sqrt(floorH * floorH + 9.0f * 9.0f);
                    float pitchDeg = -Mathf.Atan2(floorH, 9.0f) * Mathf.Rad2Deg;
                    GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ramp.name = "StairRamp_" + (f + 1) + "_to_" + (f + 2);
                    ramp.transform.SetParent(root.transform, false);
                    ramp.transform.position = new Vector3(minX + 3.5f + f * 4.0f, yBase + floorH * 0.5f, (minZ + maxZ) * 0.5f);
                    ramp.transform.rotation = Quaternion.Euler(pitchDeg, 0f, 0f);
                    ramp.transform.localScale = new Vector3(3.2f, 0.25f, rampLen);
                    ramp.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
                }
                else
                {
                    GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    roof.name = "Ceiling_" + (f + 1);
                    roof.transform.SetParent(root.transform, false);
                    roof.transform.position = new Vector3((minX + maxX) * 0.5f, yBase + floorH - 0.15f, (minZ + maxZ) * 0.5f);
                    roof.transform.localScale = new Vector3(bw, 0.3f, bd);
                    roof.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
                }
            }

            return root;
        }

        private void SpawnPropsCoverAndVegetation()
        {
            _powerNodesById.Clear();
            Transform propGroup = new GameObject("PropsAndCover").transform;
            propGroup.SetParent(_worldRoot, false);

            SpawnPropList(loadedBlueprint.props, propGroup, true);
            SpawnPropList(loadedBlueprint.cover, propGroup, true);
            SpawnPropList(loadedBlueprint.vegetation, propGroup, false);
            SpawnPropList(loadedBlueprint.hiding_spots, propGroup, true);
        }

        private void SpawnPropList(List<PropData> list, Transform parent, bool addCollider)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                Vector3 pos = ExtractVector3(p.pos);
                string glbRel = Path.Combine("Art/Environment", p.type + ".glb");
                GameObject obj = RuntimeGLBAndMeshLoader.InstantiateGLB(glbRel, pos, Quaternion.identity, parent, addCollider);
                obj.name = p.id + "_" + p.type;

                if (p.type == "prop_breaker_box_a" || p.type == "prop_generator_diesel_a")
                {
                    PowerSourceNode psn = obj.AddComponent<PowerSourceNode>();
                    psn.powerSourceId = p.id;
                    psn.assetType = p.type;
                    _powerNodesById[p.id] = psn;
                }
                else if (p.type == "prop_locker_metal_a" || p.type == "prop_dumpster_metal_a" || p.type.Contains("bush") || p.type.Contains("vent"))
                {
                    HidingSpotContainer hsc = obj.AddComponent<HidingSpotContainer>();
                    hsc.spotId = p.id;
                }

                streamingSystem.RegisterWorldObjectToTile(obj);
            }
        }

        private void SpawnPointLightsAndPickups()
        {
            if (loadedBlueprint.lights != null)
            {
                Transform lightGroup = new GameObject("TacticalLights").transform;
                lightGroup.SetParent(_worldRoot, false);
                for (int i = 0; i < loadedBlueprint.lights.Count; i++)
                {
                    var l = loadedBlueprint.lights[i];
                    GameObject lObj = new GameObject(l.id);
                    lObj.transform.SetParent(lightGroup, false);
                    lObj.transform.position = ExtractVector3(l.pos, 3.0f);
                    Light pt = lObj.AddComponent<Light>();
                    pt.type = LightType.Point;
                    pt.range = Mathf.Max(8f, l.range_m);
                    pt.intensity = 1.35f;
                    pt.color = new Color(1.0f, 0.88f, 0.68f);
                    streamingSystem.RegisterWorldObjectToTile(lObj);
                }
            }

            if (loadedBlueprint.pickups != null)
            {
                Transform pkpGroup = new GameObject("Pickups").transform;
                pkpGroup.SetParent(_worldRoot, false);
                for (int i = 0; i < loadedBlueprint.pickups.Count; i++)
                {
                    var pk = loadedBlueprint.pickups[i];
                    GameObject pkObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pkObj.name = pk.id + "_" + pk.type;
                    pkObj.transform.SetParent(pkpGroup, false);
                    pkObj.transform.position = ExtractVector3(pk.pos, 0.45f);
                    pkObj.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
                    pkObj.GetComponent<MeshRenderer>().sharedMaterial =
                        RuntimeGLBAndMeshLoader.CreateSafeMaterial("PickupMat_" + pk.id, new Color(0.18f, 0.78f, 0.38f, 1f), 0.4f, 0.7f);
                    streamingSystem.RegisterWorldObjectToTile(pkObj);
                }
            }
        }

        private void SpawnTrapsAndCameras()
        {
            _spawnedTrapsById.Clear();
            _spawnedTrapList.Clear();
            if (loadedBlueprint.traps == null) return;

            Transform trapGroup = new GameObject("TrapsAndCameras").transform;
            trapGroup.SetParent(_worldRoot, false);

            for (int i = 0; i < loadedBlueprint.traps.Count; i++)
            {
                var td = loadedBlueprint.traps[i];
                Vector3 pos = ExtractVector3(td.pos);
                string glbFile = MapTrapCodeToGlbName(td.type);
                GameObject trapObj = RuntimeGLBAndMeshLoader.InstantiateGLB(
                    Path.Combine("Art/Traps", glbFile),
                    pos,
                    Quaternion.identity,
                    trapGroup,
                    addCollider: true
                );
                trapObj.name = td.id + "_" + td.type;

                TrapAndHazardSystem ths = trapObj.AddComponent<TrapAndHazardSystem>();
                ths.trapId = td.id;
                ths.trapType = ParseTrapType(td.type);
                ths.isArmed = true;

                if (!string.IsNullOrEmpty(td.power_source_id) && _powerNodesById.TryGetValue(td.power_source_id, out PowerSourceNode pNode))
                {
                    ths.linkedPowerSource = pNode;
                }

                ths.OnPlayerDamaged += HandleTrapDamagedPlayer;
                _spawnedTrapsById[td.id] = ths;
                _spawnedTrapList.Add(ths);
                streamingSystem.RegisterWorldObjectToTile(trapObj);
            }
        }

        private void SpawnEnemiesWithPatrols()
        {
            _spawnedEnemies.Clear();
            if (loadedBlueprint.enemies == null) return;

            Transform enemyGroup = new GameObject("Enemies").transform;
            enemyGroup.SetParent(_worldRoot, false);

            List<List<Vector2>> rawWaypoints = ExtractAllEnemyPatrolWaypointsFromRawJson(_rawJsonText);

            for (int i = 0; i < loadedBlueprint.enemies.Count; i++)
            {
                var ed = loadedBlueprint.enemies[i];
                string eClass = ExtractEnemyClassFromRaw(ed.id);
                string charGlb = !string.IsNullOrEmpty(ed.asset_id) ? ed.asset_id : MapEnemyClassToGlb(eClass);
                Vector3 pos = ExtractVector3(ed.pos, ed.elevation);

                GameObject enemyObj = RuntimeGLBAndMeshLoader.InstantiateGLB(
                    Path.Combine("Art/Characters", charGlb + ".glb"),
                    pos,
                    Quaternion.identity,
                    enemyGroup,
                    addCollider: true
                );
                enemyObj.name = ed.id + "_" + eClass;

                string wpnFile = MapWeaponIdToGlb(ed.weapon);
                Transform handR = FindDeepChild(enemyObj.transform, "HandR");
                GameObject wpnObj = RuntimeGLBAndMeshLoader.InstantiateGLB(
                    Path.Combine("Art/Weapons", wpnFile + ".glb"),
                    handR != null ? handR.position : (pos + new Vector3(0.25f, 1.2f, 0.25f)),
                    Quaternion.identity,
                    handR != null ? handR : enemyObj.transform,
                    addCollider: false
                );
                wpnObj.name = ed.id + "_Weapon";

                EnemyAIFSMSystem ai = enemyObj.AddComponent<EnemyAIFSMSystem>();
                // Imported GLBs use the shared RigRoot path and authored AnimationClip assets.
                enemyObj.AddComponent<AuthoredCharacterAnimation>();
                ai.agentId = ed.id;
                ai.classId = ParseEnemyClassId(eClass);
                ai.maxHitPoints = ed.hp > 0 ? ed.hp : (eClass == "E14" ? 200f : 100f);
                ai.currentHitPoints = ai.maxHitPoints;
                ai.currentState = ed.sleeper ? AIState.Idle : AIState.Patrol;

                if (i < rawWaypoints.Count && rawWaypoints[i].Count > 0)
                {
                    List<Vector2> wps = rawWaypoints[i];
                    ai.patrolWaypoints = new List<Vector3>(wps.Count);
                    for (int w = 0; w < wps.Count; w++)
                    {
                        ai.patrolWaypoints.Add(new Vector3(wps[w].x, pos.y, wps[w].y));
                    }
                }

                _spawnedEnemies.Add(ai);
                streamingSystem.RegisterWorldObjectToTile(enemyObj);
            }
        }

        private void SpawnOrPositionPlayer()
        {
            Vector3 startPos = new Vector3(20f, 0f, 10f);
            float startYaw = 0f;
            if (loadedBlueprint.player_start != null)
            {
                if (loadedBlueprint.player_start.pos != null && loadedBlueprint.player_start.pos.Length >= 2)
                {
                    startPos = new Vector3(loadedBlueprint.player_start.pos[0], 0.1f, loadedBlueprint.player_start.pos[1]);
                }
                startYaw = loadedBlueprint.player_start.facing_deg;
            }

            spawnedPlayer = FindObjectOfType<PlayerTacticalController>();
            if (spawnedPlayer == null)
            {
                GameObject playerObj = new GameObject("Player_TacticalOperative");
                CharacterController cc = playerObj.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);

                spawnedPlayer = playerObj.AddComponent<PlayerTacticalController>();

                GameObject bodyGlb = RuntimeGLBAndMeshLoader.InstantiateGLB(
                    "Art/Characters/chr_player_body_a.glb",
                    Vector3.zero,
                    Quaternion.identity,
                    playerObj.transform,
                    addCollider: false
                );
                bodyGlb.name = "PlayerHumanoidBody";

                GameObject camPivot = new GameObject("CameraPivot");
                camPivot.transform.SetParent(playerObj.transform, false);
                camPivot.transform.localPosition = new Vector3(0f, 1.68f, 0f);
                spawnedPlayer.cameraPivot = camPivot.transform;

                GameObject leanPivot = new GameObject("LeanPivot");
                leanPivot.transform.SetParent(camPivot.transform, false);
                spawnedPlayer.leanPivot = leanPivot.transform;

                Camera mainCam = Camera.main;
                if (mainCam == null)
                {
                    GameObject camObj = new GameObject("Main Camera");
                    camObj.tag = "MainCamera";
                    mainCam = camObj.AddComponent<Camera>();
                    camObj.AddComponent<AudioListener>();
                }
                mainCam.transform.SetParent(leanPivot.transform, false);
                mainCam.transform.localPosition = Vector3.zero;
                mainCam.transform.localRotation = Quaternion.identity;
                mainCam.nearClipPlane = 0.08f;
                mainCam.farClipPlane = 450f;
                spawnedPlayer.firstPersonCamera = mainCam;

                weaponSystem = playerObj.AddComponent<WeaponAndBallisticsSystem>();
                weaponSystem.viewmodelParent = mainCam.transform;
                string initialWpn = (missionId == "M09") ? "wpn_awm_l115a3" : MainMenuController.SelectedPrimaryWeapon;
                if (!weaponSystem.TrySetActiveWeaponById(initialWpn))
                {
                    weaponSystem.TrySetActiveWeapon(2); // USP .45 Suppressed fallback
                }
            }

            // Ensure health/damage exists for both newly spawned and scene-authored player roots.
            RealLoserStudio.Tactical.Player.PlayerDamageReceiver damageReceiver = spawnedPlayer.GetComponent<RealLoserStudio.Tactical.Player.PlayerDamageReceiver>();
            if (damageReceiver == null)
                damageReceiver = spawnedPlayer.gameObject.AddComponent<RealLoserStudio.Tactical.Player.PlayerDamageReceiver>();
            if (spawnedPlayer.GetComponent<RealLoserStudio.Tactical.UI.PlayerDeathScreen>() == null)
                spawnedPlayer.gameObject.AddComponent<RealLoserStudio.Tactical.UI.PlayerDeathScreen>();

            spawnedPlayer.transform.position = startPos;
            spawnedPlayer.transform.rotation = Quaternion.Euler(0f, startYaw, 0f);
            streamingSystem.playerTransform = spawnedPlayer.transform;
        }

        private void BuildMissionObjectiveRuntime()
        {
            MissionObjectiveRuntime missionFlow = FindObjectOfType<MissionObjectiveRuntime>();
            if (missionFlow == null)
            {
                GameObject flowObject = new GameObject("MissionObjectiveRuntime");
                flowObject.transform.SetParent(transform, false);
                missionFlow = flowObject.AddComponent<MissionObjectiveRuntime>();
            }
            missionFlow.Initialize(_rawJsonText, loadedBlueprint != null ? loadedBlueprint.title : "Mission", spawnedPlayer);
            if (touchHud != null)
            {
                touchHud.missionObjectiveRuntime = missionFlow;
                missionFlow.BindUI(touchHud.missionObjectiveText, touchHud.missionObjectiveStatusText);
            }
        }

        private void BuildRuntimeMobileTouchHUD()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            touchHud = FindObjectOfType<MobileTouchHUDAndMEMap>();
            if (touchHud == null)
            {
                GameObject canvasObj = new GameObject("MobileTouchHUD_Canvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObj.AddComponent<GraphicRaycaster>();

                touchHud = canvasObj.AddComponent<MobileTouchHUDAndMEMap>();
                touchHud.playerController = spawnedPlayer;
                touchHud.weaponSystem = weaponSystem;
                touchHud.stealthSystem = stealthSystem;
                touchHud.BuildCompleteMobileTouchControls(loadedBlueprint.mission_id, loadedBlueprint.title, new Vector2(loadedBlueprint.width, loadedBlueprint.height));
            }
        }

        private static Vector3 ExtractVector3(float[] arr, float defaultY = 0f)
        {
            if (arr == null || arr.Length < 2) return new Vector3(0f, defaultY, 0f);
            float x = arr[0];
            float z = arr[1];
            float y = arr.Length >= 3 ? arr[2] : defaultY;
            return new Vector3(x, y, z);
        }

        private static string MapTrapCodeToGlbName(string code)
        {
            switch (code)
            {
                case "T1": return "trap_t1_tripwire_alarm.glb";
                case "T2": return "trap_t2_tripwire_grenade.glb";
                case "T3": return "trap_t3_directional_mine.glb";
                case "T4": return "trap_t4_buried_mine.glb";
                case "T5": return "trap_t5_laser_grid.glb";
                case "T6": return "trap_t6_motion_sensor.glb";
                case "T7": return "trap_t7_pressure_plate.glb";
                case "T8": return "trap_t8_noise_trap.glb";
                case "T9": return "trap_t9_sentry_turret.glb";
                case "T10": return "trap_t10_searchlight_tower.glb";
                case "T11": return "trap_t11_security_camera.glb";
                case "T12": return "trap_t12_electric_fence.glb";
                case "T13": return "trap_t13_alarmed_door.glb";
                case "T14": return "trap_t14_booby_trapped_door.glb";
                case "T15": return "trap_t15_dog_patrol_hazard.glb";
                default: return "trap_t16_env_hazard.glb";
            }
        }

        private static TrapType ParseTrapType(string code)
        {
            switch (code)
            {
                case "T1": return TrapType.T1_TripwireAlarm;
                case "T2": return TrapType.T2_TripwireGrenade;
                case "T3": return TrapType.T3_DirectionalMine;
                case "T4": return TrapType.T4_BuriedMine;
                case "T5": return TrapType.T5_LaserGrid;
                case "T6": return TrapType.T6_MotionSensor;
                case "T7": return TrapType.T7_PressurePlate;
                case "T8": return TrapType.T8_NoiseTrap;
                case "T9": return TrapType.T9_SentryTurret;
                case "T10": return TrapType.T10_SearchlightTower;
                case "T11": return TrapType.T11_SecurityCamera;
                case "T12": return TrapType.T12_ElectricFence;
                case "T13": return TrapType.T13_AlarmedDoor;
                case "T14": return TrapType.T14_BoobyTrappedDoor;
                case "T15": return TrapType.T15_DogPatrolHazard;
                default: return TrapType.T16_EnvironmentalHazard;
            }
        }

        private static string ExtractEnemyClassFromRaw(string enemyId)
        {
            if (!string.IsNullOrEmpty(enemyId))
            {
                for (int c = 14; c >= 1; c--)
                {
                    if (enemyId.Contains("_E" + c + "_")) return "E" + c;
                }
            }
            return "E2";
        }

        private static string MapEnemyClassToGlb(string eClass)
        {
            switch (eClass)
            {
                case "E1": return "chr_enemy_e1_recruit";
                case "E2": return "chr_enemy_e2_rifleman";
                case "E3": return "chr_enemy_e3_officer";
                case "E4": return "chr_enemy_e4_shotgunner";
                case "E5": return "chr_enemy_e7_heavy_gunner";
                case "E6": return "chr_enemy_e6_sniper";
                case "E7": return "chr_enemy_e10_dog_handler";
                case "E8": return "chr_enemy_e8_shield_breacher";
                case "E9": return "chr_enemy_e9_technician";
                case "E10": return "chr_enemy_e13_ghillie_spotter";
                case "E11": return "chr_enemy_e11_medic";
                case "E12": return "chr_enemy_e12_elite_commando";
                case "E13": return "chr_enemy_e5_smg_runner";
                case "E14": return "chr_boss_commander_rask";
                default: return "chr_enemy_e2_rifleman";
            }
        }

        private static EnemyClassId ParseEnemyClassId(string eClass)
        {
            switch (eClass)
            {
                case "E1": return EnemyClassId.E1_Recruit;
                case "E2": return EnemyClassId.E2_Rifleman;
                case "E3": return EnemyClassId.E3_Officer;
                case "E4": return EnemyClassId.E4_Shotgunner;
                case "E5": return EnemyClassId.E7_HeavyGunner;
                case "E6": return EnemyClassId.E6_Sniper;
                case "E9": return EnemyClassId.E9_Technician;
                case "E10": return EnemyClassId.E13_GhillieSpotter;
                case "E11": return EnemyClassId.E11_Medic;
                case "E12": return EnemyClassId.E12_EliteCommando;
                case "E14": return EnemyClassId.Boss_CommanderRask;
                default: return EnemyClassId.E2_Rifleman;
            }
        }

        private static string MapWeaponIdToGlb(string wpn)
        {
            if (string.IsNullOrEmpty(wpn)) return "wpn_m4a1_sopmod";
            if (wpn.Contains("mp9") || wpn.Contains("mp5")) return "wpn_mp5a3_tactical";
            if (wpn.Contains("p9") || wpn.Contains("m9")) return "wpn_m9_beretta";
            if (wpn.Contains("p45") || wpn.Contains("usp")) return "wpn_usp45_suppressed";
            if (wpn.Contains("longbow") || wpn.Contains("awm")) return "wpn_awm_l115a3";
            if (wpn.Contains("lmg") || wpn.Contains("m249")) return "wpn_m249_saw";
            if (wpn.Contains("ar12") || wpn.Contains("g36")) return "wpn_g36c_commando";
            return "wpn_m4a1_sopmod";
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == targetName) return child;
                Transform found = FindDeepChild(child, targetName);
                if (found != null) return found;
            }
            return null;
        }

        private static List<List<Vector2>> ExtractAllEnemyPatrolWaypointsFromRawJson(string rawJson)
        {
            List<List<Vector2>> result = new List<List<Vector2>>();
            if (string.IsNullOrEmpty(rawJson)) return result;

            string marker = "\"patrol_waypoints\":";
            int searchIdx = 0;
            while (true)
            {
                int idx = rawJson.IndexOf(marker, searchIdx, StringComparison.Ordinal);
                if (idx < 0) break;
                int openBracket = rawJson.IndexOf('[', idx + marker.Length);
                if (openBracket < 0) break;

                int depth = 0;
                int endBracket = openBracket;
                for (int k = openBracket; k < rawJson.Length; k++)
                {
                    if (rawJson[k] == '[') depth++;
                    else if (rawJson[k] == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            endBracket = k;
                            break;
                        }
                    }
                }

                string block = rawJson.Substring(openBracket + 1, endBracket - openBracket - 1);
                List<Vector2> wps = new List<Vector2>();
                int subIdx = 0;
                while (true)
                {
                    int sb = block.IndexOf('[', subIdx);
                    if (sb < 0) break;
                    int eb = block.IndexOf(']', sb);
                    if (eb < 0) break;
                    string pairStr = block.Substring(sb + 1, eb - sb - 1);
                    string[] parts = pairStr.Split(',');
                    if (parts.Length >= 2 &&
                        float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float px) &&
                        float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float py))
                    {
                        wps.Add(new Vector2(px, py));
                    }
                    subIdx = eb + 1;
                }
                result.Add(wps);
                searchIdx = endBracket + 1;
            }
            return result;
        }
    }
}