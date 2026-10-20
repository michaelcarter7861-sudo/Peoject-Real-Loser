using UnityEngine;
using RealLoserStudio.Tactical.Player;
using RealLoserStudio.Tactical.UI;

namespace RealLoserStudio.Tactical.Core
{
    /// <summary>
    /// Lightweight mission truck driving controller for M08. Uses the same mobile joystick and keyboard axes
    /// as the operative controller, follows collision checks, and keeps the first-person camera attached.
    /// This is a gameplay vehicle controller, not a suspension/tyre simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionCargoVehicleController : MonoBehaviour
    {
        [Min(1f)] public float forwardSpeed = 13f;
        [Min(1f)] public float reverseSpeed = 5f;
        [Min(1f)] public float steeringDegreesPerSecond = 48f;
        public bool IsOccupied { get; private set; }
        private PlayerTacticalController _driver;
        private MobileTouchHUDAndMEMap _hud;
        private Transform _previousParent;
        private Quaternion _driverLocalRotation;
        private Vector3 _routeTarget;
        private bool _hasRouteTarget;

        public void SetRouteTarget(Vector3 target) { _routeTarget = target; _hasRouteTarget = true; }

        public bool BeginDrive(PlayerTacticalController driver, MobileTouchHUDAndMEMap hud)
        {
            if (driver == null || IsOccupied) return false;
            _driver = driver;
            _hud = hud;
            _previousParent = driver.transform.parent;
            _driverLocalRotation = Quaternion.identity;
            if (_hasRouteTarget)
            {
                Vector3 route = _routeTarget - transform.position;
                route.y = 0f;
                if (route.sqrMagnitude > 1f) transform.rotation = Quaternion.LookRotation(route.normalized, Vector3.up);
            }
            driver.transform.SetParent(transform, true);
            driver.transform.position = transform.position;
            driver.transform.rotation = transform.rotation;
            driver.enabled = false;
            IsOccupied = true;
            return true;
        }

        public void StopDrive()
        {
            if (_driver != null)
            {
                _driver.transform.SetParent(_previousParent, true);
                _driver.enabled = true;
            }
            _driver = null;
            _hud = null;
            IsOccupied = false;
        }

        private void Update()
        {
            if (!IsOccupied || _driver == null) return;
            Vector2 input = _hud != null ? _hud.currentJoystickInput : Vector2.zero;
            float kx = Input.GetAxisRaw("Horizontal");
            float ky = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(kx) > Mathf.Abs(input.x)) input.x = kx;
            if (Mathf.Abs(ky) > Mathf.Abs(input.y)) input.y = ky;
            input = Vector2.ClampMagnitude(input, 1f);

            if (Mathf.Abs(input.x) > 0.05f && Mathf.Abs(input.y) > 0.02f)
            {
                float reverseSteer = input.y < 0f ? -1f : 1f;
                transform.Rotate(Vector3.up, input.x * reverseSteer * steeringDegreesPerSecond * Time.deltaTime, Space.World);
            }
            if (Mathf.Abs(input.y) > 0.03f)
            {
                float speed = input.y >= 0f ? forwardSpeed : reverseSpeed;
                Vector3 delta = transform.forward * (input.y * speed * Time.deltaTime);
                if (!WouldHitObstacle(delta)) transform.position += delta;
            }
            _driver.transform.localPosition = Vector3.zero;
            _driver.transform.localRotation = _driverLocalRotation;
        }

        private bool WouldHitObstacle(Vector3 delta)
        {
            float distance = delta.magnitude + 0.7f;
            if (distance < 0.75f) return false;
            Vector3 origin = transform.position + Vector3.up * 0.7f;
            RaycastHit[] hits = Physics.SphereCastAll(origin, 0.65f, delta.normalized, distance);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hit = hits[i].transform;
                if (hit == null || hit == transform || hit.IsChildOf(transform) || hit.IsChildOf(_driver.transform)) continue;
                if (hits[i].distance > 0.15f) return true;
            }
            return false;
        }

        private void OnDisable() { if (IsOccupied) StopDrive(); }
    }
}
