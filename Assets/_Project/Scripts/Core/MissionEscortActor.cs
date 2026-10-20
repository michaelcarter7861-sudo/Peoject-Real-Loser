using UnityEngine;

namespace RealLoserStudio.Tactical.Core
{
    /// <summary>Runtime escort NPC with follow behavior, obstacle avoidance and a health/failure state.</summary>
    [DisallowMultipleComponent]
    public sealed class MissionEscortActor : MonoBehaviour
    {
        [Min(1f)] public float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [Min(0.1f)] public float followSpeed = 2.2f;
        [Min(0.5f)] public float followDistance = 1.8f;
        public Transform followTarget;
        public bool IsDead { get; private set; }
        public float CurrentHealth => currentHealth;

        private void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
            AddHeadAndGear();
        }

        private void Update()
        {
            if (IsDead || followTarget == null) return;
            Vector3 toTarget = followTarget.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance > followDistance)
            {
                Vector3 desired = followTarget.position - followTarget.forward * followDistance;
                desired.y = transform.position.y;
                Vector3 direction = desired - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.02f)
                {
                    direction.Normalize();
                    Vector3 origin = transform.position + Vector3.up * 0.75f;
                    if (Physics.SphereCast(origin, 0.28f, direction, out RaycastHit hit, 0.8f))
                    {
                        if (hit.transform != transform && !hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(followTarget))
                        {
                            Vector3 side = Vector3.Cross(Vector3.up, direction);
                            direction = (direction + side * (Vector3.Dot(side, hit.normal) >= 0f ? 0.8f : -0.8f)).normalized;
                        }
                    }
                    transform.position += direction * followSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 5f * Time.deltaTime);
                }
            }
            else if (toTarget.sqrMagnitude > 0.02f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget.normalized), 3f * Time.deltaTime);
            }
        }

        public void ApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (currentHealth > 0f) return;
            IsDead = true;
            followTarget = null;
            Collider c = GetComponent<Collider>();
            if (c != null) c.enabled = false;
            transform.rotation = Quaternion.Euler(85f, transform.eulerAngles.y, 0f);
        }

        private void AddHeadAndGear()
        {
            if (transform.Find("InformantHead") != null) return;
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "InformantHead";
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            head.transform.localScale = new Vector3(0.48f, 0.48f, 0.48f);
            Collider hc = head.GetComponent<Collider>();
            if (hc != null) Destroy(hc);
            Renderer hr = head.GetComponent<Renderer>();
            if (hr != null) hr.material.color = new Color(0.55f, 0.39f, 0.28f);
        }
    }
}
