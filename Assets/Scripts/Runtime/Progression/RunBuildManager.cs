using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePulse
{
    public class RunBuildManager : MonoBehaviour
    {
        public static RunBuildManager Instance { get; private set; }

        [Header("Player References")]
        public Damageable playerDamageable;
        public PlayerStats playerStats;

        [Header("Seed")]
        [SerializeField] private int randomSeed;

        private readonly List<BuildItemData> _items = new List<BuildItemData>();
        private readonly Dictionary<BuildTag, int> _tagStacks = new Dictionary<BuildTag, int>();
        private System.Random _rng;
        private float _goldGainMultiplier = 1f;

        public event Action<BuildItemData, string> OnItemAcquired;
        public event Action OnBuildChanged;

        public float GoldGainMultiplier => _goldGainMultiplier;
        public int ItemCount => _items.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            int seed = randomSeed != 0 ? randomSeed : Environment.TickCount;
            _rng = new System.Random(seed);
        }

        private void Start()
        {
            TryBindPlayer();
            OnBuildChanged?.Invoke();
        }

        private void Update()
        {
            if (playerDamageable == null || playerStats == null)
            {
                TryBindPlayer();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetPlayerReferences(Damageable damageable, PlayerStats stats)
        {
            playerDamageable = damageable;
            playerStats = stats;
        }

        public BuildItemData RollDropRewardItem(bool elite)
        {
            return BuildItemCatalog.RollDrop(_rng, elite, GetPrimaryTag());
        }

        public BuildItemData RollChestRewardItem()
        {
            return BuildItemCatalog.RollChest(_rng, GetPrimaryTag());
        }

        public BuildItemData RollShopOfferItem()
        {
            int stage = 1;
            if (GameManager.Instance != null && GameManager.Instance.spawnDirector != null)
            {
                stage = Mathf.Max(1, GameManager.Instance.spawnDirector.CurrentStageDisplay);
            }

            return BuildItemCatalog.RollShop(_rng, stage, GetPrimaryTag());
        }

        public BuildItemData RollRiskRewardItem()
        {
            return BuildItemCatalog.RollRisk(_rng, GetPrimaryTag());
        }

        public BuildItemData RollUpgradeRewardItem(ItemRarity minRarity)
        {
            return BuildItemCatalog.RollUpgrade(_rng, minRarity, GetPrimaryTag());
        }

        public bool AcquireItem(BuildItemData item, string source)
        {
            if (item == null)
            {
                return false;
            }

            TryBindPlayer();

            _items.Add(item);
            AddTag(item.PrimaryTag);

            if (playerStats != null)
            {
                if (item.DamagePercent != 0f)
                {
                    playerStats.AddDamagePercent(item.DamagePercent);
                }

                if (item.MoveSpeedPercent != 0f)
                {
                    playerStats.AddMoveSpeedPercent(item.MoveSpeedPercent);
                }
            }

            if (playerDamageable != null && item.MaxHpFlat > 0f)
            {
                playerDamageable.AddMaxHp(item.MaxHpFlat, refill: true);
            }

            if (item.GoldGainPercent > 0f)
            {
                AddGoldGainPercent(item.GoldGainPercent);
            }

            OnItemAcquired?.Invoke(item, source);
            OnBuildChanged?.Invoke();
            return true;
        }

        public void AddGoldGainPercent(float percent)
        {
            if (percent <= 0f)
            {
                return;
            }

            _goldGainMultiplier *= (1f + percent);
            _goldGainMultiplier = Mathf.Clamp(_goldGainMultiplier, 0.1f, 6f);
            OnBuildChanged?.Invoke();
        }

        public int EvaluateGoldAmount(int baseAmount)
        {
            if (baseAmount <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.RoundToInt(baseAmount * _goldGainMultiplier));
        }

        public string BuildSummary()
        {
            return $"Items:{_items.Count} Focus:{GetPrimaryTag()} Goldx{_goldGainMultiplier:0.00}";
        }

        public BuildTag GetPrimaryTag()
        {
            if (_tagStacks.Count == 0)
            {
                return BuildTag.Utility;
            }

            BuildTag best = BuildTag.Utility;
            int bestCount = int.MinValue;
            foreach (KeyValuePair<BuildTag, int> kv in _tagStacks)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    best = kv.Key;
                }
            }

            return best;
        }

        private void AddTag(BuildTag tag)
        {
            if (!_tagStacks.ContainsKey(tag))
            {
                _tagStacks[tag] = 0;
            }

            _tagStacks[tag] += 1;
        }

        private void TryBindPlayer()
        {
            if (playerDamageable != null && playerStats != null)
            {
                return;
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                return;
            }

            if (playerDamageable == null)
            {
                playerDamageable = player.GetComponent<Damageable>();
            }

            if (playerStats == null)
            {
                playerStats = player.GetComponent<PlayerStats>();
            }
        }
    }
}
