using System;
using UnityEngine;

namespace RealLoserStudio.Tactical.Player
{
    /// <summary>
    /// Centralized player health/damage receiver for enemy fire, hazards, and future UI.
    /// Attach to the same root as PlayerTacticalController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDamageReceiver : MonoBehaviour
    {
        [Min(1f)] public float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        public bool IsDead { get; private set; }
        public float CurrentHealth => currentHealth;
        public float HealthNormalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

        public event Action<float, float> OnHealthChanged;
        public event Action OnPlayerDied;

        private void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        }

        public void ApplyDamage(float damage, GameObject source = null)
        {
            if (IsDead || damage <= 0f) return;
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            if (currentHealth > 0f) return;

            IsDead = true;
            PlayerTacticalController controller = GetComponent<PlayerTacticalController>();
            if (controller != null) controller.enabled = false;
            OnPlayerDied?.Invoke();
        }

        public void RestoreHealth(float amount)
        {
            if (IsDead || amount <= 0f) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void ResetHealth()
        {
            IsDead = false;
            currentHealth = maxHealth;
            PlayerTacticalController controller = GetComponent<PlayerTacticalController>();
            if (controller != null) controller.enabled = true;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
