using UnityEngine;

namespace RoguePulse
{
    [RequireComponent(typeof(SphereCollider))]
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField] private float hoverAmp = 0.15f;
        [SerializeField] private float hoverFreq = 2.5f;

        private BuildItemData _item;
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

        public void Configure(BuildItemData item)
        {
            _item = item;
            gameObject.name = item != null ? $"ItemPickup_{item.Id}" : "ItemPickup";

            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null && item != null)
            {
                renderer.material.color = BuildItemCatalog.ColorForRarity(item.Rarity);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag("Player") || _item == null)
            {
                return;
            }

            if (RunBuildManager.Instance != null)
            {
                RunBuildManager.Instance.AcquireItem(_item, "Drop");
            }

            Destroy(gameObject);
        }

        public static ItemPickup SpawnRuntimePickup(BuildItemData item, Vector3 position)
        {
            if (item == null)
            {
                return null;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.5f;
            ItemPickup pickup = go.AddComponent<ItemPickup>();
            pickup.Configure(item);
            return pickup;
        }
    }
}
