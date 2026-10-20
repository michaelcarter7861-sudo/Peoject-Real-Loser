using System.Collections.Generic;
using UnityEngine;

namespace RealLoserStudio.Tactical.Player
{
    /// <summary>
    /// Component attached to body-stashing containers (prop_locker_metal_a, prop_dumpster_metal_a, vents, closets, bushes).
    /// </summary>
    public class HidingSpotContainer : MonoBehaviour
    {
        public string spotId = "prop_locker_metal_a";
        public HidingSpotType spotType = HidingSpotType.MetalLocker;
        public int maxCapacity = 2;
        public Transform stashAnchor;
        private readonly List<GameObject> _stashedBodies = new List<GameObject>(2);

        public bool CanStashBody => _stashedBodies.Count < maxCapacity;
        public int StashedCount => _stashedBodies.Count;

        public bool TryStashBody(GameObject bodyObject)
        {
            if (!CanStashBody || bodyObject == null) return false;
            _stashedBodies.Add(bodyObject);
            if (stashAnchor != null)
            {
                bodyObject.transform.SetParent(stashAnchor, true);
                bodyObject.transform.localPosition = Vector3.zero;
            }
            bodyObject.SetActive(false); // Concealed from AI sightlines
            return true;
        }
    }
}
