using System;
using UnityEngine;

namespace RealLoserStudio.Tactical.Traps
{
    /// <summary>
    /// Power Source node (`prop_breaker_box_a` or `prop_generator_diesel_a`) that powers T12 Electric Fences
    /// or T5 Laser Grids.
    /// </summary>
    public class PowerSourceNode : MonoBehaviour
    {
        public string powerSourceId = "prop_breaker_box_a_01";
        public string assetType = "prop_breaker_box_a"; // or prop_generator_diesel_a
        public bool isPoweredOn = true;

        public event Action<bool> OnPowerStateChanged;

        public void CutPower()
        {
            if (!isPoweredOn) return;
            isPoweredOn = false;
            OnPowerStateChanged?.Invoke(false);
        }
    }
}
