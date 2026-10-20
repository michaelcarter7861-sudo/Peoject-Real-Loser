using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RealLoserStudio.Tactical.Player;
using RealLoserStudio.Tactical.Stealth;
using RealLoserStudio.Tactical.Weapons;
using RealLoserStudio.Tactical.Traps;
using RealLoserStudio.Tactical.AI;
using RealLoserStudio.Tactical.Core;

namespace RealLoserStudio.Tactical.UI
{
    public enum TacticalRouteType
    {
        Silent = 0,     // Low visibility, vents, wire cutters, garrote/suppressed
        Technical = 1,  // Breaker boxes, PDA hacking, camera loops, EMP
        Loud = 2        // Heavy breach, sniper overwatch, C4/RPG
    }

    [Serializable]
    public struct SatelliteMapZone
    {
        public string zoneId;
        public string zoneType; // exterior, interior, cliff_ridge, extraction
        public Rect worldBounds;
    }

    [Serializable]
    public struct TacticalRouteOverlay
    {
        public TacticalRouteType routeType;
        public string routeLabel;
        public List<Vector3> worldWaypoints;
    }

    /// <summary>
    /// Android Landscape Touch HUD & IGI Top-Down Satellite Map (`ME Map`) Controller.
    /// Implements:
    /// - Virtual Joystick (inner walk zone / outer sprint zone) + WASD fallback
    /// - Touch-Drag Look + Scope FOV sensitivity scaling + Mouse Look fallback
    /// - Lean Left / Lean Right (±18°) buttons
    /// - Stance Button (Tap = Crouch, Hold = Prone)
    /// - Fire, ADS Scope, Reload, Switch Weapon, and Context Action Button with radial timer ring
    /// - Live Top-Down IGI Satellite Map (`ME Map`) with zones, tagged enemies, camera cones, and 3 tactical routes
    /// </summary>
    public class MobileTouchHUDAndMEMap : MonoBehaviour
    {
        [Header("Player & System Bindings")]
        public PlayerTacticalController playerController;
        public WeaponAndBallisticsSystem weaponSystem;
        public StealthAndDetectionSystem stealthSystem;
        public MissionObjectiveRuntime missionObjectiveRuntime;
        public Text missionObjectiveText;
        public Text missionObjectiveStatusText;

        [Header("Virtual Joystick (Walk / Sprint Zones)")]
        public float joystickDeadZone = 0.12f;
        public float joystickSprintThreshold = 0.78f;
        public Vector2 currentJoystickInput { get; private set; }
        public bool isSprintZoneActive { get; private set; }

        [Header("Touch-Drag Look & Scope Scaling")]
        public float baseTouchSensitivity = 0.18f;
        public float baseFieldOfView = 70.0f;
        public float currentScopeFieldOfView = 70.0f;
        public bool isScopeActive { get; private set; }
        private Vector2 _pendingLookDelta;

        [Header("Stance Button (Tap = Crouch, Hold = Prone)")]
        public float proneHoldThresholdSeconds = 0.28f;
        private float _stancePressTimer;
        private bool _isStanceButtonHeld;
        private bool _proneTriggeredOnHold;

        // Context Action Button & Progress Ring
        public bool isContextActionActive { get; private set; }
        public float contextActionProgress01 { get; private set; }
        private float _contextActionRequiredDuration;
        private float _contextActionElapsed;
        private TrapAndHazardSystem _activeTargetTrap;
        private CounterToolType _activeTool = CounterToolType.WireCutters;

        [Header("Live Top-Down IGI Satellite Map (ME Map)")]
        public bool isMEMapOpen;
        public TacticalRouteType highlightedRoute = TacticalRouteType.Silent;
        public List<SatelliteMapZone> mapZones = new List<SatelliteMapZone>();
        public List<TacticalRouteOverlay> tacticalRoutes = new List<TacticalRouteOverlay>(3);
        public readonly List<Transform> taggedEnemies = new List<Transform>();
        public readonly List<TrapAndHazardSystem> trackedCamerasAndSearchlights = new List<TrapAndHazardSystem>();

