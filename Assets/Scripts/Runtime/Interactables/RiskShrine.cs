using UnityEngine;

namespace RoguePulse
{
    public class RiskShrine : InteractableBase
    {
        [SerializeField, Range(0.05f, 0.8f)] private float selfDamagePercent = 0.35f;
        [SerializeField] private int bonusGold = 45;
        [SerializeField] private Renderer shrineRenderer;
        [SerializeField] private Color idleColor = new Color(0.92f, 0.43f, 0.24f);
        [SerializeField] private Color usedColor = new Color(0.35f, 0.35f, 0.35f);

        private bool _used;

        private void Start()
        {
            if (shrineRenderer == null)
            {
                shrineRenderer = GetComponentInChildren<Renderer>();
            }

            UpdateVisual();
        }

        public override string Prompt
        {
            get
            {
                if (_used)
                {
                    return "Shrine exhausted";
                }

                return $"Press E: lose {Mathf.RoundToInt(selfDamagePercent * 100f)}% HP for rare reward";
            }
        }

        public override bool CanInteract(GameObject interactor)
        {
            if (_used || interactor == null || !interactor.CompareTag("Player"))
            {
                return false;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                return false;
            }

            Damageable damageable = interactor.GetComponent<Damageable>();
            return damageable != null && !damageable.IsDead;
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            Damageable damageable = interactor.GetComponent<Damageable>();
            if (damageable == null)
            {
                return;
            }

            float damage = Mathf.Max(1f, damageable.MaxHp * selfDamagePercent);
            bool tookDamage = damageable.TakeDamage(damage);
            if (!tookDamage || damageable.IsDead)
            {
                return;
            }

            CurrencyManager.Instance?.AddGold(bonusGold);
            BuildItemData reward = RunBuildManager.Instance != null ? RunBuildManager.Instance.RollRiskRewardItem() : null;
            if (reward != null)
            {
                RunBuildManager.Instance.AcquireItem(reward, "RiskShrine");
                GameHUD.Instance?.ShowRuntimeMessage($"Risk reward: {reward.DisplayName}");
            }

            _used = true;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (shrineRenderer == null)
            {
                return;
            }

            shrineRenderer.material.color = _used ? usedColor : idleColor;
        }
    }
}
