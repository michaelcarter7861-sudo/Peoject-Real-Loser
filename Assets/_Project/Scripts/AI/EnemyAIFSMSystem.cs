using System;
using System.Collections.Generic;
using UnityEngine;
using RealLoserStudio.Tactical.Player;
using RealLoserStudio.Tactical.Stealth;
using RealLoserStudio.Tactical.Traps;
using RealLoserStudio.Tactical.Core;

namespace RealLoserStudio.Tactical.AI
{
    public enum EnemyClassId
    {
        E1_Recruit = 1,
        E2_Rifleman = 2,
        E3_Officer = 3,          // RadioCheckIn every 45s
        E4_Shotgunner = 4,
        E5_SMGRunner = 5,
        E6_Sniper = 6,           // High-ground overwatch (watchtower, roof, cliff ridge)
        E7_HeavyGunner = 7,
        E8_ShieldBreacher = 8,
        E9_Technician = 9,       // RepairCameraAndDisarmC4
        E10_DogHandler = 10,
        E11_Medic = 11,          // ReviveFallenAlly
        E12_EliteCommando = 12,
        E13_GhillieSpotter = 13,
        E14_Juggernaut = 14,
        Civ_Worker = 15,
        Civ_Scientist = 16,
        Civ_Hostage = 17,
        Civ_Technician = 18,
        NPC_DrMirza = 19,
        NPC_Informant = 20,
        Boss_CaptainDar = 21,
        Boss_CommanderRask = 22,
        Boss_ViktorHale = 23
    }

    public enum AIState
    {
        Idle = 0,
        Patrol = 1,
        InvestigateNoise = 2,
        InspectBody = 3,
        RunToAlarm = 4,
        CombatCover = 5,
        Flank = 6,
        RadioCheckIn = 7,            // E3 Officer every 45s
        RepairCameraAndDisarmC4 = 8, // E9 Technician
        ReviveFallenAlly = 9,        // E11 Medic
        IncapacitatedOrDead = 10
    }

    /// <summary>
    /// Full Tactical Enemy & Civilian AI FSM for Project Real Loser (Unity 2022.3 LTS).
    /// Supports E1–E14, Civilians, Key NPCs, and 3 Bosses with specialized states:
    /// - E3 Officer: RadioCheckIn every 45.0s
    /// - E9 Technician: RepairCameraAndDisarmC4
    /// - E11 Medic: ReviveFallenAlly
    /// - E6 Sniper: High-point overwatch
    /// </summary>
    public class EnemyAIFSMSystem : MonoBehaviour
    {
        [Header("Identity & Archetype")]
        public string agentId = "enemy_01";
        public EnemyClassId classId = EnemyClassId.E2_Rifleman;
        public AIState currentState = AIState.Patrol;

        [Header("Patrol Waypoints (>= 3 Required for E1-E14)")]
        public List<Vector3> patrolWaypoints = new List<Vector3>();
        private int _currentWaypointIndex;
        public float patrolMoveSpeed = 2.6f;
        public float combatMoveSpeed = 4.4f;

        [Header("Perception & Combat Parameters")]
        public float sightRangeMeters = 42.0f;
        public float sightConeAngleDegrees = 85.0f;
        public float maxHitPoints = 100.0f;
        public float currentHitPoints = 100.0f;
        public bool isKnockedOut;
        public bool isDead;

        [Header("Special Role Timers (E3, E9, E11)")]
        public const float E3_RADIO_CHECKIN_INTERVAL = 45.0f;
        private float _radioCheckInTimer = E3_RADIO_CHECKIN_INTERVAL;
        private float _stateActionTimer;

        // Contextual targets
        private Vector3 _investigateTargetPos;
        [Header("Awareness & Line of Sight")]
        [Min(0.1f)] public float combatMemorySeconds = 5.5f;
        [Min(0f)] public float eyeHeightMeters = 1.45f;
        [Min(0f)] public float playerAimHeightMeters = 1.0f;
        [Min(0f)] public float visibilityRayOriginOffset = 0.08f;
        private Vector3 _lastKnownPlayerPosition;
        private float _combatMemoryRemaining;
        private EnemyAIFSMSystem _targetFallenAlly;
        private TrapAndHazardSystem _targetDamagedCamera;
        private Transform _nearestAlarmPanel;
        private Transform _nearestCoverNode;
        [Header("Combat Firing")]
        [Min(0.1f)] public float enemyShotInterval = 0.85f;
        [Min(1f)] public float enemyShotDamage = 8f;
        [Min(1f)] public float enemyWeaponRange = 36f;
        private float _nextEnemyShotTime;
        private StealthAndDetectionSystem _noiseSubscriptionSource;

