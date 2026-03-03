using UnityEngine;

namespace RoguePulse
{
    [RequireComponent(typeof(SphereCollider))]
    public class GoldPickup : MonoBehaviour
    {
        [SerializeField] private int amount = 5;
        [SerializeField] private float spinSpeed = 130f;
        [SerializeField] private float hoverAmp = 0.12f;
        [SerializeField] private float hoverFreq = 2.3f;

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

        public void SetAmount(int value)
        {
            amount = Mathf.Max(1, value);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag("Player"))
            {
                return;
            }

            int finalAmount = amount;
            if (RunBuildManager.Instance != null)
            {
                finalAmount = RunBuildManager.Instance.EvaluateGoldAmount(amount);
            }

            CurrencyManager.Instance?.AddGold(finalAmount);
            Destroy(gameObject);
        }

        public static GoldPickup SpawnRuntimePickup(int amount, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.34f;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.9f, 0.16f);
            }

            GoldPickup pickup = go.AddComponent<GoldPickup>();
            pickup.SetAmount(amount);
            return pickup;
        }
    }
}
