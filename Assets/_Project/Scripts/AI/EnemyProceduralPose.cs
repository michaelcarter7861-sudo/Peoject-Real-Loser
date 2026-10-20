using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLoserStudio.Tactical.AI
{
    /// <summary>
    /// Lightweight fallback posing for imported skeletal characters that do not ship
    /// with animation clips. It does not replace authored walk/run/weapon animations;
    /// it adds subtle, deterministic motion to matching bones when a rig is present.
    /// If the model has no usable bone names, the component safely remains inactive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyProceduralPose : MonoBehaviour
    {
        [Serializable]
        private sealed class BonePose
        {
            public Transform bone;
            public Quaternion restLocalRotation;
            public BoneRole role;
        }

        private enum BoneRole { Head, Spine, LeftArm, RightArm, LeftLeg, RightLeg }
        private readonly List<BonePose> _bones = new List<BonePose>();
        private EnemyAIFSMSystem _ai;
        private Vector3 _lastPosition;
        private float _phase;
        private bool _hasRig;

        [Range(0f, 1f)] public float motionIntensity = 0.35f;
        [Range(0f, 2f)] public float scanAmount = 0.45f;

        private void Awake()
        {
            _ai = GetComponent<EnemyAIFSMSystem>();
            _lastPosition = transform.position;
            CacheRig();
        }

        private void CacheRig()
        {
            _bones.Clear();
            Transform[] nodes = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                Transform t = nodes[i];
                if (t == transform) continue;
                // Normalize common GLTF/Blender/Mixamo naming conventions. The project's
                // shipped rigs use names such as UpperArmL and UpperLegR (no separators),
                // which the earlier matcher did not recognize.
                string n = NormalizeBoneName(t.name);
                BoneRole role = default; // Fixed: Assigned default value here to prevent unassigned error
                bool matched = false;

                if (n == "head" || n.EndsWith("head")) { role = BoneRole.Head; matched = true; }
                else if (ContainsAny(n, "spine", "chest", "upperbody")) { role = BoneRole.Spine; matched = true; }
                else if (ContainsAny(n, "leftarm", "arml", "upperarml", "shoulderl", "leftshoulder")) { role = BoneRole.LeftArm; matched = true; }
                else if (ContainsAny(n, "rightarm", "armr", "upperarmr", "shoulderr", "rightshoulder")) { role = BoneRole.RightArm; matched = true; }
                else if (ContainsAny(n, "leftleg", "thighl", "upperlegl", "legl")) { role = BoneRole.LeftLeg; matched = true; }
                else if (ContainsAny(n, "rightleg", "thighr", "upperlegr", "legr")) { role = BoneRole.RightLeg; matched = true; }

                if (!matched) continue;
                // Prefer the most specific match; do not animate a parent and its child
                // for the same role, which can exaggerate the rotation.
                bool duplicate = false;
                for (int b = 0; b < _bones.Count; b++)
                    if (_bones[b].role == role) { duplicate = true; break; }
                if (duplicate) continue;
                _bones.Add(new BonePose { bone = t, restLocalRotation = t.localRotation, role = role });
            }
            _hasRig = _bones.Count >= 2;
        }

        private void LateUpdate()
        {
            if (!_hasRig) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            delta.y = 0f;
            float speed = dt > 0.0001f ? delta.magnitude / dt : 0f;
            bool moving = speed > 0.12f && _ai != null && !_ai.IsNeutralized;
            bool combat = _ai != null && (_ai.currentState == AIState.CombatCover || _ai.currentState == AIState.Flank);
            _phase += dt * (moving ? Mathf.Lerp(4.5f, 8f, Mathf.Clamp01(speed / 4.5f)) : 1.6f);

            for (int i = 0; i < _bones.Count; i++)
            {
                BonePose p = _bones[i];
                if (p.bone == null) continue;
                Vector3 euler = Vector3.zero;
                float gait = moving ? Mathf.Sin(_phase) * motionIntensity : Mathf.Sin(_phase * 0.55f) * 0.035f;
                switch (p.role)
                {
                    case BoneRole.Head:
                        float scan = _ai != null && (_ai.currentState == AIState.Idle || _ai.currentState == AIState.Patrol)
                            ? Mathf.Sin(_phase * 0.31f) * scanAmount : 0f;
                        euler.y = scan * 16f;
                        euler.x = combat ? -5f : Mathf.Sin(_phase * 0.7f) * 1.5f;
                        break;
                    case BoneRole.Spine:
                        euler.x = combat ? -4f : (moving ? -2.0f : 0f);
                        euler.z = moving ? Mathf.Sin(_phase * 0.5f) * 1.8f : 0f;
                        break;
                    case BoneRole.LeftArm:
                        euler.x = combat ? -22f : gait * 24f;
                        euler.z = combat ? -7f : 3f;
                        break;
                    case BoneRole.RightArm:
                        euler.x = combat ? -25f : -gait * 24f;
                        euler.z = combat ? 7f : -3f;
                        break;
                    case BoneRole.LeftLeg:
                        euler.x = moving ? gait * 28f : 0f;
                        break;
                    case BoneRole.RightLeg:
                        euler.x = moving ? -gait * 28f : 0f;
                        break;
                }
                p.bone.localRotation = p.restLocalRotation * Quaternion.Euler(euler);
            }
        }

        private static string NormalizeBoneName(string value)
        {
            return value.ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "").Replace(".", "");
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
                if (value.Contains(tokens[i])) return true;
            return false;
        }
    }
}