        // Runtime uGUI Elements
        private Text _statusBannerText;
        private Text _weaponBannerText;
        private GameObject _meMapPanel;
        private RectTransform _meMapPlayerDot;
        private Vector2 _missionMapDimensions = new Vector2(300f, 300f);

        private void Update()
        {
            float dt = Time.deltaTime;
            TickHUD(dt);

            if (playerController != null)
            {
                Vector2 move = currentJoystickInput;
                bool sprint = isSprintZoneActive;

                // Keyboard fallback for Editor/Desktop testing alongside Android Touch
                float kx = Input.GetAxisRaw("Horizontal");
                float ky = Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(kx) > 0.01f || Mathf.Abs(ky) > 0.01f)
                {
                    move = new Vector2(kx, ky);
                    sprint = Input.GetKey(KeyCode.LeftShift);
                }

                PlayerDamageReceiver damageReceiver = playerController.GetComponent<PlayerDamageReceiver>();
                if (damageReceiver == null || !damageReceiver.IsDead)
                    playerController.UpdateMovement(move, _pendingLookDelta, sprint, dt);
                _pendingLookDelta = Vector2.zero;
            }

            UpdateStatusReadout();
            UpdateMEMapDot();
        }

        public void OnVirtualJoystickUpdate(Vector2 rawNormalizedJoystick)
        {
            float mag = rawNormalizedJoystick.magnitude;
            if (mag < joystickDeadZone)
            {
                currentJoystickInput = Vector2.zero;
                isSprintZoneActive = false;
                return;
            }

            isSprintZoneActive = (mag >= joystickSprintThreshold);
            currentJoystickInput = Vector2.ClampMagnitude(rawNormalizedJoystick, 1.0f);
        }

        public Vector2 ComputeScaledLookDelta(Vector2 rawTouchPixelDelta)
        {
            float fovScale = Mathf.Clamp(currentScopeFieldOfView / Mathf.Max(baseFieldOfView, 1f), 0.08f, 1.0f);
            return rawTouchPixelDelta * (baseTouchSensitivity * Mathf.Clamp(PlayerPrefs.GetFloat("PRL_LookSensitivity", 1f), 0.25f, 2f) * fovScale);
        }

        public void OnLeanButtonInput(float leanDirection)
        {
            if (playerController != null)
            {
                playerController.SetLeanInput(leanDirection);
            }
        }

        public void OnStanceButtonDown()
        {
            _isStanceButtonHeld = true;
            _stancePressTimer = 0f;
            _proneTriggeredOnHold = false;
        }

        public void OnStanceButtonUp()
        {
            if (!_isStanceButtonHeld) return;
            _isStanceButtonHeld = false;

            if (!_proneTriggeredOnHold && playerController != null)
            {
                PlayerPosture next = (playerController.CurrentPosture == PlayerPosture.Crouch)
                    ? PlayerPosture.Stand
                    : PlayerPosture.Crouch;
                playerController.SetPosture(next);
            }
        }

