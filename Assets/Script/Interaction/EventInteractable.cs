using UnityEngine;
using UnityEngine.Events;

namespace GameClub.Player
{
    public class EventInteractable : InteractableBase
    {
        [SerializeField] private UnityEvent onInteract = new UnityEvent();

        protected override void OnInteract() => onInteract.Invoke();
    }
}