        public bool IsNeutralized => isDead || isKnockedOut;

        private void OnEnable()
        {
            RefreshNoiseSubscription();
        }

        private void OnDisable()
        {
            RemoveNoiseSubscription();
        }

        private void OnDestroy()
        {
            RemoveNoiseSubscription();
        }

        private void RefreshNoiseSubscription()
        {
            StealthAndDetectionSystem current = StealthAndDetectionSystem.Instance;
            if (_noiseSubscriptionSource == current) return;
            RemoveNoiseSubscription();
            if (isActiveAndEnabled && current != null)
            {
                _noiseSubscriptionSource = current;
                _noiseSubscriptionSource.OnNoiseEmitted += HandleNoiseStimulus;
            }
        }

        private void RemoveNoiseSubscription()
        {
            if (_noiseSubscriptionSource != null)
                _noiseSubscriptionSource.OnNoiseEmitted -= HandleNoiseStimulus;
            _noiseSubscriptionSource = null;
        }

        public void TickFSM(PlayerTacticalController player, List<EnemyAIFSMSystem> squadList, List<TrapAndHazardSystem> trapList, float deltaTime)
        {
            // Scene bootstrap order is not guaranteed: retry binding when the stealth singleton appears/reloads.
            RefreshNoiseSubscription();
            deltaTime = Mathf.Max(0f, deltaTime);
            if (IsNeutralized)
            {
                currentState = AIState.IncapacitatedOrDead;
                return;
            }

            // E3 Officer: Periodic 45-second Radio Check-In
            if (classId == EnemyClassId.E3_Officer)
            {
                _radioCheckInTimer -= deltaTime;
                if (_radioCheckInTimer <= 0f && currentState != AIState.CombatCover && currentState != AIState.Flank)
                {
                    _radioCheckInTimer = E3_RADIO_CHECKIN_INTERVAL;
                    TransitionToState(AIState.RadioCheckIn, 3.0f);
                }
            }

            // E9 Technician: Scan for destroyed/disabled T11 Security Cameras or planted C4
            if (classId == EnemyClassId.E9_Technician && currentState == AIState.Patrol && trapList != null)
            {
                for (int i = 0; i < trapList.Count; i++)
                {
                    TrapAndHazardSystem t = trapList[i];
                    if (t != null && t.trapType == TrapType.T11_SecurityCamera && (t.isDestroyed || !t.isArmed))
                    {
                        _targetDamagedCamera = t;
                        TransitionToState(AIState.RepairCameraAndDisarmC4, 6.0f);
                        break;
                    }
                }
            }

            // E11 Medic: Scan for knocked-out or downed allies
            if (classId == EnemyClassId.E11_Medic && currentState != AIState.ReviveFallenAlly && squadList != null)
            {
                for (int i = 0; i < squadList.Count; i++)
                {
                    EnemyAIFSMSystem ally = squadList[i];
                    if (ally != null && ally != this && ally.isKnockedOut && !ally.isDead && ally.gameObject.activeInHierarchy)
                    {
                        _targetFallenAlly = ally;
                        TransitionToState(AIState.ReviveFallenAlly, 5.0f);
                        break;
                    }
                }
            }

            // Perception: vision requires range, cone and unobstructed line of sight.
            bool seesPlayer = player != null && CanSeePlayer(player);
            if (seesPlayer)
            {
                _lastKnownPlayerPosition = player.transform.position;
                _combatMemoryRemaining = Mathf.Max(0.1f, combatMemorySeconds);
                if (IsCivilian(classId))
                {
                    if (currentState != AIState.RunToAlarm)
                        TransitionToState(AIState.RunToAlarm, 8.0f);
                }
                else if (currentState != AIState.CombatCover && currentState != AIState.Flank)
                {
                    if (classId == EnemyClassId.E5_SMGRunner || classId == EnemyClassId.E12_EliteCommando)
                        TransitionToState(AIState.Flank, 10.0f);
                    else
                        TransitionToState(AIState.CombatCover, 12.0f);
                }
            }
            else if (currentState == AIState.CombatCover || currentState == AIState.Flank)
            {
                _combatMemoryRemaining -= deltaTime;
                if (_combatMemoryRemaining <= 0f)
                {
                    _investigateTargetPos = _lastKnownPlayerPosition;
                    TransitionToState(AIState.InvestigateNoise, 4.0f);
                }
            }

            // Execute State Logic
            switch (currentState)
            {
                case AIState.Idle:
                    if (patrolWaypoints.Count >= 3)
                        TransitionToState(AIState.Patrol, 0f);
                    break;

                case AIState.Patrol:
                    UpdatePatrolState(deltaTime);
                    break;

                case AIState.InvestigateNoise:
                    MoveTowardPoint(_investigateTargetPos, patrolMoveSpeed * 1.15f, deltaTime);
                    if (Vector3.Distance(transform.position, _investigateTargetPos) < 1.2f)
                    {
                        _stateActionTimer -= deltaTime;
                        if (_stateActionTimer <= 0f)
                            TransitionToState(AIState.Patrol, 0f);
                    }
                    break;

                case AIState.InspectBody:
                    if (_targetFallenAlly != null)
                    {
                        MoveTowardPoint(_targetFallenAlly.transform.position, combatMoveSpeed, deltaTime);
                        if (Vector3.Distance(transform.position, _targetFallenAlly.transform.position) < 2.0f)
                        {
                            StealthAndDetectionSystem.Instance?.EscalateToAlert(CompoundAlertLevel.Alert2_CompoundAlarm);
                            TransitionToState(AIState.RunToAlarm, 6.0f);
                        }
                    }
                    else
                    {
                        TransitionToState(AIState.Patrol, 0f);
                    }
                    break;

                case AIState.RunToAlarm:
                    if (_nearestAlarmPanel != null)
                    {
                        MoveTowardPoint(_nearestAlarmPanel.position, combatMoveSpeed, deltaTime);
                        if (Vector3.Distance(transform.position, _nearestAlarmPanel.position) < 1.5f)
                        {
                            StealthAndDetectionSystem.Instance?.EmitNoise(transform.position, 60.0f, NoiseCategory.AlarmSiren, true);
                            TransitionToState(AIState.CombatCover, 10.0f);
                        }
                    }
                    else
                    {
                        StealthAndDetectionSystem.Instance?.EscalateToAlert(CompoundAlertLevel.Alert2_CompoundAlarm);
                        TransitionToState(AIState.CombatCover, 10.0f);
                    }
                    break;

                case AIState.CombatCover:
                    if (seesPlayer && _nearestCoverNode != null)
                        MoveTowardPoint(_nearestCoverNode.position, combatMoveSpeed, deltaTime);
                    else if (!seesPlayer && _combatMemoryRemaining > 0f)
                        MoveTowardPoint(_lastKnownPlayerPosition, patrolMoveSpeed, deltaTime);
                    break;

                case AIState.Flank:
                    if (seesPlayer && player != null)
                    {
                        Vector3 flankDirection = (player.transform.position - transform.position).normalized;
                        Vector3 flankOffset = Vector3.Cross(Vector3.up, flankDirection) * 6.5f;
                        MoveTowardPoint(player.transform.position + flankOffset, combatMoveSpeed * 1.15f, deltaTime);
                    }
                    else if (_combatMemoryRemaining > 0f)
                    {
                        MoveTowardPoint(_lastKnownPlayerPosition, patrolMoveSpeed, deltaTime);
                    }
                    break;

                case AIState.RadioCheckIn:
                    _stateActionTimer -= deltaTime;
                    if (_stateActionTimer <= 0f)
                    {
                        // Completed E3 45s radio check-in
                        TransitionToState(AIState.Patrol, 0f);
                    }
                    break;

                case AIState.RepairCameraAndDisarmC4:
                    if (_targetDamagedCamera != null)
                    {
                        MoveTowardPoint(_targetDamagedCamera.transform.position, patrolMoveSpeed * 1.25f, deltaTime);
                        if (Vector3.Distance(transform.position, _targetDamagedCamera.transform.position) <= 2.2f)
                        {
                            _stateActionTimer -= deltaTime;
                            if (_stateActionTimer <= 0f)
                            {
                                _targetDamagedCamera.isDestroyed = false;
                                _targetDamagedCamera.isArmed = true;
                                _targetDamagedCamera.hitPoints = 30.0f;
                                _targetDamagedCamera = null;
                                TransitionToState(AIState.Patrol, 0f);
                            }
                        }
                    }
                    else
                    {
                        TransitionToState(AIState.Patrol, 0f);
                    }
                    break;

                case AIState.ReviveFallenAlly:
                    if (_targetFallenAlly != null && _targetFallenAlly.isKnockedOut && !_targetFallenAlly.isDead)
                    {
                        MoveTowardPoint(_targetFallenAlly.transform.position, combatMoveSpeed, deltaTime);
                        if (Vector3.Distance(transform.position, _targetFallenAlly.transform.position) <= 1.8f)
                        {
                            _stateActionTimer -= deltaTime;
                            if (_stateActionTimer <= 0f)
                            {
                                _targetFallenAlly.isKnockedOut = false;
                                _targetFallenAlly.currentHitPoints = _targetFallenAlly.maxHitPoints * 0.5f;
                                _targetFallenAlly.TransitionToState(AIState.CombatCover, 10.0f);
                                _targetFallenAlly = null;
                                TransitionToState(AIState.CombatCover, 10.0f);
                            }
                        }
                    }
                    else
                    {
                        TransitionToState(AIState.Patrol, 0f);
                    }
                    break;
            }

            // Combat states must be able to threaten the player, not merely move toward cover.
            if (player != null && !IsCivilian(classId) &&
                (currentState == AIState.CombatCover || currentState == AIState.Flank))
            {
                TryFireAtPlayer(player);
            }
        }

