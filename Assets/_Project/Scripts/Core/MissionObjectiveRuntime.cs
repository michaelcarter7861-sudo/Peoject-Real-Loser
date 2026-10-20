using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RealLoserStudio.Tactical.Player;
using RealLoserStudio.Tactical.AI;
using RealLoserStudio.Tactical.UI;

namespace RealLoserStudio.Tactical.Core
{
    /// <summary>
    /// Data-driven mission interaction/extraction runtime. Handles common interactable targets,
    /// alternate objectives, mission-specific enemy kill gates, M06 remote-charge safety and M08
    /// convoy checkpoints. Mission-specific mechanics that have no supporting entities in the
    /// blueprint (e.g. a real drivable truck/hostage actor) are explicitly reported as unsupported.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionObjectiveRuntime : MonoBehaviour
    {
        [Serializable] private sealed class MissionData { public string mission_id; public ObjectiveBundle objectives; public List<StructureRecord> structures; }
        [Serializable] private sealed class StructureRecord { public string id; public string type; public float[] pos; }
        [Serializable] private sealed class ObjectiveBundle { public List<ObjectiveRecord> primary; public ExtractionRecord extraction; }
        [Serializable] private sealed class ObjectiveRecord
        {
            public string id; public string title; public string target_id; public string alternate_target_id; public string hostage_id; public float[] target_pos; public float[] alternate_pos;
            public float[] cell_pos; public float[] extraction_pos; public float[] checkpoint_pos;
            public float[] boss_pos; public float[] truck_pos; public float[] delivery_pos; public float[] uplink_pos;
            public float checkpoint_alpha_x; public float checkpoint_bravo_x;
            public float min_safe_detonation_distance_m; public float hack_duration_sec; public float e9_technician_disarm_window_sec;
            public string boss_id; public string[] required_snipers; public string[] target_vehicles;
            public List<ManifoldRecord> manifolds;
        }
        [Serializable] private sealed class ManifoldRecord { public string id; public float[] pos; }
        [Serializable] private sealed class ExtractionRecord { public string id; public string title; public float[] pos; public float radius = 10f; }
        private sealed class ObjectiveStep
        {
            public string title;
            public Vector3 position;
            public Vector3 alternatePosition;
            public bool hasAlternate;
            public bool skipFollowingStepOnAlternate;
            public string requiredEnemyId;
            public string[] requiredEnemyIds;
            public string plantTargetId;
            public string destroyTargetId;
            public string alternateDestroyTargetId;
            public float holdDuration;
            public GameObject marker;
            public GameObject alternateMarker;
        }

        [Header("Interaction")]
        [Min(1f)] public float interactionRadius = 3.2f;
        [Min(1f)] public float extractionRadius = 10f;
        public PlayerTacticalController player;
        public Text objectiveText;
        public Text statusText;
        private readonly List<ObjectiveStep> _steps = new List<ObjectiveStep>();
        private readonly List<Vector3> _plantedChargePositions = new List<Vector3>();
        private readonly List<string> _plantedChargeTargetIds = new List<string>();
        private readonly List<GameObject> _chargeVisuals = new List<GameObject>();
        private readonly HashSet<string> _plantedVehicleCharges = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _chargePlantTimes = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _chargePlantStepIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        private float _technicianDisarmWindow = 10f;
        private int _requiredChargeCount = 3;
        private Transform _hostageTransform;
        private MissionEscortActor _hostageActor;
        private MissionCargoVehicleController _cargoVehicle;
        private bool _hostageFreed;
        private Vector3 _extractionPosition;
        private GameObject _extractionMarker;
        private int _stepIndex;
        private bool _missionComplete;
        private bool _missionFailed;
        private bool _chargesDetonated;
        private bool _holdInProgress;
        private bool _usedAlternatePosition;
        private float _holdElapsed;
        private string _missionId = "M01";
        private string _missionTitle = "Mission";
        private float _safeDetonationDistance = 50f;
        private string _bossId;
        private string[] _requiredSnipers;
        private string[] _targetVehicles;

        public bool IsComplete => _missionComplete;
        public bool IsFailed => _missionFailed;
        public string CurrentObjective => _missionComplete ? "MISSION COMPLETE" : _missionFailed ? "MISSION FAILED — RESTART" : _stepIndex < _steps.Count ? _steps[_stepIndex].title : (_missionId == "M06" && !_chargesDetonated ? "Detonate charges from a safe distance" : "Proceed to extraction");

        public void BindUI(Text objective, Text status) { objectiveText = objective; statusText = status; RefreshText(); }

        public void Initialize(string json, string missionTitle, PlayerTacticalController playerController)
        {
            ClearRuntimeMarkers();
            _steps.Clear();
            _plantedChargePositions.Clear();
            _plantedChargeTargetIds.Clear();
            for (int ci = 0; ci < _chargeVisuals.Count; ci++) if (_chargeVisuals[ci] != null) Destroy(_chargeVisuals[ci]);
            _chargeVisuals.Clear();
            _plantedVehicleCharges.Clear();
            _chargePlantTimes.Clear();
            _chargePlantStepIndices.Clear();
            _technicianDisarmWindow = 10f;
            _requiredChargeCount = 3;
            if (_hostageTransform != null) Destroy(_hostageTransform.gameObject);
            _hostageTransform = null;
            _hostageActor = null;
            if (_cargoVehicle != null) _cargoVehicle.StopDrive();
            _cargoVehicle = null;
            _hostageFreed = false;
            _stepIndex = 0;
            _missionComplete = false;
            _missionFailed = false;
            _chargesDetonated = false;
            _holdInProgress = false;
            _usedAlternatePosition = false;
            _holdElapsed = 0f;
            _bossId = null;
            _requiredSnipers = null;
            _targetVehicles = null;
            _safeDetonationDistance = 50f;
            player = playerController;
            _missionTitle = string.IsNullOrEmpty(missionTitle) ? "Mission" : missionTitle;

            MissionData data = null;
            try { data = JsonUtility.FromJson<MissionData>(json); }
            catch (Exception e) { Debug.LogError("[MissionObjectiveRuntime] Objective JSON parse failed: " + e.Message); }
            if (data == null || data.objectives == null)
            {
                Debug.LogError("[MissionObjectiveRuntime] Mission objective data is missing.");
                SetStatus("MISSION DATA ERROR — OBJECTIVES UNAVAILABLE");
                return;
            }

            _missionId = string.IsNullOrEmpty(data.mission_id) ? SceneManager.GetActiveScene().name : data.mission_id;
            List<ObjectiveRecord> primary = data.objectives.primary;
            if (primary != null)
            {
                for (int i = 0; i < primary.Count; i++)
                {
                    ObjectiveRecord o = primary[i];
                    if (o == null) continue;
                    _bossId = !string.IsNullOrEmpty(o.boss_id) ? o.boss_id : _bossId;
                    _requiredSnipers = o.required_snipers ?? _requiredSnipers;
                    _targetVehicles = o.target_vehicles ?? _targetVehicles;
                    if (o.min_safe_detonation_distance_m > 0f) _safeDetonationDistance = o.min_safe_detonation_distance_m;
                    if (o.e9_technician_disarm_window_sec > 0f) _technicianDisarmWindow = o.e9_technician_disarm_window_sec;

                    // M03 explicitly defines two mutually exclusive ways to destroy the relay.
                    if (_missionId == "M03" && o.target_pos != null && o.alternate_pos != null)
                    {
                        ObjectiveStep relay = AddStep("Plant C4 on relay tower OR destroy the fuel tank", o.target_pos, o.alternate_pos);
                        if (relay != null)
                        {
                            relay.plantTargetId = o.target_id;
                            relay.alternateDestroyTargetId = o.alternate_target_id;
                            relay.skipFollowingStepOnAlternate = true;
                        }
                        ObjectiveStep detonateRelay = AddStep("Detonate C4 on relay tower", o.target_pos);
                        if (detonateRelay != null) { detonateRelay.plantTargetId = o.target_id; detonateRelay.destroyTargetId = o.target_id; }
                    }
                    else
                    {
                        if (o.cell_pos != null) AddStep("Free the informant", o.cell_pos);
                        if (o.target_pos != null)
                        {
                            ObjectiveStep targetStep = AddStep(o.title, o.target_pos);
                            if (targetStep != null && (_missionId == "M02" || _missionId == "M07")) targetStep.destroyTargetId = o.target_id;
                        }
                        if (o.alternate_pos != null) AddStep("Complete alternate target: " + o.title, o.alternate_pos);
                        if (o.checkpoint_pos != null) AddStep("Reach convoy sabotage area", o.checkpoint_pos);
                        if (o.truck_pos != null) AddStep("Reach and secure the cargo vehicle", o.truck_pos);
                        if (o.uplink_pos != null)
                        {
                            ObjectiveStep uplink = AddStep("Hack the uplink terminal", o.uplink_pos);
                            if (uplink != null)
                            {
                                uplink.requiredEnemyIds = o.required_snipers;
                                uplink.holdDuration = Mathf.Max(0f, o.hack_duration_sec);
                            }
                        }
                        if (o.manifolds != null)
                        {
                            for (int m = 0; m < o.manifolds.Count; m++)
                            {
                                ManifoldRecord manifold = o.manifolds[m];
                                if (manifold == null || manifold.pos == null) continue;
                                ObjectiveStep charge = AddStep("Plant charge at manifold " + (m + 1), manifold.pos);
                                if (charge != null) charge.plantTargetId = manifold.id;
                            }
                            if (_missionId == "M06") _requiredChargeCount = o.manifolds.Count;
                        }
                        if (o.checkpoint_alpha_x > 0f) AddStep("Pass convoy checkpoint Alpha", new float[] { o.checkpoint_alpha_x, o.truck_pos != null && o.truck_pos.Length > 1 ? o.truck_pos[1] : 130f, 0f });
                        if (o.checkpoint_bravo_x > 0f) AddStep("Pass convoy checkpoint Bravo", new float[] { o.checkpoint_bravo_x, o.truck_pos != null && o.truck_pos.Length > 1 ? o.truck_pos[1] : 130f, 0f });
                        if (o.delivery_pos != null) AddStep("Deliver cargo at the destination", o.delivery_pos);
                        // Only M04 has an actual escort-destination step in this common loop.
                        if (_missionId == "M04" && o.extraction_pos != null) AddStep("Escort the informant to the rear dock", o.extraction_pos);
                        if (o.target_vehicles != null && o.target_vehicles.Length > 0 && _missionId == "M05")
                        {
                            for (int v = 0; v < o.target_vehicles.Length; v++)
                            {
                                string vehicleId = o.target_vehicles[v];
                                Vector3 vehiclePosition;
                                if (!TryFindStructurePosition(data.structures, vehicleId, out vehiclePosition))
                                {
                                    Debug.LogError("[MissionObjectiveRuntime] Required convoy vehicle structure not found: " + vehicleId);
                                    continue;
                                }
                                ObjectiveStep plant = AddStep("Plant C4 on cargo truck " + (v + 1), new float[] { vehiclePosition.x, vehiclePosition.z, vehiclePosition.y });
                                if (plant != null) plant.plantTargetId = vehicleId;
                                ObjectiveStep detonate = AddStep("Detonate C4 on cargo truck " + (v + 1), new float[] { vehiclePosition.x, vehiclePosition.z, vehiclePosition.y });
                                if (detonate != null) { detonate.destroyTargetId = vehicleId; detonate.plantTargetId = vehicleId; }
                            }
                        }
                        if (o.boss_pos != null)
                        {
                            ObjectiveStep bossStep = AddStep("Neutralize target: " + o.title, o.boss_pos);
                            if (bossStep != null) bossStep.requiredEnemyId = o.boss_id;
                        }
                    }
                }
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                _steps[i].marker = CreateMarker("ObjectiveMarker_" + i, _steps[i].position, i == 0 ? new Color(1f, 0.72f, 0.12f, 0.95f) : new Color(0.95f, 0.85f, 0.2f, 0.45f), 1.6f);
                if (_steps[i].hasAlternate)
                    _steps[i].alternateMarker = CreateMarker("ObjectiveAlternateMarker_" + i, _steps[i].alternatePosition, new Color(1f, 0.45f, 0.12f, 0.8f), 1.6f);
            }

            if (data.objectives.extraction != null && data.objectives.extraction.pos != null && data.objectives.extraction.pos.Length >= 2)
            {
                _extractionPosition = ReadPosition(data.objectives.extraction.pos);
                extractionRadius = Mathf.Max(2f, data.objectives.extraction.radius);
                _extractionMarker = CreateMarker("Extraction_" + data.objectives.extraction.id, _extractionPosition, new Color(0.1f, 0.9f, 0.45f, 0.9f), 2.5f);
            }
            else Debug.LogError("[MissionObjectiveRuntime] Extraction coordinates are missing for " + _missionId);

            if (_missionId == "M08" && primary != null)
            {
                for (int i = 0; i < primary.Count; i++)
                {
                    ObjectiveRecord o = primary[i];
                    if (o == null || o.truck_pos == null) continue;
                    GameObject truckObject = FindSceneObjectByName("M08_VEH_CARGO_TRUCK_01");
                    if (truckObject == null) truckObject = FindNearestSceneObject("veh_truck_cargo", ReadPosition(o.truck_pos), 18f);
                    if (truckObject != null)
                    {
                        _cargoVehicle = truckObject.GetComponent<MissionCargoVehicleController>();
                        if (_cargoVehicle == null) _cargoVehicle = truckObject.AddComponent<MissionCargoVehicleController>();
                        if (o.delivery_pos != null) _cargoVehicle.SetRouteTarget(ReadPosition(o.delivery_pos));
                    }
                    else Debug.LogError("[MissionObjectiveRuntime] M08 cargo truck was not spawned; vehicle-driving objective cannot run.");
                    break;
                }
            }

            if (_steps.Count == 0) Debug.LogError("[MissionObjectiveRuntime] No supported primary objective coordinates found for " + _missionId);
            RefreshText();
        }

        private ObjectiveStep AddStep(string title, float[] rawPosition, float[] alternate = null)
        {
            if (rawPosition == null || rawPosition.Length < 2) return null;
            ObjectiveStep step = new ObjectiveStep { title = string.IsNullOrEmpty(title) ? "Complete mission objective" : title, position = ReadPosition(rawPosition) };
            if (alternate != null && alternate.Length >= 2) { step.hasAlternate = true; step.alternatePosition = ReadPosition(alternate); }
            _steps.Add(step);
            return step;
        }

        private static Vector3 ReadPosition(float[] p) => new Vector3(p[0], p.Length > 2 ? p[2] : 0f, p[1]);

        private GameObject CreateMarker(string markerName, Vector3 pos, Color color, float scale)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = markerName;
            marker.transform.position = pos + Vector3.up * 0.12f;
            marker.transform.localScale = new Vector3(scale, 0.08f, scale);
            Collider c = marker.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = marker.GetComponent<Renderer>();
            if (r != null)
            {
                Shader shader = RuntimeGLBAndMeshLoader.GetSafeLitShader();
                if (shader != null) { Material m = new Material(shader); m.color = color; m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.15f); r.sharedMaterial = m; }
            }
            return marker;
        }

        public bool TryInteract()
        {
            if (_missionComplete || _missionFailed || player == null) return false;
            PlayerDamageReceiver health = player.GetComponent<PlayerDamageReceiver>();
            if (health != null && health.IsDead) return false;

            if (_stepIndex < _steps.Count)
            {
                ObjectiveStep step = _steps[_stepIndex];
                bool atPrimary = IsWithinObjectiveRange(player.transform.position, step.position, interactionRadius);
                bool atAlternate = step.hasAlternate && IsWithinObjectiveRange(player.transform.position, step.alternatePosition, interactionRadius);
                if (!atPrimary && !atAlternate) { SetStatus("OBJECTIVE TOO FAR — MOVE CLOSER"); return false; }
                if (!RequiredEnemiesNeutralized(step)) return false;
                if (!string.IsNullOrEmpty(step.plantTargetId) && step.destroyTargetId == step.plantTargetId && !_plantedVehicleCharges.Contains(step.plantTargetId))
                {
                    if (step.title.StartsWith("Detonate", StringComparison.Ordinal)) { SetStatus("PLANT C4 FIRST"); return false; }
                }
                _usedAlternatePosition = !atPrimary && atAlternate;
                if (step.title.StartsWith("Plant C4", StringComparison.Ordinal) && !string.IsNullOrEmpty(step.plantTargetId) && !_usedAlternatePosition)
                {
                    if (!_plantedVehicleCharges.Add(step.plantTargetId)) { SetStatus("C4 ALREADY PLANTED"); return false; }
                    SetStatus("C4 PLANTED — MOVE TO DETONATE CONTROL");
                }
                if (_missionId == "M08" && step.title.IndexOf("secure the cargo vehicle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (_cargoVehicle == null) { SetStatus("CARGO VEHICLE MISSING — MISSION CANNOT CONTINUE"); return false; }
                    if (!_cargoVehicle.BeginDrive(player, FindObjectOfType<MobileTouchHUDAndMEMap>()))
                    { SetStatus("CARGO VEHICLE UNAVAILABLE — CHECK CLEARANCE AROUND TRUCK"); return false; }
                    SetStatus("TRUCK HIJACKED — DRIVE TO CHECKPOINT ALPHA");
                }
                if (_missionId == "M04" && step.title.IndexOf("Escort the informant", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (_hostageActor == null || _hostageActor.IsDead) { _missionFailed = true; SetStatus("MISSION FAILED — INFORMANT LOST"); RefreshText(); return false; }
                    if (Vector3.Distance(_hostageActor.transform.position, step.position) > extractionRadius) { SetStatus("WAIT FOR INFORMANT — ESCORT HIM TO THE DOCK"); return false; }
                }
                if (step.holdDuration > 0f)
                {
                    if (!_holdInProgress) { _holdInProgress = true; _holdElapsed = 0f; SetStatus("HACKING — STAY IN RANGE"); }
                    return true;
                }
                CompleteCurrentStep();
                return true;
            }

            if (_missionId == "M06" && !_chargesDetonated && _plantedChargePositions.Count > 0)
            {
                if (_plantedChargePositions.Count < _requiredChargeCount)
                { SetStatus("CHARGES MISSING — REPLANT DISARMED MANIFOLD CHARGES"); return false; }
                float nearestCharge = float.MaxValue;
                for (int i = 0; i < _plantedChargePositions.Count; i++) nearestCharge = Mathf.Min(nearestCharge, Vector3.Distance(player.transform.position, _plantedChargePositions[i]));
                if (nearestCharge < _safeDetonationDistance) { SetStatus("UNSAFE DETONATION — MOVE AT LEAST " + _safeDetonationDistance.ToString("0") + "M AWAY"); return false; }
                _chargesDetonated = true;
                for (int ci = 0; ci < _plantedChargeTargetIds.Count; ci++)
                {
                    GameObject manifold = FindSceneObjectByName(_plantedChargeTargetIds[ci]);
                    if (manifold != null)
                    {
                        SpawnExplosion(manifold.transform.position, true);
                        Destroy(manifold);
                    }
                }
                for (int ci = 0; ci < _chargeVisuals.Count; ci++) if (_chargeVisuals[ci] != null) Destroy(_chargeVisuals[ci]);
                _chargeVisuals.Clear();
                SetStatus("CHARGES DETONATED FROM SAFE DISTANCE");
                RefreshText();
                return true;
            }

            if (_extractionMarker == null) { SetStatus("OBJECTIVES COMPLETE — NO EXTRACTION DATA"); return false; }
            if (Vector3.Distance(player.transform.position, _extractionPosition) > extractionRadius) { SetStatus("EXTRACTION ZONE NOT REACHED"); return false; }
            if (_missionId == "M06" && !_chargesDetonated) { SetStatus("DETONATE CHARGES BEFORE EXTRACTION"); return false; }
            _missionComplete = true;
            if (_cargoVehicle != null && _cargoVehicle.IsOccupied) _cargoVehicle.StopDrive();
            SetStatus("MISSION COMPLETE — " + _missionId);
            RefreshText();
            if (_extractionMarker != null) { Destroy(_extractionMarker); _extractionMarker = null; }
            return true;
        }

        private void Update()
        {
            if (_missionComplete || _missionFailed || player == null) return;
            PlayerDamageReceiver health = player.GetComponent<PlayerDamageReceiver>();
            if (health != null && health.IsDead)
            {
                _missionFailed = true;
                _holdInProgress = false;
                SetStatus("OPERATIVE DOWN — RESTART MISSION");
                RefreshText();
                return;
            }
            if (_hostageFreed && _hostageActor != null && _hostageActor.IsDead)
            {
                _missionFailed = true;
                SetStatus("MISSION FAILED — INFORMANT LOST");
                RefreshText();
                return;
            }
            if (_missionId == "M06") TickTechnicianDisarm();
            if (_missionId == "M08" && _cargoVehicle != null && _cargoVehicle.IsOccupied && _stepIndex < _steps.Count)
            {
                ObjectiveStep curStep = _steps[_stepIndex];
                if (IsWithinObjectiveRange(player.transform.position, curStep.position, 14.0f))
                {
                    CompleteCurrentStep();
                    return;
                }
            }
            if (_holdInProgress && _stepIndex < _steps.Count)
            {
                ObjectiveStep step = _steps[_stepIndex];
                bool inRange = IsWithinObjectiveRange(player.transform.position, step.position, interactionRadius) || (step.hasAlternate && IsWithinObjectiveRange(player.transform.position, step.alternatePosition, interactionRadius));
                if (!inRange) { _holdInProgress = false; _holdElapsed = 0f; SetStatus("HACK INTERRUPTED — RETURN TO TERMINAL"); }
                else
                {
                    _holdElapsed += Time.deltaTime;
                    SetStatus("HACKING " + Mathf.Clamp01(_holdElapsed / step.holdDuration).ToString("P0"));
                    if (_holdElapsed >= step.holdDuration && RequiredEnemiesNeutralized(step)) { _holdInProgress = false; CompleteCurrentStep(); }
                }
            }
        }

        private bool RequiredEnemiesNeutralized(ObjectiveStep step)
        {
            if (!string.IsNullOrEmpty(step.requiredEnemyId) && !IsEnemyNeutralized(step.requiredEnemyId))
            { SetStatus("TARGET STILL ACTIVE — NEUTRALIZE TARGET FIRST"); return false; }
            if (step.requiredEnemyIds != null)
            {
                for (int i = 0; i < step.requiredEnemyIds.Length; i++)
                    if (!IsEnemyNeutralized(step.requiredEnemyIds[i])) { SetStatus("ELITE SNIPERS REMAINING — " + step.requiredEnemyIds[i]); return false; }
            }
            // M10 assassination must be complete before extraction becomes available.
            if (_missionId == "M10" && _stepIndex >= _steps.Count - 1 && !string.IsNullOrEmpty(_bossId) && !IsEnemyNeutralized(_bossId))
            { SetStatus("COMMANDER RASK IS STILL ACTIVE"); return false; }
            return true;
        }

        private bool IsEnemyNeutralized(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            EnemyAIFSMSystem[] enemies = FindObjectsOfType<EnemyAIFSMSystem>();
            for (int i = 0; i < enemies.Length; i++) if (enemies[i] != null && enemies[i].agentId == id) return enemies[i].IsNeutralized;
            return false;
        }

        private void CompleteCurrentStep()
        {
            ObjectiveStep step = _steps[_stepIndex];
            string targetToDestroy = _usedAlternatePosition && !string.IsNullOrEmpty(step.alternateDestroyTargetId) ? step.alternateDestroyTargetId : step.destroyTargetId;
            if (_missionId == "M06" && !_usedAlternatePosition && !string.IsNullOrEmpty(step.plantTargetId) && string.IsNullOrEmpty(step.destroyTargetId))
            {
                if (!_plantedChargeTargetIds.Contains(step.plantTargetId))
                {
                    _plantedChargePositions.Add(step.position);
                    _plantedChargeTargetIds.Add(step.plantTargetId);
                    _chargePlantTimes[step.plantTargetId] = Time.time;
                    _chargePlantStepIndices[step.plantTargetId] = _stepIndex;
                }
            }
            if ((_missionId == "M06" || _missionId == "M05" || _missionId == "M03") && !_usedAlternatePosition && !string.IsNullOrEmpty(step.plantTargetId) && string.IsNullOrEmpty(step.destroyTargetId))
            {
                GameObject charge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                charge.name = "PlantedC4_" + step.plantTargetId;
                charge.transform.position = step.position + Vector3.up * 0.45f;
                charge.transform.localScale = new Vector3(0.35f, 0.18f, 0.25f);
                Collider chargeCollider = charge.GetComponent<Collider>();
                if (chargeCollider != null) chargeCollider.enabled = false;
                Renderer chargeRenderer = charge.GetComponent<Renderer>();
                if (chargeRenderer != null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader != null) { Material mat = new Material(shader); mat.color = new Color(0.16f, 0.18f, 0.15f); chargeRenderer.sharedMaterial = mat; }
                }
                _chargeVisuals.Add(charge);
            }
            if (!string.IsNullOrEmpty(targetToDestroy))
            {
                GameObject targetObject = FindSceneObjectByName(targetToDestroy);
                if (targetObject != null)
                {
                    SpawnExplosion(targetObject.transform.position, _missionId == "M03" || _missionId == "M05" || _missionId == "M06");
                    Destroy(targetObject);
                }
                else Debug.LogWarning("[MissionObjectiveRuntime] Objective completed but target object was not found in scene: " + targetToDestroy);
                for (int ci = _chargeVisuals.Count - 1; ci >= 0; ci--)
                {
                    if (_chargeVisuals[ci] != null && _chargeVisuals[ci].name == "PlantedC4_" + targetToDestroy)
                    { Destroy(_chargeVisuals[ci]); _chargeVisuals.RemoveAt(ci); }
                }
                _plantedVehicleCharges.Remove(targetToDestroy);
            }
            if (_missionId == "M04" && !_hostageFreed && step.title.IndexOf("Free the informant", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _hostageFreed = true;
                CreateOrActivateHostage(step.position);
            }
            if (_missionId == "M04" && step.title.IndexOf("Escort the informant", StringComparison.OrdinalIgnoreCase) >= 0 && _hostageTransform != null)
            {
                Destroy(_hostageTransform.gameObject);
                _hostageTransform = null;
            }
            bool skipNext = step.skipFollowingStepOnAlternate && _usedAlternatePosition;
            _usedAlternatePosition = false;
            if (step.marker != null) { Destroy(step.marker); step.marker = null; }
            if (step.alternateMarker != null) { Destroy(step.alternateMarker); step.alternateMarker = null; }
            _stepIndex++;
            if (skipNext) _stepIndex = Mathf.Min(_steps.Count, _stepIndex + 1);
            if (_stepIndex < _steps.Count)
            {
                Renderer r = _steps[_stepIndex].marker != null ? _steps[_stepIndex].marker.GetComponent<Renderer>() : null;
                if (r != null) r.material.color = new Color(1f, 0.72f, 0.12f, 0.95f);
                if (_steps[_stepIndex].alternateMarker != null)
                {
                    Renderer ar = _steps[_stepIndex].alternateMarker.GetComponent<Renderer>();
                    if (ar != null) ar.material.color = new Color(1f, 0.45f, 0.12f, 0.95f);
                }
            }
            _holdElapsed = 0f;
            _holdInProgress = false;
            SetStatus("OBJECTIVE CONFIRMED");
            RefreshText();
        }

        public void RestartMission()
        {
            Time.timeScale = 1f;
            // The active scene name is authoritative; mission IDs are data IDs and may not match scene names.
            Scene active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0) SceneManager.LoadScene(active.buildIndex);
            else SceneManager.LoadScene(active.name);
        }


        private static bool TryFindStructurePosition(List<StructureRecord> structures, string id, out Vector3 position)
        {
            if (structures != null)
                for (int i = 0; i < structures.Count; i++)
                    if (structures[i] != null && structures[i].id == id && structures[i].pos != null && structures[i].pos.Length >= 2)
                    { position = ReadPosition(structures[i].pos); return true; }
            position = Vector3.zero;
            return false;
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return null;
            GameObject direct = GameObject.Find(objectName);
            if (direct != null) return direct;
            Transform[] all = FindObjectsOfType<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == objectName) return all[i].gameObject;
            }
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name.StartsWith(objectName + "_", StringComparison.OrdinalIgnoreCase))
                    return all[i].gameObject;
            }
            return null;
        }

        private bool IsWithinObjectiveRange(Vector3 actorPos, Vector3 targetPos, float radius)
        {
            float effectiveRadius = (_cargoVehicle != null && _cargoVehicle.IsOccupied) ? Mathf.Max(radius, 12.0f) : radius;
            if (Vector3.Distance(actorPos, targetPos) <= effectiveRadius) return true;
            float horizDist = Vector2.Distance(new Vector2(actorPos.x, actorPos.z), new Vector2(targetPos.x, targetPos.z));
            float vertDist = Mathf.Abs(actorPos.y - targetPos.y);
            return horizDist <= effectiveRadius && vertDist <= 10.5f;
        }

        private void CreateOrActivateHostage(Vector3 startPosition)
        {
            GameObject hostage = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hostage.name = "M04_Informant_EscortActor";
            hostage.transform.position = startPosition + Vector3.up * 1f;
            hostage.transform.localScale = new Vector3(0.65f, 1f, 0.65f);
            Renderer renderer = hostage.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = RuntimeGLBAndMeshLoader.GetSafeLitShader();
                if (shader != null) { Material mat = new Material(shader); mat.color = new Color(0.18f, 0.42f, 0.68f); renderer.sharedMaterial = mat; }
            }
            Collider collider = hostage.GetComponent<Collider>();
            if (collider != null) { collider.enabled = true; collider.isTrigger = true; }
            _hostageTransform = hostage.transform;
            _hostageActor = hostage.AddComponent<MissionEscortActor>();
            _hostageActor.followTarget = player != null ? player.transform : null;
            SetStatus("INFORMANT FREED — ESCORT TO REAR DOCK");
        }

        private void TickTechnicianDisarm()
        {
            if (_plantedChargeTargetIds.Count == 0 || _technicianDisarmWindow <= 0f) return;
            EnemyAIFSMSystem[] enemies = FindObjectsOfType<EnemyAIFSMSystem>();
            for (int e = 0; e < enemies.Length; e++)
            {
                EnemyAIFSMSystem technician = enemies[e];
                if (technician == null || technician.IsNeutralized || technician.classId != EnemyClassId.E9_Technician) continue;
                for (int i = 0; i < _plantedChargeTargetIds.Count; i++)
                {
                    string id = _plantedChargeTargetIds[i];
                    float plantedAt;
                    if (!_chargePlantTimes.TryGetValue(id, out plantedAt) || Time.time - plantedAt > _technicianDisarmWindow) continue;
                    if (Vector3.Distance(technician.transform.position, _plantedChargePositions[i]) > 3.0f) continue;
                    int stepIndex;
                    _chargePlantStepIndices.TryGetValue(id, out stepIndex);
                    for (int c = _chargeVisuals.Count - 1; c >= 0; c--)
                    {
                        if (_chargeVisuals[c] != null && _chargeVisuals[c].name == "PlantedC4_" + id)
                        { Destroy(_chargeVisuals[c]); _chargeVisuals.RemoveAt(c); }
                    }
                    _plantedChargeTargetIds.RemoveAt(i);
                    _plantedChargePositions.RemoveAt(i);
                    _chargePlantTimes.Remove(id);
                    _chargePlantStepIndices.Remove(id);
                    _stepIndex = Mathf.Clamp(stepIndex, 0, Mathf.Max(0, _steps.Count - 1));
                    if (_stepIndex < _steps.Count && _steps[_stepIndex].marker == null)
                        _steps[_stepIndex].marker = CreateMarker("ObjectiveMarker_Replant_" + _stepIndex, _steps[_stepIndex].position, new Color(1f, 0.72f, 0.12f, 0.95f), 1.6f);
                    SetStatus("TECHNICIAN DISARMED " + id + " — REPLANT CHARGE");
                    RefreshText();
                    return;
                }
            }
        }

        private static GameObject FindNearestSceneObject(string nameContains, Vector3 position, float maxDistance)
        {
            Transform[] all = FindObjectsOfType<Transform>();
            GameObject best = null;
            float bestDistance = maxDistance;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                float d = Vector3.Distance(all[i].position, position);
                if (d <= bestDistance) { bestDistance = d; best = all[i].gameObject; }
            }
            return best;
        }

        private void SpawnExplosion(Vector3 position, bool large)
        {
            GameObject blast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blast.name = "PRL_ExplosionEffect";
            blast.transform.position = position + Vector3.up * 0.8f;
            blast.transform.localScale = Vector3.one * 0.3f;
            Collider col = blast.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer rend = blast.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = RuntimeGLBAndMeshLoader.CreateSafeMaterial("PRL_Explosion_Flash", new Color(1f, 0.42f, 0.08f), 0f, 0.05f);
                rend.sharedMaterial = mat;
            }
            StartCoroutine(AnimateExplosion(blast, large ? 7.5f : 4f, large ? 0.65f : 0.35f));
        }

        private System.Collections.IEnumerator AnimateExplosion(GameObject effect, float size, float duration)
        {
            float elapsed = 0f;
            Vector3 initial = effect != null ? effect.transform.localScale : Vector3.one;
            while (elapsed < duration && effect != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                effect.transform.localScale = Vector3.Lerp(initial, Vector3.one * size, t);
                Renderer r = effect.GetComponent<Renderer>();
                if (r != null && r.material.HasProperty("_Color"))
                {
                    Color c = r.material.color; c.a = 1f - t; r.material.color = c;
                }
                yield return null;
            }
            if (effect != null) Destroy(effect);
        }

        private void RefreshText()
        {
            if (objectiveText == null) return;
            if (_missionComplete) objectiveText.text = "MISSION COMPLETE";
            else if (_missionFailed) objectiveText.text = "MISSION FAILED — RESTART";
            else if (_stepIndex < _steps.Count) objectiveText.text = "OBJECTIVE " + (_stepIndex + 1) + "/" + _steps.Count + ": " + _steps[_stepIndex].title;
            else if (_missionId == "M06" && !_chargesDetonated) objectiveText.text = "OBJECTIVES COMPLETE — DETONATE FROM SAFE DISTANCE";
            else objectiveText.text = "OBJECTIVES COMPLETE — REACH EXTRACTION";
        }

        private void SetStatus(string value) { if (statusText != null) statusText.text = value; Debug.Log("[MissionObjectiveRuntime] " + value); }

        private void ClearRuntimeMarkers()
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i].marker != null) Destroy(_steps[i].marker);
                if (_steps[i].alternateMarker != null) Destroy(_steps[i].alternateMarker);
            }
            if (_extractionMarker != null) Destroy(_extractionMarker);
            _extractionMarker = null;
        }
    }
}
