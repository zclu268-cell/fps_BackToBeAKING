using UnityEngine;

namespace RoguePulse
{
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private float interactionRadius = 2.2f;
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        private readonly Collider[] _buffer = new Collider[24];

        private void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                GameHUD.Instance?.SetInteractionPrompt(string.Empty);
                return;
            }

            InteractableBase best = FindNearest();
            if (best == null)
            {
                GameHUD.Instance?.SetInteractionPrompt(string.Empty);
                return;
            }

            GameHUD.Instance?.SetInteractionPrompt(best.Prompt);
            if (InputCompat.GetKeyDown(KeyCode.E) && best.CanInteract(gameObject))
            {
                best.Interact(gameObject);
            }
        }

        private InteractableBase FindNearest()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, interactionRadius, _buffer, mask, triggerInteraction);
            InteractableBase best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _buffer[i];
                if (col == null)
                {
                    continue;
                }

                InteractableBase interactable = col.GetComponentInParent<InteractableBase>();
                if (interactable == null)
                {
                    continue;
                }

                float sqr = (interactable.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = interactable;
                }
            }

            return best;
        }
    }
}
