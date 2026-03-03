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
                renderer.material.color = new Color(0.35f, 0.88f, 1f);
            }

            ExperiencePickup pickup = go.AddComponent<ExperiencePickup>();
            pickup.SetAmount(amount);
            return pickup;
        }
    }
}
