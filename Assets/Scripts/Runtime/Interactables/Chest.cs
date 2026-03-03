using UnityEngine;

namespace RoguePulse
{
    public class Chest : InteractableBase
    {
        [SerializeField] private int cost = 35;
        [SerializeField] private Renderer chestRenderer;
        [SerializeField] private Color openedColor = new Color(0.35f, 1f, 0.35f);

        private bool _opened;

        private void Awake()
        {
            if (chestRenderer == null)
            {
                chestRenderer = GetComponentInChildren<Renderer>();
            }
        }

        public override string Prompt
        {
            get
            {
                if (_opened)
                {
                    return "Chest opened";
                }

                if (CurrencyManager.Instance == null || CurrencyManager.Instance.CanAfford(cost))
                {
                    return $"Press E: random item ({cost}G)";
                }

                return $"Need {cost}G";
            }
        }

        public override bool CanInteract(GameObject interactor)
        {
            if (_opened || interactor == null || !interactor.CompareTag("Player"))
            {
                return false;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                return false;
            }

            return CurrencyManager.Instance != null && CurrencyManager.Instance.CanAfford(cost);
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            if (!CurrencyManager.Instance.SpendGold(cost))
            {
                return;
            }

            BuildItemData item = RunBuildManager.Instance != null ? RunBuildManager.Instance.RollChestRewardItem() : null;
            if (item != null)
            {
                RunBuildManager.Instance.AcquireItem(item, "Chest");
                GameHUD.Instance?.ShowRuntimeMessage($"Chest: {item.DisplayName}");
            }

            _opened = true;
            if (chestRenderer != null)
            {
                chestRenderer.material.color = openedColor;
            }
        }
    }
}
