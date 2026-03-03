using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace RoguePulse
{
    public static class BuildItemCatalog
    {
        private static readonly BuildItemData[] Items =
        {
            new BuildItemData("assault_blade", "Assault Blade", "Damage +10%", ItemRarity.Common, BuildTag.Assault, 0.10f, 0f, 0f, 0f, 40),
            new BuildItemData("quick_boots", "Quick Boots", "Move +10%", ItemRarity.Common, BuildTag.Mobility, 0f, 0.10f, 0f, 0f, 40),
            new BuildItemData("iron_plate", "Iron Plate", "Max HP +18", ItemRarity.Common, BuildTag.Sustain, 0f, 0f, 18f, 0f, 45),
            new BuildItemData("small_wallet", "Small Wallet", "Gold gain +8%", ItemRarity.Common, BuildTag.Economy, 0f, 0f, 0f, 0.08f, 36),
            new BuildItemData("stabilizer", "Stabilizer", "Damage +8%, Move +4%", ItemRarity.Common, BuildTag.Utility, 0.08f, 0.04f, 0f, 0f, 42),

            new BuildItemData("berserker_core", "Berserker Core", "Damage +16%", ItemRarity.Uncommon, BuildTag.Assault, 0.16f, 0f, 0f, 0f, 85),
            new BuildItemData("jet_thrusters", "Jet Thrusters", "Move +16%", ItemRarity.Uncommon, BuildTag.Mobility, 0f, 0.16f, 0f, 0f, 85),
            new BuildItemData("adaptive_shell", "Adaptive Shell", "Max HP +34", ItemRarity.Uncommon, BuildTag.Sustain, 0f, 0f, 34f, 0f, 90),
            new BuildItemData("contract_token", "Contract Token", "Gold gain +15%", ItemRarity.Uncommon, BuildTag.Economy, 0f, 0f, 0f, 0.15f, 80),
            new BuildItemData("tactic_module", "Tactic Module", "Damage +10%, Move +10%", ItemRarity.Uncommon, BuildTag.Utility, 0.10f, 0.10f, 0f, 0f, 92),

            new BuildItemData("plasma_reactor", "Plasma Reactor", "Damage +26%", ItemRarity.Rare, BuildTag.Assault, 0.26f, 0f, 0f, 0f, 165),
            new BuildItemData("phase_shoes", "Phase Shoes", "Move +26%", ItemRarity.Rare, BuildTag.Mobility, 0f, 0.26f, 0f, 0f, 160),
            new BuildItemData("guardian_shell", "Guardian Shell", "Max HP +62", ItemRarity.Rare, BuildTag.Sustain, 0f, 0f, 62f, 0f, 175),
            new BuildItemData("treasure_relay", "Treasure Relay", "Gold gain +28%", ItemRarity.Rare, BuildTag.Economy, 0f, 0f, 0f, 0.28f, 155),
            new BuildItemData("war_harness", "War Harness", "Damage +18%, Move +14%", ItemRarity.Rare, BuildTag.Utility, 0.18f, 0.14f, 0f, 0f, 170),

            new BuildItemData("sun_breaker", "Sun Breaker", "Damage +40%", ItemRarity.Legendary, BuildTag.Assault, 0.40f, 0f, 0f, 0f, 320),
            new BuildItemData("void_stride", "Void Stride", "Move +38%", ItemRarity.Legendary, BuildTag.Mobility, 0f, 0.38f, 0f, 0f, 300),
            new BuildItemData("everheart", "Everheart", "Max HP +110", ItemRarity.Legendary, BuildTag.Sustain, 0f, 0f, 110f, 0f, 340),
            new BuildItemData("golden_protocol", "Golden Protocol", "Gold gain +45%", ItemRarity.Legendary, BuildTag.Economy, 0f, 0f, 0f, 0.45f, 300),
            new BuildItemData("apex_suite", "Apex Suite", "Damage +24%, Move +20%, HP +36", ItemRarity.Legendary, BuildTag.Utility, 0.24f, 0.20f, 36f, 0f, 360)
        };

        public static BuildItemData RollDrop(Random rng, bool elite, BuildTag preferredTag)
        {
            ItemRarity rarity = elite ? RollEliteRarity(rng) : RollNormalRarity(rng);
            return PickByRarityAndTag(rng, rarity, preferredTag);
        }

        public static BuildItemData RollChest(Random rng, BuildTag preferredTag)
        {
            int roll = rng.Next(0, 100);
            ItemRarity rarity = roll < 55 ? ItemRarity.Uncommon : (roll < 90 ? ItemRarity.Rare : ItemRarity.Legendary);
            return PickByRarityAndTag(rng, rarity, preferredTag);
        }

        public static BuildItemData RollShop(Random rng, int stageDisplay, BuildTag preferredTag)
        {
            int roll = rng.Next(0, 100);
            ItemRarity rarity;

            if (stageDisplay <= 1)
            {
                rarity = roll < 70 ? ItemRarity.Common : ItemRarity.Uncommon;
            }
            else if (stageDisplay == 2)
            {
                rarity = roll < 45 ? ItemRarity.Common : (roll < 88 ? ItemRarity.Uncommon : ItemRarity.Rare);
            }
            else
            {
                rarity = roll < 20 ? ItemRarity.Uncommon : (roll < 75 ? ItemRarity.Rare : ItemRarity.Legendary);
            }

            return PickByRarityAndTag(rng, rarity, preferredTag);
        }

        public static BuildItemData RollRisk(Random rng, BuildTag preferredTag)
        {
            int roll = rng.Next(0, 100);
            ItemRarity rarity = roll < 78 ? ItemRarity.Rare : ItemRarity.Legendary;
            return PickByRarityAndTag(rng, rarity, preferredTag);
        }

        public static BuildItemData RollUpgrade(Random rng, ItemRarity minRarity, BuildTag preferredTag)
        {
            int roll = rng.Next(0, 100);
            ItemRarity rarity = minRarity;
            if (minRarity == ItemRarity.Uncommon)
            {
                rarity = roll < 72 ? ItemRarity.Uncommon : (roll < 95 ? ItemRarity.Rare : ItemRarity.Legendary);
            }
            else if (minRarity == ItemRarity.Rare)
            {
                rarity = roll < 80 ? ItemRarity.Rare : ItemRarity.Legendary;
            }

            return PickByRarityAndTag(rng, rarity, preferredTag);
        }

        public static int CostForRarity(ItemRarity rarity)
        {
            if (rarity == ItemRarity.Common) return 40;
            if (rarity == ItemRarity.Uncommon) return 85;
            if (rarity == ItemRarity.Rare) return 165;
            return 320;
        }

        public static Color ColorForRarity(ItemRarity rarity)
        {
            if (rarity == ItemRarity.Common) return new Color(0.84f, 0.84f, 0.84f);
            if (rarity == ItemRarity.Uncommon) return new Color(0.42f, 0.85f, 0.42f);
            if (rarity == ItemRarity.Rare) return new Color(0.35f, 0.62f, 0.96f);
            return new Color(0.95f, 0.75f, 0.26f);
        }

        private static ItemRarity RollNormalRarity(Random rng)
        {
            int roll = rng.Next(0, 100);
            if (roll < 70) return ItemRarity.Common;
            if (roll < 95) return ItemRarity.Uncommon;
            return ItemRarity.Rare;
        }

        private static ItemRarity RollEliteRarity(Random rng)
        {
            int roll = rng.Next(0, 100);
            if (roll < 35) return ItemRarity.Common;
            if (roll < 80) return ItemRarity.Uncommon;
            if (roll < 97) return ItemRarity.Rare;
            return ItemRarity.Legendary;
        }

        private static BuildItemData PickByRarityAndTag(Random rng, ItemRarity rarity, BuildTag preferredTag)
        {
            List<BuildItemData> pool = new List<BuildItemData>();
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].Rarity == rarity)
                {
                    pool.Add(Items[i]);
                }
            }

            if (pool.Count == 0)
            {
                pool.AddRange(Items);
            }

            float total = 0f;
            float[] weights = new float[pool.Count];
            for (int i = 0; i < pool.Count; i++)
            {
                float weight = pool[i].PrimaryTag == preferredTag ? 2.35f : 1f;
                weights[i] = weight;
                total += weight;
            }

            float pick = (float)rng.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++)
            {
                pick -= weights[i];
                if (pick <= 0f)
                {
                    return pool[i];
                }
            }

            return pool[pool.Count - 1];
        }
    }
}