        public void OnFireButtonPressed()
        {
            if (weaponSystem == null || playerController == null) return;
            Transform camT = playerController.firstPersonCamera != null
                ? playerController.firstPersonCamera.transform
                : playerController.transform;

            weaponSystem.FireActiveWeapon(camT.position + camT.forward * 0.4f, camT.forward);

            var def = weaponSystem.ActiveWeapon;
            string sfx = def.isSuppressed ? "sfx_wpn_pistol_suppressed" : "sfx_wpn_rifle_fire";
            AudioClip clip = RuntimeGLBAndMeshLoader.LoadWavAudioClip(sfx);
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, camT.position, 0.85f);
            }

        }

        public void OnScopeButtonToggle()
        {
            isScopeActive = !isScopeActive;
            currentScopeFieldOfView = isScopeActive ? 24.0f : baseFieldOfView;
            if (playerController != null && playerController.firstPersonCamera != null)
            {
                playerController.firstPersonCamera.fieldOfView = currentScopeFieldOfView;
            }
        }

        public void OnCycleWeaponButton()
        {
            if (weaponSystem == null) return;
            int nextIndex = (weaponSystem.activeWeaponIndex + 1) % WeaponAndBallisticsSystem.All26Weapons.Length;
            weaponSystem.TrySetActiveWeapon(nextIndex);
        }

        public void OnReloadButtonPressed()
        {
            if (weaponSystem != null) weaponSystem.StartReload();
        }

        public void OnContextInteractNearestTrap()
        {
            if (playerController == null) return;
            TrapAndHazardSystem[] allTraps = FindObjectsOfType<TrapAndHazardSystem>();
            TrapAndHazardSystem nearest = null;
            float bestDist = 4.5f;
            for (int i = 0; i < allTraps.Length; i++)
            {
                if (!allTraps[i].isArmed || allTraps[i].isDestroyed) continue;
                float d = Vector3.Distance(playerController.transform.position, allTraps[i].transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest = allTraps[i];
                }
            }

            if (nearest != null)
            {
                nearest.isArmed = false;
                AudioClip snip = RuntimeGLBAndMeshLoader.LoadWavAudioClip("sfx_gadget_wire_cutter_snip");
                if (snip != null) AudioSource.PlayClipAtPoint(snip, nearest.transform.position, 0.9f);
            }
        }

        public void BeginContextAction(TrapAndHazardSystem trap, CounterToolType tool)
        {
            if (trap == null || playerController == null) return;
            float duration = trap.GetCounterInteractionDuration(tool, playerController.CurrentPosture);
            if (duration < 0f) return;

            _activeTargetTrap = trap;
            _activeTool = tool;
            _contextActionRequiredDuration = Mathf.Max(duration, 0.05f);
            _contextActionElapsed = 0f;
            isContextActionActive = true;
            contextActionProgress01 = 0f;
        }

        public void CancelContextAction()
        {
            isContextActionActive = false;
            contextActionProgress01 = 0f;
            _activeTargetTrap = null;
        }

        public void ToggleMEMap()
        {
            isMEMapOpen = !isMEMapOpen;
            if (_meMapPanel != null)
            {
                _meMapPanel.SetActive(isMEMapOpen);
            }
        }

        public void SelectTacticalRoute(TacticalRouteType route)
        {
            highlightedRoute = route;
        }

        public void TickHUD(float deltaTime)
        {
            if (_isStanceButtonHeld)
            {
                _stancePressTimer += deltaTime;
                if (!_proneTriggeredOnHold && _stancePressTimer >= proneHoldThresholdSeconds && playerController != null)
                {
                    _proneTriggeredOnHold = true;
                    PlayerPosture next = (playerController.CurrentPosture == PlayerPosture.Prone)
                        ? PlayerPosture.Stand
                        : PlayerPosture.Prone;
                    playerController.SetPosture(next);
                }
            }

            if (isContextActionActive && _activeTargetTrap != null)
            {
                _contextActionElapsed += deltaTime;
                contextActionProgress01 = Mathf.Clamp01(_contextActionElapsed / _contextActionRequiredDuration);
                if (contextActionProgress01 >= 1.0f)
                {
                    _activeTargetTrap.ApplyCountermeasure(_activeTool, playerController);
                    CancelContextAction();
                }
            }
        }

        /// <summary>
        /// Programmatically builds the complete Android landscape touch HUD & Satellite ME Map overlay.
        /// </summary>
        public void BuildCompleteMobileTouchControls(string missionId, string missionTitle, Vector2 mapSize)
        {
            _missionMapDimensions = new Vector2(Mathf.Max(100f, mapSize.x), Mathf.Max(100f, mapSize.y));
            Font font = SceneFlowControllers.GetSafeBuiltinFont();

            // 1. Top Tactical Status Bar
            GameObject topBar = CreateHUDPanel(transform, "TopStatusBar", new Vector2(0.02f, 0.91f), new Vector2(0.98f, 0.98f), new Color(0.05f, 0.08f, 0.10f, 0.82f));
            _statusBannerText = CreateHUDText(topBar.transform, "StatusText",
                $"{missionId}: {missionTitle} | ALERT: 0 (UNAWARE) | POSTURE: STAND",
                new Vector2(0.02f, 0.1f), new Vector2(0.58f, 0.9f), font, 20, TextAnchor.MiddleLeft, new Color(0.72f, 0.96f, 0.78f));
            _weaponBannerText = CreateHUDText(topBar.transform, "WeaponText",
                "WEAPON: USP .45 Suppressed",
                new Vector2(0.60f, 0.1f), new Vector2(0.84f, 0.9f), font, 20, TextAnchor.MiddleRight, new Color(0.90f, 0.92f, 0.78f));

            CreateHUDButton(topBar.transform, "BtnMEMap", "ME SATELLITE MAP", new Vector2(0.86f, 0.12f), new Vector2(0.99f, 0.88f), font, new Color(0.14f, 0.42f, 0.56f, 0.95f), () =>
            {
                ToggleMEMap();
            });

            // 2. Center Crosshair
            GameObject crosshair = CreateHUDPanel(transform, "Crosshair", new Vector2(0.497f, 0.494f), new Vector2(0.503f, 0.506f), new Color(0.25f, 0.95f, 0.45f, 0.85f));

            // 3. Left Virtual Joystick Pad (Press-and-Hold D-Pad + Analog Touch Drag)
            GameObject joyPad = CreateHUDPanel(transform, "VirtualJoystickPad", new Vector2(0.03f, 0.06f), new Vector2(0.23f, 0.42f), new Color(0.10f, 0.14f, 0.16f, 0.55f));
            RectTransform joyRect = joyPad.GetComponent<RectTransform>();
            EventTrigger joyTrig = joyPad.AddComponent<EventTrigger>();
            EventTrigger.Entry joyDrag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            joyDrag.callback.AddListener((data) =>
            {
                PointerEventData ped = (PointerEventData)data;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(joyRect, ped.position, ped.pressEventCamera, out Vector2 localPt))
                {
                    Vector2 halfSize = joyRect.rect.size * 0.5f;
                    Vector2 norm = new Vector2(
                        halfSize.x > 1f ? Mathf.Clamp(localPt.x / halfSize.x, -1f, 1f) : 0f,
                        halfSize.y > 1f ? Mathf.Clamp(localPt.y / halfSize.y, -1f, 1f) : 0f
                    );
                    OnVirtualJoystickUpdate(norm);
                }
            });
            EventTrigger.Entry joyUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            joyUp.callback.AddListener((_) => OnVirtualJoystickUpdate(Vector2.zero));
            joyTrig.triggers.Add(joyDrag);
            joyTrig.triggers.Add(joyUp);

            BindHoldMoveButton(CreateHUDButton(joyPad.transform, "MoveFwd", "FWD", new Vector2(0.34f, 0.66f), new Vector2(0.66f, 0.96f), font, new Color(0.2f, 0.3f, 0.25f, 0.8f), () => OnVirtualJoystickUpdate(new Vector2(0f, 1f))).gameObject, new Vector2(0f, 1f));
            BindHoldMoveButton(CreateHUDButton(joyPad.transform, "MoveBack", "BACK", new Vector2(0.34f, 0.04f), new Vector2(0.66f, 0.34f), font, new Color(0.2f, 0.3f, 0.25f, 0.8f), () => OnVirtualJoystickUpdate(new Vector2(0f, -0.75f))).gameObject, new Vector2(0f, -0.75f));
            BindHoldMoveButton(CreateHUDButton(joyPad.transform, "MoveLeft", "LEFT", new Vector2(0.04f, 0.35f), new Vector2(0.33f, 0.65f), font, new Color(0.2f, 0.3f, 0.25f, 0.8f), () => OnVirtualJoystickUpdate(new Vector2(-0.85f, 0f))).gameObject, new Vector2(-0.85f, 0f));
            BindHoldMoveButton(CreateHUDButton(joyPad.transform, "MoveRight", "RIGHT", new Vector2(0.67f, 0.35f), new Vector2(0.96f, 0.65f), font, new Color(0.2f, 0.3f, 0.25f, 0.8f), () => OnVirtualJoystickUpdate(new Vector2(0.85f, 0f))).gameObject, new Vector2(0.85f, 0f));
            CreateHUDButton(joyPad.transform, "MoveStop", "STOP", new Vector2(0.35f, 0.36f), new Vector2(0.65f, 0.64f), font, new Color(0.35f, 0.22f, 0.22f, 0.8f), () => OnVirtualJoystickUpdate(Vector2.zero));

            // 4. Right Touch Look Pad & Action Buttons
            GameObject lookPad = CreateHUDPanel(transform, "TouchLookZone", new Vector2(0.52f, 0.28f), new Vector2(0.96f, 0.86f), new Color(0f, 0f, 0f, 0.02f));
            EventTrigger lookTrig = lookPad.AddComponent<EventTrigger>();
            EventTrigger.Entry dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener((data) =>
            {
                PointerEventData ped = (PointerEventData)data;
                _pendingLookDelta += ComputeScaledLookDelta(ped.delta);
            });
            lookTrig.triggers.Add(dragEntry);

            // Tactical Action Cluster (Right Side)
            CreateHUDButton(transform, "BtnFire", "FIRE", new Vector2(0.84f, 0.08f), new Vector2(0.96f, 0.24f), font, new Color(0.72f, 0.18f, 0.16f, 0.90f), OnFireButtonPressed);
            CreateHUDButton(transform, "BtnScope", "SCOPE", new Vector2(0.71f, 0.08f), new Vector2(0.82f, 0.20f), font, new Color(0.18f, 0.42f, 0.52f, 0.88f), OnScopeButtonToggle);
            CreateHUDButton(transform, "BtnCrouch", "CROUCH", new Vector2(0.58f, 0.08f), new Vector2(0.69f, 0.20f), font, new Color(0.22f, 0.36f, 0.28f, 0.88f), () =>
            {
                if (playerController != null)
                {
                    playerController.SetPosture(playerController.CurrentPosture == PlayerPosture.Crouch ? PlayerPosture.Stand : PlayerPosture.Crouch);
                }
            });
            CreateHUDButton(transform, "BtnProne", "PRONE", new Vector2(0.45f, 0.08f), new Vector2(0.56f, 0.20f), font, new Color(0.22f, 0.32f, 0.42f, 0.88f), () =>
            {
                if (playerController != null)
                {
                    playerController.SetPosture(playerController.CurrentPosture == PlayerPosture.Prone ? PlayerPosture.Stand : PlayerPosture.Prone);
                }
            });
            CreateHUDButton(transform, "BtnLeanL", "LEAN L", new Vector2(0.26f, 0.22f), new Vector2(0.35f, 0.32f), font, new Color(0.26f, 0.28f, 0.32f, 0.85f), () => OnLeanButtonInput(-1f));
            CreateHUDButton(transform, "BtnLeanC", "CENTER", new Vector2(0.36f, 0.22f), new Vector2(0.44f, 0.32f), font, new Color(0.26f, 0.28f, 0.32f, 0.85f), () => OnLeanButtonInput(0f));
            CreateHUDButton(transform, "BtnLeanR", "LEAN R", new Vector2(0.45f, 0.22f), new Vector2(0.54f, 0.32f), font, new Color(0.26f, 0.28f, 0.32f, 0.85f), () => OnLeanButtonInput(1f));
            CreateHUDButton(transform, "BtnDisarm", "DISARM / HACK", new Vector2(0.26f, 0.08f), new Vector2(0.43f, 0.20f), font, new Color(0.62f, 0.46f, 0.14f, 0.90f), OnContextInteractNearestTrap);
            CreateHUDButton(transform, "BtnSwitchWpn", "NEXT WPN", new Vector2(0.84f, 0.26f), new Vector2(0.96f, 0.36f), font, new Color(0.28f, 0.36f, 0.24f, 0.88f), OnCycleWeaponButton);
            CreateHUDButton(transform, "BtnReload", "RELOAD", new Vector2(0.71f, 0.26f), new Vector2(0.82f, 0.36f), font, new Color(0.48f, 0.38f, 0.20f, 0.90f), OnReloadButtonPressed);
            CreateHUDButton(transform, "BtnObjectiveInteract", "INTERACT", new Vector2(0.57f, 0.24f), new Vector2(0.69f, 0.34f), font, new Color(0.12f, 0.48f, 0.30f, 0.94f), OnObjectiveInteract);
            CreateHUDButton(transform, "BtnRestartMission", "RESTART", new Vector2(0.02f, 0.84f), new Vector2(0.12f, 0.90f), font, new Color(0.48f, 0.20f, 0.16f, 0.90f), OnRestartMission);
            CreateHUDButton(transform, "BtnPauseMenu", "PAUSE", new Vector2(0.13f, 0.84f), new Vector2(0.23f, 0.90f), font, new Color(0.18f, 0.28f, 0.36f, 0.90f), () => { FindObjectOfType<TacticalPauseSettings>()?.ShowPause(); });
            CreateHUDButton(transform, "BtnJump", "JUMP", new Vector2(0.84f, 0.38f), new Vector2(0.96f, 0.47f), font, new Color(0.24f, 0.44f, 0.36f, 0.88f), () => { if (playerController != null) playerController.TryJump(); });
            missionObjectiveText = CreateHUDText(transform, "MissionObjectiveReadout", "OBJECTIVE: INITIALIZING", new Vector2(0.25f, 0.43f), new Vector2(0.72f, 0.49f), font, 18, TextAnchor.MiddleCenter, new Color(0.96f, 0.86f, 0.42f, 1f));
            missionObjectiveStatusText = CreateHUDText(transform, "MissionObjectiveStatus", "", new Vector2(0.25f, 0.385f), new Vector2(0.72f, 0.425f), font, 16, TextAnchor.MiddleCenter, new Color(0.78f, 0.95f, 0.82f, 1f));

            // 5. Top-Down Satellite ME Map Modal Overlay
            _meMapPanel = CreateHUDPanel(transform, "MESatelliteMapOverlay", new Vector2(0.18f, 0.16f), new Vector2(0.82f, 0.88f), new Color(0.04f, 0.10f, 0.08f, 0.94f));
            CreateHUDText(_meMapPanel.transform, "MapTitle",
                $"IGI SATELLITE RECONNAISSANCE MAP ({missionId} — {_missionMapDimensions.x:0}m x {_missionMapDimensions.y:0}m)",
                new Vector2(0.03f, 0.90f), new Vector2(0.97f, 0.98f), font, 22, TextAnchor.MiddleLeft, new Color(0.45f, 0.98f, 0.62f));

            GameObject playerDotObj = CreateHUDPanel(_meMapPanel.transform, "PlayerMapBlip", new Vector2(0.49f, 0.49f), new Vector2(0.51f, 0.51f), new Color(0.2f, 1.0f, 0.4f, 1f));
            _meMapPlayerDot = playerDotObj.GetComponent<RectTransform>();
            _meMapPanel.SetActive(false);
        }

        private void BindHoldMoveButton(GameObject btnObj, Vector2 dir)
        {
            if (btnObj == null) return;
            EventTrigger trig = btnObj.GetComponent<EventTrigger>();
            if (trig == null) trig = btnObj.AddComponent<EventTrigger>();
            EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener((_) => OnVirtualJoystickUpdate(dir));
            EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener((_) => OnVirtualJoystickUpdate(Vector2.zero));
            EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener((_) => OnVirtualJoystickUpdate(Vector2.zero));
            trig.triggers.Add(down);
            trig.triggers.Add(up);
            trig.triggers.Add(exit);
        }

        private void OnObjectiveInteract()
        {
            if (missionObjectiveRuntime == null) { Debug.LogWarning("Mission objective runtime is not ready."); return; }
            missionObjectiveRuntime.TryInteract();
        }

        private void OnRestartMission()
        {
            if (missionObjectiveRuntime != null) missionObjectiveRuntime.RestartMission();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private void UpdateStatusReadout()
        {
            if (_statusBannerText != null && playerController != null)
            {
                string alertStr = stealthSystem != null ? stealthSystem.currentAlertLevel.ToString() : "Alert0_Unaware";
                PlayerDamageReceiver health = playerController.GetComponent<PlayerDamageReceiver>();
                string healthStr = health != null ? $" | HP: {health.CurrentHealth:0}/{health.maxHealth:0}" : string.Empty;
                string deathStr = health != null && health.IsDead ? " | OPERATIVE DOWN" : string.Empty;
                _statusBannerText.text = $"POS: ({playerController.transform.position.x:0}, {playerController.transform.position.z:0}) | ALERT: {alertStr} | POSTURE: {playerController.CurrentPosture}{healthStr}{deathStr}";
            }
            if (_weaponBannerText != null && weaponSystem != null)
            {
                string ammo = weaponSystem.ActiveWeapon.category == WeaponSlotCategory.MeleeStealth
                    ? "MELEE"
                    : $"{weaponSystem.CurrentMagazineAmmo}/{weaponSystem.CurrentReserveAmmo}";
                string state = weaponSystem.IsReloading ? " | RELOADING" : (weaponSystem.CurrentMagazineAmmo == 0 && weaponSystem.ActiveWeapon.category != WeaponSlotCategory.MeleeStealth ? " | EMPTY" : string.Empty);
                _weaponBannerText.text = $"WPN: {weaponSystem.ActiveWeapon.displayName} | AMMO: {ammo}{state}";
            }
        }

        private void UpdateMEMapDot()
        {
            if (!isMEMapOpen || _meMapPlayerDot == null || playerController == null) return;
            float nx = Mathf.Clamp01(playerController.transform.position.x / _missionMapDimensions.x);
            float nz = Mathf.Clamp01(playerController.transform.position.z / _missionMapDimensions.y);
            _meMapPlayerDot.anchorMin = new Vector2(Mathf.Clamp01(nx - 0.012f), Mathf.Clamp01(nz - 0.015f));
            _meMapPlayerDot.anchorMax = new Vector2(Mathf.Clamp01(nx + 0.012f), Mathf.Clamp01(nz + 0.015f));
        }

        private static GameObject CreateHUDPanel(Transform parent, string name, Vector2 min, Vector2 max, Color col)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.color = col;
            return go;
        }

        private static Text CreateHUDText(Transform parent, string name, string text, Vector2 min, Vector2 max, Font font, int size, TextAnchor align, Color col)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Text t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = col;
            return t;
        }

        private static Button CreateHUDButton(Transform parent, string name, string label, Vector2 min, Vector2 max, Font font, Color bgCol, UnityEngine.Events.UnityAction onClick)
        {
            GameObject p = CreateHUDPanel(parent, name, min, max, bgCol);
            Button b = p.AddComponent<Button>();
            b.onClick.AddListener(onClick);
            CreateHUDText(p.transform, "Lbl", label, Vector2.zero, Vector2.one, font, 16, TextAnchor.MiddleCenter, Color.white);
            return b;
        }
    }
}
