using UnityEngine;

namespace RoguePulse
{
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        public abstract string Prompt { get; }
        public abstract bool CanInteract(GameObject interactor);
        public abstract void Interact(GameObject interactor);
    }
}
