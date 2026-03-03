using System;
using UnityEngine;

namespace RoguePulse
{
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        [SerializeField] private int startGold;

        public int Gold { get; private set; }
        public event Action<int> OnGoldChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Gold = Mathf.Max(0, startGold);
            OnGoldChanged?.Invoke(Gold);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        public bool CanAfford(int cost)
        {
            return cost <= 0 || Gold >= cost;
        }

        public bool SpendGold(int cost)
        {
            if (!CanAfford(cost))
            {
                return false;
            }

            Gold -= Mathf.Max(0, cost);
            OnGoldChanged?.Invoke(Gold);
            return true;
        }
    }
}
