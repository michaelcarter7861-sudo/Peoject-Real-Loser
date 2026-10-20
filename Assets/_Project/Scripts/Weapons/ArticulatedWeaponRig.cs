using UnityEngine;

namespace RealLoserStudio.Tactical.Weapons
{
    /// <summary>
    /// Runtime binding for each procedural `.glb` weapon with 4 articulated sub-meshes
    /// and 6 required reference empties (`GripR`, `GripL`, `Muzzle`, `Eject`, `Mag`, `SightAnchor`).
    /// </summary>
    public class ArticulatedWeaponRig : MonoBehaviour
    {
        [Header("4 Articulated Sub-Meshes")]
        public Transform subMesh0Receiver;
        public Transform subMesh1ActionSlideBolt;
        public Transform subMesh2Magazine;
        public Transform subMesh3BarrelSight;

        [Header("6 Required Reference Empties")]
        public Transform gripR;
        public Transform gripL;
        public Transform muzzle;
        public Transform eject;
        public Transform mag;
        public Transform sightAnchor;

        private Vector3 _slideRestPosition;
        private bool _slideRestPositionCached;

        private void Awake()
        {
            CacheSlideRestPosition();
        }

        private void CacheSlideRestPosition()
        {
            if (subMesh1ActionSlideBolt == null || _slideRestPositionCached) return;
            _slideRestPosition = subMesh1ActionSlideBolt.localPosition;
            _slideRestPositionCached = true;
        }

        public void AnimateSlideReciprocation(float normalizedCycle)
        {
            if (subMesh1ActionSlideBolt == null) return;
            CacheSlideRestPosition();
            float zOffset = -Mathf.Sin(Mathf.Clamp01(normalizedCycle) * Mathf.PI) * 0.045f;
            subMesh1ActionSlideBolt.localPosition = _slideRestPosition + new Vector3(0f, 0f, zOffset);
        }
    }
}
