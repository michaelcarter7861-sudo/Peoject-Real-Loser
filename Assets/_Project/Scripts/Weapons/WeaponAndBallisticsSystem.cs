using System;
using System.Collections.Generic;
using UnityEngine;
using RealLoserStudio.Tactical.Stealth;
using RealLoserStudio.Tactical.AI;
using RealLoserStudio.Tactical.Traps;

namespace RealLoserStudio.Tactical.Weapons
{
    public enum WeaponSlotCategory
    {
        Sidearm = 0,
        SMG = 1,
        AssaultRifle = 2,
        Shotgun = 3,
        SniperRifle = 4,
        HeavyWeapon = 5,
        MeleeStealth = 6
    }

    [Serializable]
    public struct WeaponDefinition
    {
        public string weaponId;
        public string displayName;
        public WeaponSlotCategory category;
        public float baseDamage;
        public float roundsPerMinute;
        public int magazineCapacity;
        public float reloadSeconds;
        public float effectiveRangeMeters;
        public float noiseRadiusMeters;
        public bool isSuppressed;
    }

        /// <summary>
    /// Master Weapon & Ballistics System registering all 26 weapons (24 firearms + knife + garrote).
    /// </summary>
    public class WeaponAndBallisticsSystem : MonoBehaviour
    {
        public static readonly WeaponDefinition[] All26Weapons = new WeaponDefinition[]
        {
            // 5 Pistols & Sidearms
            new WeaponDefinition { weaponId = "wpn_m9_beretta", displayName = "M9 Beretta 9mm", category = WeaponSlotCategory.Sidearm, baseDamage = 28f, roundsPerMinute = 420f, magazineCapacity = 15, reloadSeconds = 1.8f, effectiveRangeMeters = 45f, noiseRadiusMeters = 38f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_glock17_tactical", displayName = "G17 Tactical", category = WeaponSlotCategory.Sidearm, baseDamage = 26f, roundsPerMinute = 480f, magazineCapacity = 17, reloadSeconds = 1.7f, effectiveRangeMeters = 45f, noiseRadiusMeters = 38f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_usp45_suppressed", displayName = "USP .45 Suppressed", category = WeaponSlotCategory.Sidearm, baseDamage = 34f, roundsPerMinute = 380f, magazineCapacity = 12, reloadSeconds = 1.9f, effectiveRangeMeters = 40f, noiseRadiusMeters = 8.5f, isSuppressed = true },
            new WeaponDefinition { weaponId = "wpn_makarov_pm", displayName = "Makarov PM", category = WeaponSlotCategory.Sidearm, baseDamage = 25f, roundsPerMinute = 390f, magazineCapacity = 8, reloadSeconds = 1.7f, effectiveRangeMeters = 35f, noiseRadiusMeters = 36f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_deagle_50ae", displayName = "Desert Eagle .50 AE", category = WeaponSlotCategory.Sidearm, baseDamage = 68f, roundsPerMinute = 210f, magazineCapacity = 7, reloadSeconds = 2.3f, effectiveRangeMeters = 60f, noiseRadiusMeters = 65f, isSuppressed = false },

            // 5 SMGs
            new WeaponDefinition { weaponId = "wpn_mp5sd_integral", displayName = "MP5SD3 Integral Suppressed", category = WeaponSlotCategory.SMG, baseDamage = 24f, roundsPerMinute = 800f, magazineCapacity = 30, reloadSeconds = 2.2f, effectiveRangeMeters = 65f, noiseRadiusMeters = 9.5f, isSuppressed = true },
            new WeaponDefinition { weaponId = "wpn_mp5a3_tactical", displayName = "MP5A3 Tactical", category = WeaponSlotCategory.SMG, baseDamage = 27f, roundsPerMinute = 800f, magazineCapacity = 30, reloadSeconds = 2.1f, effectiveRangeMeters = 75f, noiseRadiusMeters = 45f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_uzi_pro", displayName = "Micro Uzi 9mm", category = WeaponSlotCategory.SMG, baseDamage = 22f, roundsPerMinute = 950f, magazineCapacity = 32, reloadSeconds = 2.0f, effectiveRangeMeters = 50f, noiseRadiusMeters = 46f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_pp19_bizon", displayName = "PP-19 Bizon Helical", category = WeaponSlotCategory.SMG, baseDamage = 25f, roundsPerMinute = 680f, magazineCapacity = 64, reloadSeconds = 2.8f, effectiveRangeMeters = 70f, noiseRadiusMeters = 44f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_p90_pdw", displayName = "FN P90 5.7mm", category = WeaponSlotCategory.SMG, baseDamage = 26f, roundsPerMinute = 900f, magazineCapacity = 50, reloadSeconds = 2.5f, effectiveRangeMeters = 85f, noiseRadiusMeters = 42f, isSuppressed = false },

            // 6 Assault Rifles & Carbines
            new WeaponDefinition { weaponId = "wpn_m4a1_sopmod", displayName = "M4A1 SOPMOD", category = WeaponSlotCategory.AssaultRifle, baseDamage = 36f, roundsPerMinute = 780f, magazineCapacity = 30, reloadSeconds = 2.3f, effectiveRangeMeters = 160f, noiseRadiusMeters = 14f, isSuppressed = true },
            new WeaponDefinition { weaponId = "wpn_ak47_m", displayName = "AKM 7.62x39", category = WeaponSlotCategory.AssaultRifle, baseDamage = 44f, roundsPerMinute = 600f, magazineCapacity = 30, reloadSeconds = 2.5f, effectiveRangeMeters = 175f, noiseRadiusMeters = 68f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_ak74u_krinkov", displayName = "AKS-74U Compact", category = WeaponSlotCategory.AssaultRifle, baseDamage = 34f, roundsPerMinute = 700f, magazineCapacity = 30, reloadSeconds = 2.3f, effectiveRangeMeters = 120f, noiseRadiusMeters = 62f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_g36c_commando", displayName = "G36C Commando", category = WeaponSlotCategory.AssaultRifle, baseDamage = 35f, roundsPerMinute = 750f, magazineCapacity = 30, reloadSeconds = 2.2f, effectiveRangeMeters = 150f, noiseRadiusMeters = 58f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_aug_a2", displayName = "Steyr AUG A2 Optical", category = WeaponSlotCategory.AssaultRifle, baseDamage = 37f, roundsPerMinute = 680f, magazineCapacity = 30, reloadSeconds = 2.6f, effectiveRangeMeters = 190f, noiseRadiusMeters = 58f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_galil_ace", displayName = "Galil ACE 32", category = WeaponSlotCategory.AssaultRifle, baseDamage = 42f, roundsPerMinute = 650f, magazineCapacity = 35, reloadSeconds = 2.4f, effectiveRangeMeters = 170f, noiseRadiusMeters = 64f, isSuppressed = false },

            // 3 Shotguns
            new WeaponDefinition { weaponId = "wpn_spas12_tactical", displayName = "SPAS-12 Combat", category = WeaponSlotCategory.Shotgun, baseDamage = 96f, roundsPerMinute = 180f, magazineCapacity = 8, reloadSeconds = 3.8f, effectiveRangeMeters = 28f, noiseRadiusMeters = 72f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_m870_breacher", displayName = "M870 Breacher", category = WeaponSlotCategory.Shotgun, baseDamage = 105f, roundsPerMinute = 85f, magazineCapacity = 6, reloadSeconds = 3.4f, effectiveRangeMeters = 25f, noiseRadiusMeters = 74f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_saiga12_auto", displayName = "Saiga-12K Box Mag", category = WeaponSlotCategory.Shotgun, baseDamage = 84f, roundsPerMinute = 300f, magazineCapacity = 10, reloadSeconds = 2.7f, effectiveRangeMeters = 26f, noiseRadiusMeters = 70f, isSuppressed = false },

            // 3 Sniper & Marksman Rifles
            new WeaponDefinition { weaponId = "wpn_svd_dragunov", displayName = "SVD Dragunov 7.62x54R", category = WeaponSlotCategory.SniperRifle, baseDamage = 95f, roundsPerMinute = 140f, magazineCapacity = 10, reloadSeconds = 2.8f, effectiveRangeMeters = 380f, noiseRadiusMeters = 85f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_awm_l115a3", displayName = "AWM .338 Lapua", category = WeaponSlotCategory.SniperRifle, baseDamage = 145f, roundsPerMinute = 42f, magazineCapacity = 5, reloadSeconds = 3.2f, effectiveRangeMeters = 500f, noiseRadiusMeters = 95f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_m82_barrett", displayName = "Barrett M82A1 .50 BMG", category = WeaponSlotCategory.SniperRifle, baseDamage = 210f, roundsPerMinute = 90f, magazineCapacity = 10, reloadSeconds = 3.9f, effectiveRangeMeters = 650f, noiseRadiusMeters = 125f, isSuppressed = false },

            // 2 Heavy Weapons
            new WeaponDefinition { weaponId = "wpn_m249_saw", displayName = "M249 SAW Para", category = WeaponSlotCategory.HeavyWeapon, baseDamage = 38f, roundsPerMinute = 800f, magazineCapacity = 100, reloadSeconds = 5.2f, effectiveRangeMeters = 200f, noiseRadiusMeters = 78f, isSuppressed = false },
            new WeaponDefinition { weaponId = "wpn_rpg7_launcher", displayName = "RPG-7V Anti-Armor", category = WeaponSlotCategory.HeavyWeapon, baseDamage = 350f, roundsPerMinute = 15f, magazineCapacity = 1, reloadSeconds = 4.5f, effectiveRangeMeters = 220f, noiseRadiusMeters = 140f, isSuppressed = false },

            // 2 Melee & Stealth Takedown Weapons
            new WeaponDefinition { weaponId = "wpn_knife_mesh", displayName = "KA-BAR Tactical Knife", category = WeaponSlotCategory.MeleeStealth, baseDamage = 150f, roundsPerMinute = 90f, magazineCapacity = 1, reloadSeconds = 0f, effectiveRangeMeters = 1.9f, noiseRadiusMeters = 2.5f, isSuppressed = true },
            new WeaponDefinition { weaponId = "wpn_garrote_mesh", displayName = "Silent Fiber Garrote", category = WeaponSlotCategory.MeleeStealth, baseDamage = 200f, roundsPerMinute = 30f, magazineCapacity = 1, reloadSeconds = 0f, effectiveRangeMeters = 1.5f, noiseRadiusMeters = 0.8f, isSuppressed = true }
        };

        public int activeWeaponIndex;
        public ArticulatedWeaponRig currentRig;
        public Transform viewmodelParent;
        private GameObject _activeViewmodelInstance;

        [Header("Runtime weapon state")]
        public LayerMask hitMask = ~0;
        public bool infiniteAmmoForTesting;
        public int CurrentMagazineAmmo => _magazineAmmo != null ? _magazineAmmo[SafeWeaponIndex] : ActiveWeapon.magazineCapacity;
        public int CurrentReserveAmmo => _reserveAmmo != null ? _reserveAmmo[SafeWeaponIndex] : ActiveWeapon.magazineCapacity * 4;
        public bool IsReloading => _reloadRemaining > 0f;
        public bool IsReadyToFire => !IsReloading && Time.time >= _nextFireTime && (infiniteAmmoForTesting || CurrentMagazineAmmo > 0);

        private int[] _magazineAmmo;
        private int[] _reserveAmmo;
        private float _nextFireTime;
        private float _reloadRemaining;
        private int _reloadWeaponIndex = -1;

        private int SafeWeaponIndex => Mathf.Clamp(activeWeaponIndex, 0, All26Weapons.Length - 1);
        public WeaponDefinition ActiveWeapon => All26Weapons[SafeWeaponIndex];

        public bool TrySetActiveWeapon(int requestedIndex)
        {
            if (IsReloading) return false;
            activeWeaponIndex = Mathf.Clamp(requestedIndex, 0, All26Weapons.Length - 1);
            RefreshFirstPersonViewmodel();
            return true;
        }

        public bool TrySetActiveWeaponById(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return false;
            for (int i = 0; i < All26Weapons.Length; i++)
            {
                if (string.Equals(All26Weapons[i].weaponId, weaponId, StringComparison.OrdinalIgnoreCase))
                {
                    return TrySetActiveWeapon(i);
                }
            }
            return false;
        }

        public void RefreshFirstPersonViewmodel()
        {
            if (viewmodelParent == null) return;
            if (_activeViewmodelInstance != null)
            {
                Destroy(_activeViewmodelInstance);
                _activeViewmodelInstance = null;
            }
            WeaponDefinition def = ActiveWeapon;
            _activeViewmodelInstance = RealLoserStudio.Tactical.Core.RuntimeGLBAndMeshLoader.InstantiateGLB(
                "Art/Weapons/" + def.weaponId + ".glb",
                Vector3.zero,
                Quaternion.identity,
                viewmodelParent,
                addCollider: false
            );
            if (_activeViewmodelInstance != null)
            {
                _activeViewmodelInstance.name = "FP_Viewmodel_" + def.weaponId;
                _activeViewmodelInstance.transform.localPosition = new Vector3(0.24f, -0.22f, 0.42f);
                _activeViewmodelInstance.transform.localRotation = Quaternion.identity;
                currentRig = _activeViewmodelInstance.GetComponent<ArticulatedWeaponRig>();
                if (currentRig == null) currentRig = _activeViewmodelInstance.AddComponent<ArticulatedWeaponRig>();
            }
        }

        private void Awake()
        {
            _magazineAmmo = new int[All26Weapons.Length];
            _reserveAmmo = new int[All26Weapons.Length];
            for (int i = 0; i < All26Weapons.Length; i++)
            {
                _magazineAmmo[i] = All26Weapons[i].magazineCapacity;
                _reserveAmmo[i] = All26Weapons[i].magazineCapacity * 4;
            }
        }

        private void Update()
        {
            if (_reloadRemaining <= 0f) return;
            _reloadRemaining -= Time.deltaTime;
            if (_reloadRemaining > 0f) return;

            int idx = _reloadWeaponIndex >= 0 ? Mathf.Clamp(_reloadWeaponIndex, 0, All26Weapons.Length - 1) : SafeWeaponIndex;
            int missing = Mathf.Max(0, All26Weapons[idx].magazineCapacity - _magazineAmmo[idx]);
            int loaded = Mathf.Min(missing, _reserveAmmo[idx]);
            _magazineAmmo[idx] += loaded;
            _reserveAmmo[idx] -= loaded;
            _reloadRemaining = 0f;
            _reloadWeaponIndex = -1;
        }

        public bool StartReload()
        {
            if (_magazineAmmo == null) Awake();
            int idx = SafeWeaponIndex;
            WeaponDefinition def = All26Weapons[idx];
            if (def.category == WeaponSlotCategory.MeleeStealth || IsReloading || _magazineAmmo[idx] >= def.magazineCapacity || _reserveAmmo[idx] <= 0)
                return false;
            _reloadWeaponIndex = idx;
            _reloadRemaining = Mathf.Max(0.1f, def.reloadSeconds);
            return true;
        }

        public void FireActiveWeapon(Vector3 muzzleOrigin, Vector3 aimDirection)
        {
            if (_magazineAmmo == null) Awake();
            if (IsReloading || Time.time < _nextFireTime || aimDirection.sqrMagnitude < 0.0001f) return;

            WeaponDefinition def = ActiveWeapon;
            bool melee = def.category == WeaponSlotCategory.MeleeStealth;
            int idx = SafeWeaponIndex;
            if (!melee && !infiniteAmmoForTesting)
            {
                if (_magazineAmmo[idx] <= 0) return;
                _magazineAmmo[idx]--;
            }

            float secondsPerShot = 60f / Mathf.Max(1f, def.roundsPerMinute);
            _nextFireTime = Time.time + secondsPerShot;
            NoiseCategory nCat = def.isSuppressed ? NoiseCategory.SuppressedGunshot : NoiseCategory.UnsuppressedGunshot;
            if (!melee)
                StealthAndDetectionSystem.Instance?.EmitNoise(muzzleOrigin, def.noiseRadiusMeters, nCat, !def.isSuppressed && def.noiseRadiusMeters >= 60f);

            Vector3 direction = aimDirection.normalized;
            if (melee)
            {
                ApplyMeleeHit(muzzleOrigin, direction, def);
                return;
            }

            int pellets = def.category == WeaponSlotCategory.Shotgun ? 8 : 1;
            float pelletDamage = def.baseDamage / pellets;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 shotDirection = direction;
                if (pellets > 1)
                    shotDirection = Quaternion.Euler(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-4f, 4f), 0f) * direction;
                if (TryRaycastWorld(muzzleOrigin, shotDirection, def.effectiveRangeMeters, out RaycastHit hit))
                    ApplyHit(hit, def, pelletDamage, muzzleOrigin);
            }

            if (currentRig != null) currentRig.AnimateSlideReciprocation(0.5f);
        }

