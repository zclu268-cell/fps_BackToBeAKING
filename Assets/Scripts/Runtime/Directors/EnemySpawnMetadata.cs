using UnityEngine;

namespace RoguePulse
{
    public class EnemySpawnMetadata : MonoBehaviour
    {
        [SerializeField] private bool isElite;
        [SerializeField] private float goldMultiplier = 1f;

        public bool IsElite => isElite;
        public float GoldMultiplier => goldMultiplier;

        public void Configure(bool elite, float goldDropMultiplier)
        {
            isElite = elite;
            goldMultiplier = Mathf.Max(0.1f, goldDropMultiplier);
        }
    }
}
