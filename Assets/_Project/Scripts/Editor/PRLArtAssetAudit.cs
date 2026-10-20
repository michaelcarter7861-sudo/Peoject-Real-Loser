#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RealLoserStudio.Tactical.Editor
{
    public static class PRLArtAssetAudit
    {
        [MenuItem("Real Loser/Art Pipeline/Validate Authored Clips and Texture Sets")]
        public static void Validate()
        {
            string[] requiredClips = { "PRL_Idle", "PRL_Walk", "PRL_AimFire", "PRL_Reload", "PRL_Takedown" };
            string[] requiredTextureSets = { "Concrete", "DesertSand", "OliveFabric", "BrushedSteel", "WeaponSteel", "WeatheredWood", "Foliage", "Rubber" };
            List<string> errors = new List<string>();
            foreach (string clip in requiredClips)
            {
                AnimationClip asset = Resources.Load<AnimationClip>("PRLAnimations/" + clip);
                if (asset == null) errors.Add("Missing clip: " + clip);
                else if (AnimationUtility.GetCurveBindings(asset).Length < 3) errors.Add("Clip has too few transform curves: " + clip);
            }
            foreach (string set in requiredTextureSets)
            {
                if (Resources.Load<Texture2D>("PRLTextures/T_" + set + "_BaseColor") == null) errors.Add("Missing base color: " + set);
                if (Resources.Load<Texture2D>("PRLTextures/T_" + set + "_Normal") == null) errors.Add("Missing normal map: " + set);
                if (Resources.Load<Texture2D>("PRLTextures/T_" + set + "_Mask") == null) errors.Add("Missing metallic/AO/smoothness mask: " + set);
            }
            if (errors.Count == 0) Debug.Log("[PRL Art Audit] PASS: 5 authored clips and 8 complete PBR texture sets are available through Resources.");
            else Debug.LogError("[PRL Art Audit] FAIL:\n- " + string.Join("\n- ", errors.ToArray()));
        }
    }
}
#endif