        // Ignore colliders belonging to the shooter so a muzzle placed near the player capsule
        // cannot consume the shot before it reaches the world.
        private bool TryRaycastWorld(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit selectedHit)
        {
            selectedHit = default(RaycastHit);
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance, hitMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].collider != null ? hits[i].collider.transform : null;
                if (hitTransform == null) continue;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                selectedHit = hits[i];
                return true;
            }
            return false;
        }

        private void ApplyMeleeHit(Vector3 origin, Vector3 direction, WeaponDefinition def)
        {
            Collider[] hits = Physics.OverlapSphere(origin + direction * (def.effectiveRangeMeters * 0.55f), def.effectiveRangeMeters * 0.65f, hitMask, QueryTriggerInteraction.Ignore);
            float bestDot = -1f;
            EnemyAIFSMSystem bestEnemy = null;
            for (int i = 0; i < hits.Length; i++)
            {
                EnemyAIFSMSystem enemy = hits[i].GetComponentInParent<EnemyAIFSMSystem>();
                if (enemy == null || enemy.IsNeutralized) continue;
                Vector3 toTarget = enemy.transform.position - origin;
                if (toTarget.sqrMagnitude > def.effectiveRangeMeters * def.effectiveRangeMeters) continue;
                float dot = Vector3.Dot(direction, toTarget.normalized);
                if (dot > 0.35f && dot > bestDot) { bestDot = dot; bestEnemy = enemy; }
            }
            if (bestEnemy != null) bestEnemy.ApplyDamage(def.baseDamage, true);
        }

        private void ApplyHit(RaycastHit hit, WeaponDefinition def, float damage, Vector3 origin)
        {
            EnemyAIFSMSystem enemy = hit.collider.GetComponentInParent<EnemyAIFSMSystem>();
            if (enemy != null)
            {
                float distance = Vector3.Distance(origin, hit.point);
                float falloff = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(distance / Mathf.Max(1f, def.effectiveRangeMeters)));
                enemy.ApplyDamage(damage * falloff);
                return;
            }

            TrapAndHazardSystem trap = hit.collider.GetComponentInParent<TrapAndHazardSystem>();
            if (trap != null)
            {
                trap.hitPoints -= damage;
                if (trap.hitPoints <= 0f) { trap.isDestroyed = true; trap.isArmed = false; }
            }
        }
    }
}
