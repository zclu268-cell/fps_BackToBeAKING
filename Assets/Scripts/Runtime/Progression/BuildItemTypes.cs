namespace RoguePulse
{
    public enum BuildTag
    {
        Assault = 0,
        Mobility = 1,
        Sustain = 2,
        Utility = 3,
        Economy = 4
    }

    public enum ItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Legendary = 3
    }

    public sealed class BuildItemData
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly ItemRarity Rarity;
        public readonly BuildTag PrimaryTag;
        public readonly float DamagePercent;
        public readonly float MoveSpeedPercent;
        public readonly float MaxHpFlat;
        public readonly float GoldGainPercent;
        public readonly int ShopCost;

        public BuildItemData(
            string id,
            string name,
            string description,
            ItemRarity rarity,
            BuildTag tag,
            float damagePercent,
            float moveSpeedPercent,
            float maxHpFlat,
            float goldGainPercent,
            int shopCost)
        {
            Id = id;
            DisplayName = name;
            Description = description;
            Rarity = rarity;
            PrimaryTag = tag;
            DamagePercent = damagePercent;
            MoveSpeedPercent = moveSpeedPercent;
            MaxHpFlat = maxHpFlat;
            GoldGainPercent = goldGainPercent;
            ShopCost = shopCost;
        }
    }
}