        private void TryFireAtPlayer(PlayerTacticalController player)
        {
            if (player == null || Time.time < _nextEnemyShotTime) return;
            PlayerDamageReceiver receiver = player.GetComponent<PlayerDamageReceiver>();
            if (receiver == null || receiver.IsDead) return;

            Vector3 origin = transform.position + Vector3.up * 1.35f;
            Vector3 target = player.transform.position + Vector3.up * 1.05f;
            Vector3 toTarget = target - origin;
            float distance = toTarget.magnitude;
            float range = Mathf.Min(enemyWeaponRange, sightRangeMeters * 1.15f);
            if (distance <= 0.1f || distance > range) return;

            Vector3 direction = toTarget / distance;
            float angle = Vector3.Angle(transform.forward, direction);
            if (angle > sightConeAngleDegrees * 0.65f) return;

            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance + 0.25f, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool playerVisible = false;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                Transform hitTransform = hitCollider != null ? hitCollider.transform : null;
                if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform)) continue;

                // In the informant mission, stray enemy fire can hit the escort if he crosses the line of fire.
                MissionEscortActor escort = hitTransform.GetComponentInParent<MissionEscortActor>();
                if (escort != null && !escort.IsDead)
                {
                    _nextEnemyShotTime = Time.time + Mathf.Max(0.1f, enemyShotInterval);
                    escort.ApplyDamage(enemyShotDamage);
                    StealthAndDetectionSystem.Instance?.EmitNoise(origin, 48f, NoiseCategory.UnsuppressedGunshot, true);
                    return;
                }
                if (hitCollider.isTrigger) continue;
                if (hitTransform == player.transform || hitTransform.IsChildOf(player.transform)) playerVisible = true;
                break; // First non-trigger, non-self collider decides line of sight.
            }
            if (!playerVisible) return;

            _nextEnemyShotTime = Time.time + Mathf.Max(0.1f, enemyShotInterval);
            float distanceFactor = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(distance / Mathf.Max(1f, range)));
            float classFactor = classId == EnemyClassId.E6_Sniper || classId == EnemyClassId.E7_HeavyGunner ||
                                classId == EnemyClassId.E14_Juggernaut ? 1.65f : 1f;
            receiver.ApplyDamage(enemyShotDamage * distanceFactor * classFactor, gameObject);
            StealthAndDetectionSystem.Instance?.EmitNoise(origin, 48f, NoiseCategory.UnsuppressedGunshot, true);
        }

        private void UpdatePatrolState(float deltaTime)
        {
            if (patrolWaypoints == null || patrolWaypoints.Count == 0) return;

            Vector3 targetWp = patrolWaypoints[_currentWaypointIndex];
            MoveTowardPoint(targetWp, patrolMoveSpeed, deltaTime);

            Vector2 pos2D = new Vector2(transform.position.x, transform.position.z);
            Vector2 wp2D = new Vector2(targetWp.x, targetWp.z);
            if (Vector2.Distance(pos2D, wp2D) <= 0.45f)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % patrolWaypoints.Count;
            }
        }

        private void MoveTowardPoint(Vector3 target, float speed, float deltaTime)
        {
            Vector3 diff = target - transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist < 0.05f) return;

            Vector3 dir = diff / dist;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), deltaTime * 8.0f);
            transform.position += dir * Mathf.Min(dist, speed * deltaTime);
        }

        public bool CanSeePlayer(PlayerTacticalController player)
        {
            if (player == null || !player.isActiveAndEnabled) return false;
            float visFactor = StealthAndDetectionSystem.Instance != null
                ? StealthAndDetectionSystem.Instance.ComputePlayerVisibilityFactor(player)
                : player.ActiveProfile.visibilityMultiplier;

            float effectiveRange = sightRangeMeters * Mathf.Clamp(visFactor, 0.05f, 2.0f);
            if (classId == EnemyClassId.E6_Sniper || classId == EnemyClassId.E13_GhillieSpotter)
                effectiveRange *= 1.85f;

            Vector3 origin = transform.position + Vector3.up * Mathf.Max(0f, eyeHeightMeters);
            Vector3 target = player.transform.position + Vector3.up * Mathf.Max(0f, playerAimHeightMeters);
            Vector3 toPlayer = target - origin;
            float dist = toPlayer.magnitude;
            if (dist > effectiveRange || dist < 0.05f) return false;

            Vector3 flatDirection = target - origin;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude < 0.001f) return true;
            float angle = Vector3.Angle(transform.forward, flatDirection.normalized);
            if (angle > (sightConeAngleDegrees * 0.5f)) return false;

            // Ignore this actor's own colliders; the first real obstruction must be the player.
            Vector3 rayOrigin = origin + flatDirection.normalized * Mathf.Max(0f, visibilityRayOriginOffset);
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, (target - rayOrigin).normalized,
                Mathf.Max(0f, Vector3.Distance(rayOrigin, target)), ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i].collider;
                if (hit == null) continue;
                Transform hitTransform = hit.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                if (hitTransform == player.transform || hitTransform.IsChildOf(player.transform)) return true;
                // Any solid collider before the player blocks vision (walls, doors, terrain, props).
                return false;
            }
            return false;
        }

        private void HandleNoiseStimulus(NoiseStimulus stim)
        {
            if (IsNeutralized) return;
            float dist = Vector3.Distance(transform.position, stim.origin);
            if (dist <= stim.effectiveRadiusMeters)
            {
                _investigateTargetPos = stim.origin;
                if (currentState == AIState.Idle || currentState == AIState.Patrol)
                {
                    TransitionToState(AIState.InvestigateNoise, 8.0f);
                }
            }
        }

        public void ApplyDamage(float dmg, bool nonLethalTakedown = false)
        {
            if (IsNeutralized) return;
            if (nonLethalTakedown)
            {
                isKnockedOut = true;
                currentHitPoints = 1f;
                currentState = AIState.IncapacitatedOrDead;
                return;
            }

            currentHitPoints -= dmg;
            if (currentHitPoints <= 0f)
            {
                isDead = true;
                currentHitPoints = 0f;
                currentState = AIState.IncapacitatedOrDead;
            }
        }

        public void TransitionToState(AIState nextState, float actionDuration)
        {
            currentState = nextState;
            _stateActionTimer = actionDuration;
        }

        public static bool IsCivilian(EnemyClassId id)
        {
            return id == EnemyClassId.Civ_Worker ||
                   id == EnemyClassId.Civ_Scientist ||
                   id == EnemyClassId.Civ_Hostage ||
                   id == EnemyClassId.Civ_Technician;
        }
    }
}
