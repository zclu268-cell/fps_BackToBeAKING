using System;
using UnityEngine;

namespace RoguePulse
{
    public class PlayerStats : MonoBehaviour
    {
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private float moveSpeedMultiplier = 1f;

        public float DamageMultiplier => damageMultiplier;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;

        public event Action OnStatsChanged;

        public void AddDamagePercent(float percent)
        {
            damageMultiplier = Mathf.Max(0.1f, damageMultiplier + percent);
            OnStatsChanged?.Invoke();
        }

        public void AddMoveSpeedPercent(float percent)
        {
            moveSpeedMultiplier = Mathf.Max(0.1f, moveSpeedMultiplier + percent);
            OnStatsChanged?.Invoke();
        }
    }
}
