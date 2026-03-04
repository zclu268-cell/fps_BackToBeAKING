using UnityEngine;

namespace RoguePulse
{
    [RequireComponent(typeof(SphereCollider))]
    public class ExperiencePickup : MonoBehaviour
    {
        [SerializeField, Min(1)] private int amount = 8;
        [SerializeField] private float spinSpeed = 135f;
        [SerializeField] private float hoverAmp = 0.10f;
        [SerializeField] private float hoverFreq = 2.6f;
        [SerializeField, Min(0.1f)] private float autoPickupRadius = 1.8f;
        [SerializeField, Min(0f)] private float playerHeightTolerance = 2.5f;

        private const float PlayerLookupInterval = 0.45f;

        private Vector3 _origin;
        private Transform _playerTransform;
        private float _nextPlayerLookupAt;
        private bool _collected;

        private void Awake()
        {
            SphereCollider trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(trigger.radius, autoPickupRadius);
            _origin = transform.position;
            CachePlayerTransform(force: true);
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            float hover = Mathf.Sin(Time.time * hoverFreq) * hoverAmp;
            transform.position = new Vector3(_origin.x, _origin.y + hover, _origin.z);
            TryAutoPickupByDistance();
        }

        public void SetAmount(int value)
        {
            amount = Mathf.Max(1, value);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerCollider(other))
            {
                return;
            }

            Collect();
        }

        private void TryAutoPickupByDistance()
        {
            CachePlayerTransform(force: false);
            if (_playerTransform == null || _collected)
            {
                return;
            }

            Vector3 delta = _playerTransform.position - transform.position;
            float horizontalSqrDistance = delta.x * delta.x + delta.z * delta.z;
            if (horizontalSqrDistance > autoPickupRadius * autoPickupRadius)
            {
                return;
            }

            if (Mathf.Abs(delta.y) > playerHeightTolerance)
            {
                return;
            }

            Collect();
        }

        private void CachePlayerTransform(bool force)
        {
            if (!force && Time.time < _nextPlayerLookupAt)
            {
                return;
            }

            _nextPlayerLookupAt = Time.time + PlayerLookupInterval;
            if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy)
            {
                return;
            }

            GameObject player = GameObject.FindWithTag("Player");
            _playerTransform = player != null ? player.transform : null;
        }

        private static bool IsPlayerCollider(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            if (other.CompareTag("Player"))
            {
                return true;
            }

            Transform root = other.transform.root;
            return root != null && root.CompareTag("Player");
        }

        private void Collect()
        {
            if (_collected)
            {
                return;
            }

            _collected = true;
            ExperienceManager.Instance?.AddExperience(amount);
            Destroy(gameObject);
        }

        public static ExperiencePickup SpawnRuntimePickup(int amount, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ExperiencePickup";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.24f;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color xpBlue = new Color(0.25f, 0.78f, 1f, 1f);
                Material material = renderer.material;
                if (material != null)
                {
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", xpBlue);
                    }

                    if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", xpBlue);
                    }
                }
            }

            ExperiencePickup pickup = go.AddComponent<ExperiencePickup>();
            pickup.SetAmount(amount);
            return pickup;
        }
    }
}
