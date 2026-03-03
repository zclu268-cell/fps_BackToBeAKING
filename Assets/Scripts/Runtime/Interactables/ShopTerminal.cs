using UnityEngine;

namespace RoguePulse
{
    public class ShopTerminal : InteractableBase
    {
        [SerializeField] private bool oneTimeUse;
        [SerializeField] private Renderer terminalRenderer;
        [SerializeField] private Color soldOutColor = new Color(0.4f, 0.4f, 0.4f);

        private BuildItemData _offer;
        private int _cost;
        private bool _soldOut;

        private void Start()
        {
            if (terminalRenderer == null)
            {
                terminalRenderer = GetComponentInChildren<Renderer>();
            }

            RollOffer();
            RefreshVisual();
        }

        public override string Prompt
        {
            get
            {
                if (_soldOut)
                {
                    return "Shop sold out";
                }

                if (_offer == null)
                {
                    return "Shop calibrating...";
                }

                if (CurrencyManager.Instance == null || CurrencyManager.Instance.CanAfford(_cost))
                {
                    return $"Press E: {_offer.DisplayName} ({_cost}G)";
                }

                return $"Need {_cost}G for {_offer.DisplayName}";
            }
        }

        public override bool CanInteract(GameObject interactor)
        {
            if (_soldOut || _offer == null || interactor == null || !interactor.CompareTag("Player"))
            {
                return false;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                return false;
            }

            return CurrencyManager.Instance != null && CurrencyManager.Instance.CanAfford(_cost);
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            if (!CurrencyManager.Instance.SpendGold(_cost))
            {
                return;
            }

            RunBuildManager.Instance?.AcquireItem(_offer, "Shop");
            GameHUD.Instance?.ShowRuntimeMessage($"Bought {_offer.DisplayName}");

            if (oneTimeUse)
            {
                _soldOut = true;
            }
            else
            {
                RollOffer();
            }

            RefreshVisual();
        }

        private void RollOffer()
        {
            _offer = RunBuildManager.Instance != null ? RunBuildManager.Instance.RollShopOfferItem() : null;
            if (_offer == null)
            {
                _cost = 0;
                return;
            }

            _cost = _offer.ShopCost > 0 ? _offer.ShopCost : BuildItemCatalog.CostForRarity(_offer.Rarity);
        }

        private void RefreshVisual()
        {
            if (terminalRenderer == null)
            {
                return;
            }

            if (_soldOut)
            {
                terminalRenderer.material.color = soldOutColor;
            }
            else if (_offer != null)
            {
                terminalRenderer.material.color = BuildItemCatalog.ColorForRarity(_offer.Rarity);
            }
        }
    }
}
