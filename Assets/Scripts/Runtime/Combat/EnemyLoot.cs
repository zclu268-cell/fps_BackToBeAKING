using System;
using UnityEngine;

namespace RoguePulse
{
    [RequireComponent(typeof(Damageable))]
    public class EnemyLoot : MonoBehaviour
    {
        [SerializeField] private int minGold = 3;
        [SerializeField] private int maxGold = 6;
        [SerializeField] private int minXp = 8;
        [SerializeField] private int maxXp = 12;
        [SerializeField] private int eliteXpBonus = 10;
        [Header("Ground Drop")]
        [SerializeField] private bool snapDropsToGround = true;
        [SerializeField] private LayerMask dropGroundMask = ~0;
        [SerializeField, Min(0.1f)] private float dropGroundProbeUp = 10f;
        [SerializeField, Min(1f)] private float dropGroundProbeDown = 120f;
        [SerializeField, Min(0f)] private float dropGroundOffset = 0.06f;
        [Header("XP Pickup")]
        [SerializeField, Range(1, 6)] private int minXpPickups = 1;
        [SerializeField, Range(1, 6)] private int maxXpPickups = 2;
        [Header("Item Drop")]
        [SerializeField, Range(0f, 1f)] private float normalItemDropChance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float eliteItemDropChance = 0.45f;
        [Header("Heal Pickup")]
        [SerializeField, Range(0f, 1f)] private float normalHealDropChance = 0.16f;
        [SerializeField, Range(0f, 1f)] private float eliteHealDropChance = 0.50f;
        [SerializeField, Min(1f)] private float minHealAmount = 12f;
        [SerializeField, Min(1f)] private float maxHealAmount = 20f;
        [SerializeField, Min(0f)] private float eliteHealBonus = 8f;

        private Damageable _damageable;
        private readonly RaycastHit[] _groundProbeHits = new RaycastHit[12];

        public static event Action<EnemySpawnMetadata> OnEnemyKilled;

        private void Awake()
        {
            _damageable = GetComponent<Damageable>();
        }

        private void OnEnable()
        {
            if (_damageable != null)
            {
                _damageable.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_damageable != null)
            {
                _damageable.OnDeath -= HandleDeath;
            }
        }

        private void HandleDeath(Damageable _)
        {
            EnemySpawnMetadata meta = GetComponent<EnemySpawnMetadata>();
            bool elite = meta != null && meta.IsElite;

            int gold = UnityEngine.Random.Range(minGold, maxGold + 1);
            if (meta != null)
            {
                gold = Mathf.Max(1, Mathf.CeilToInt(gold * meta.GoldMultiplier));
            }

            GoldPickup.SpawnRuntimePickup(gold, GetDropPosition(0.45f, 0.32f));
            DropXpPickups(elite);
            TryDropHealPickup(elite);
            TryDropItem(elite);
            OnEnemyKilled?.Invoke(meta);
        }

        private void DropXpPickups(bool elite)
        {
            int xp = UnityEngine.Random.Range(minXp, maxXp + 1);
            if (elite)
            {
                xp += eliteXpBonus;
            }

            if (xp <= 0)
            {
                return;
            }

            int low = Mathf.Min(minXpPickups, maxXpPickups);
            int high = Mathf.Max(minXpPickups, maxXpPickups);
            int pickupCount = UnityEngine.Random.Range(low, high + 1);
            pickupCount = Mathf.Clamp(pickupCount, 1, xp);

            int remaining = xp;
            for (int i = 0; i < pickupCount; i++)
            {
                int amount;
                int remainingSlots = pickupCount - i;
                if (remainingSlots <= 1)
                {
                    amount = remaining;
                }
                else
                {
                    int maxForThis = remaining - (remainingSlots - 1);
                    amount = UnityEngine.Random.Range(1, maxForThis + 1);
                }

                remaining -= amount;
                ExperiencePickup.SpawnRuntimePickup(amount, GetDropPosition(0.62f, 0.42f));
            }
        }

        private void TryDropHealPickup(bool elite)
        {
            float chance = elite ? eliteHealDropChance : normalHealDropChance;
            if (UnityEngine.Random.value > chance)
            {
                return;
            }

            float minAmount = Mathf.Max(1f, minHealAmount);
            float maxAmount = Mathf.Max(minAmount, maxHealAmount);
            float amount = UnityEngine.Random.Range(minAmount, maxAmount);
            if (elite)
            {
                amount += eliteHealBonus;
            }

            HealPickup.SpawnRuntimePickup(amount, GetDropPosition(0.68f, 0.36f));
        }

        private Vector3 GetDropPosition(float upOffset, float horizontalRadius)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * horizontalRadius;
            offset.y = 0f;
            Vector3 rawPos = transform.position + Vector3.up * upOffset + offset;
            return SnapDropToGround(rawPos);
        }

        private void TryDropItem(bool elite)
        {
            if (RunBuildManager.Instance == null)
            {
                return;
            }

            float chance = elite ? eliteItemDropChance : normalItemDropChance;
            if (UnityEngine.Random.value > chance)
            {
                return;
            }

            BuildItemData item = RunBuildManager.Instance.RollDropRewardItem(elite);
            if (item == null)
            {
                return;
            }

            Vector3 pos = GetDropPosition(0.8f, 0.25f);
            ItemPickup.SpawnRuntimePickup(item, pos);
        }

        private Vector3 SnapDropToGround(Vector3 rawPos)
        {
            if (!snapDropsToGround)
            {
                return rawPos;
            }

            Vector3 rayOrigin = new Vector3(rawPos.x, rawPos.y + dropGroundProbeUp, rawPos.z);
            float rayDistance = dropGroundProbeUp + dropGroundProbeDown;

            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                _groundProbeHits,
                rayDistance,
                dropGroundMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                return rawPos;
            }

            float nearest = float.MaxValue;
            Vector3 snapped = rawPos;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundProbeHits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    snapped = hit.point + Vector3.up * dropGroundOffset;
                }
            }

            return nearest < float.MaxValue ? snapped : rawPos;
        }
    }
}
