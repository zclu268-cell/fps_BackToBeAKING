using UnityEngine;

namespace RoguePulse
{
    [RequireComponent(typeof(SphereCollider))]
    public class HealPickup : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float healAmount = 15f;
        [SerializeField] private float spinSpeed = 110f;
        [SerializeField] private float hoverAmp = 0.09f;
        [SerializeField] private float hoverFreq = 2.2f;

        private Vector3 _origin;

        private void Awake()
        {
            SphereCollider trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            _origin = transform.position;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            float hover = Mathf.Sin(Time.time * hoverFreq) * hoverAmp;
            transform.position = new Vector3(_origin.x, _origin.y + hover, _origin.z);
        }

        public void SetHealAmount(float value)
        {
            healAmount = Mathf.Max(1f, value);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag("Player"))
            {
                return;
            }

            Damageable damageable = other.GetComponentInParent<Damageable>();
            if (damageable == null || damageable.IsDead)
            {
                return;
            }

            if (damageable.CurrentHp >= damageable.MaxHp - 0.01f)
            {
                return;
            }

            damageable.Heal(healAmount);
            Destroy(gameObject);
        }

        public static HealPickup SpawnRuntimePickup(float amount, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "HealPickup";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.25f;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.34f, 1f, 0.46f);
            }

            HealPickup pickup = go.AddComponent<HealPickup>();
            pickup.SetHealAmount(amount);
            return pickup;
        }
    }
}
