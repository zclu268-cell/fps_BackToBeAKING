using System;
using UnityEngine;

namespace RoguePulse
{
    public enum LevelUpgradeType
    {
        Damage = 0,
        MoveSpeed = 1,
        MaxHp = 2,
        GoldGain = 3,
        GrantItem = 4
    }

    [Serializable]
    public sealed class LevelUpgradeChoice
    {
        public string title;
        public string description;
        public LevelUpgradeType type;
        public float value;
        public ItemRarity minRewardRarity;
    }

    public class ExperienceManager : MonoBehaviour
    {
        public static ExperienceManager Instance { get; private set; }

        [Header("Player References")]
        public Damageable playerDamageable;
        public PlayerStats playerStats;

        [Header("XP Curve")]
        [SerializeField] private int startLevel = 1;
        [SerializeField] private int xpBase = 100;
        [SerializeField] private int xpPerLevel = 60;

        [Header("Choice")]
        [SerializeField] private float autoPickDelay = 10f;
        [SerializeField] private int randomSeed;
        [Header("Guaranteed Level-Up Bonus")]
        [SerializeField, Min(0f)] private float levelUpMaxHpBonus = 12f;
        [SerializeField] private bool refillHpOnLevelUp = true;

        private int _level;
        private int _xp;
        private int _queuedUps;
        private bool _awaitingChoice;
        private float _deadline;
        private readonly LevelUpgradeChoice[] _choices = new LevelUpgradeChoice[3];
        private System.Random _rng;

        public event Action<int, int, int> OnExperienceChanged;
        public event Action<int> OnLevelChanged;
        public event Action<LevelUpgradeChoice[]> OnChoicesPresented;
        public event Action<LevelUpgradeChoice> OnChoiceApplied;

        public int Level => _level;
        public int CurrentXp => _xp;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _level = Mathf.Max(1, startLevel);
            int seed = randomSeed != 0 ? randomSeed : Environment.TickCount;
            _rng = new System.Random(seed);
        }

        private void Start()
        {
            TryBindPlayer();
            RaiseExperience();
        }

        private void Update()
        {
            if (playerDamageable == null || playerStats == null)
            {
                TryBindPlayer();
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                return;
            }

            if (!_awaitingChoice)
            {
                if (_queuedUps > 0)
                {
                    PresentChoices();
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                ResolveChoice(0);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                ResolveChoice(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                ResolveChoice(2);
                return;
            }

            if (Time.time >= _deadline)
            {
                ResolveChoice(0);
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

        public void AddExperience(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _xp += amount;
            while (_xp >= RequiredXp())
            {
                _xp -= RequiredXp();
                _level++;
                ApplyGuaranteedLevelUpBonus();
                _queuedUps++;
                OnLevelChanged?.Invoke(_level);
            }

            RaiseExperience();
        }

        private void PresentChoices()
        {
            _queuedUps = Mathf.Max(0, _queuedUps - 1);
            BuildChoices();
            _awaitingChoice = true;
            _deadline = Time.time + autoPickDelay;
            OnChoicesPresented?.Invoke(_choices);
        }

        private void BuildChoices()
        {
            LevelUpgradeType[] types =
            {
                LevelUpgradeType.Damage,
                LevelUpgradeType.MoveSpeed,
                LevelUpgradeType.MaxHp,
                LevelUpgradeType.GoldGain,
                LevelUpgradeType.GrantItem
            };

            for (int i = types.Length - 1; i > 0; i--)
            {
                int swap = _rng.Next(0, i + 1);
                LevelUpgradeType t = types[i];
                types[i] = types[swap];
                types[swap] = t;
            }

            _choices[0] = BuildChoice(types[0]);
            _choices[1] = BuildChoice(types[1]);
            _choices[2] = BuildChoice(types[2]);
        }

        private static LevelUpgradeChoice BuildChoice(LevelUpgradeType type)
        {
            if (type == LevelUpgradeType.Damage)
            {
                return new LevelUpgradeChoice { type = type, title = "Power Calibration", description = "Damage +12%", value = 0.12f };
            }

            if (type == LevelUpgradeType.MoveSpeed)
            {
                return new LevelUpgradeChoice { type = type, title = "Kinetic Frame", description = "Move +10%", value = 0.10f };
            }

            if (type == LevelUpgradeType.MaxHp)
            {
                return new LevelUpgradeChoice { type = type, title = "Vital Plating", description = "Max HP +22", value = 22f };
            }

            if (type == LevelUpgradeType.GoldGain)
            {
                return new LevelUpgradeChoice { type = type, title = "Economy Protocol", description = "Gold gain +10%", value = 0.10f };
            }

            return new LevelUpgradeChoice
            {
                type = type,
                title = "Supply Drop",
                description = "Gain random item (Uncommon+)",
                minRewardRarity = ItemRarity.Uncommon
            };
        }

        private void ResolveChoice(int index)
        {
            if (!_awaitingChoice)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, _choices.Length - 1);
            LevelUpgradeChoice choice = _choices[index];
            ApplyChoice(choice);
            OnChoiceApplied?.Invoke(choice);

            _awaitingChoice = false;
            if (_queuedUps > 0)
            {
                PresentChoices();
            }
        }

        private void ApplyChoice(LevelUpgradeChoice choice)
        {
            if (choice == null)
            {
                return;
            }

            TryBindPlayer();
            if (choice.type == LevelUpgradeType.Damage && playerStats != null)
            {
                playerStats.AddDamagePercent(choice.value);
            }
            else if (choice.type == LevelUpgradeType.MoveSpeed && playerStats != null)
            {
                playerStats.AddMoveSpeedPercent(choice.value);
            }
            else if (choice.type == LevelUpgradeType.MaxHp && playerDamageable != null)
            {
                playerDamageable.AddMaxHp(choice.value, refill: true);
            }
            else if (choice.type == LevelUpgradeType.GoldGain)
            {
                RunBuildManager.Instance?.AddGoldGainPercent(choice.value);
            }
            else if (choice.type == LevelUpgradeType.GrantItem && RunBuildManager.Instance != null)
            {
                BuildItemData item = RunBuildManager.Instance.RollUpgradeRewardItem(choice.minRewardRarity);
                if (item != null)
                {
                    RunBuildManager.Instance.AcquireItem(item, "LevelUp");
                }
            }
        }

        private void ApplyGuaranteedLevelUpBonus()
        {
            if (levelUpMaxHpBonus <= 0f)
            {
                return;
            }

            TryBindPlayer();
            if (playerDamageable == null || playerDamageable.IsDead)
            {
                return;
            }

            playerDamageable.AddMaxHp(levelUpMaxHpBonus, refillHpOnLevelUp);
        }

        private int RequiredXp()
        {
            return Mathf.Max(50, xpBase + (_level - 1) * xpPerLevel);
        }

        private void RaiseExperience()
        {
            OnExperienceChanged?.Invoke(_level, _xp, RequiredXp());
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
