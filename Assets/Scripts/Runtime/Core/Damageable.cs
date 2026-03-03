using System;
using UnityEngine;

namespace RoguePulse
{
    public class Damageable : MonoBehaviour
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private float destroyDelay = 0.1f;

        private float _currentHp;
        private bool _dead;

        public float MaxHp => maxHp;
        public float CurrentHp => _currentHp;
        public bool IsDead => _dead;
        public bool IsInvulnerable { get; set; }

        public event Action<Damageable, float, float> OnHealthChanged;
        public event Action<Damageable> OnDeath;

        private void Awake()
        {
            ResetHealth();
        }

        public void ResetHealth()
        {
            _dead = false;
            IsInvulnerable = false;
            _currentHp = Mathf.Max(1f, maxHp);
            OnHealthChanged?.Invoke(this, _currentHp, maxHp);
        }

        public bool TakeDamage(float amount)
        {
            if (_dead || IsInvulnerable || amount <= 0f)
            {
                return false;
            }

            _currentHp = Mathf.Max(0f, _currentHp - amount);
            OnHealthChanged?.Invoke(this, _currentHp, maxHp);

            if (_currentHp <= 0f)
            {
                Die();
            }

            return true;
        }

        public void Heal(float amount)
        {
            if (_dead || amount <= 0f)
            {
                return;
            }

            _currentHp = Mathf.Min(maxHp, _currentHp + amount);
            OnHealthChanged?.Invoke(this, _currentHp, maxHp);
        }

        public void AddMaxHp(float amount, bool refill)
        {
            if (amount <= 0f)
            {
                return;
            }

            maxHp = Mathf.Max(1f, maxHp + amount);
            _currentHp = refill ? maxHp : Mathf.Min(_currentHp, maxHp);
            OnHealthChanged?.Invoke(this, _currentHp, maxHp);
        }

        public void SetDeathDestroyBehavior(bool enabled, float delay = 0.1f)
        {
            destroyOnDeath = enabled;
            destroyDelay = Mathf.Max(0f, delay);
        }

        private void Die()
        {
            if (_dead)
            {
                return;
            }

            _dead = true;
            OnDeath?.Invoke(this);
            if (destroyOnDeath)
            {
                Destroy(gameObject, destroyDelay);
            }
        }
    }
}
