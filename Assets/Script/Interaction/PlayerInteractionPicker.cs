using UnityEngine;
using UnityEngine.InputSystem;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerInputReader), typeof(GravityBody2D))]
    public sealed class PlayerInteractionPicker : MonoBehaviour
    {
        [SerializeField] private Camera pickCamera;
        [SerializeField] private Collider2D actorCollider;
        [SerializeField] private LayerMask pickLayers = (1 << 8) | (1 << 9);
        public Collider2D Hovered { get; private set; }

        private PlayerInputReader input;
        private IGravityExchange player;

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            if (!actorCollider) actorCollider = GetComponent<Collider2D>();
            player = GetComponent<IGravityExchange>();
            if (!pickCamera) pickCamera = GetComponent<PlayerInput>().camera;
            if (!pickCamera) pickCamera = Camera.main;
        }

        private void OnEnable()
        {
            input.InteractPressed += Interact;
            input.ExchangePressed += Exchange;
        }

        private void OnDisable()
        {
            input.InteractPressed -= Interact;
            input.ExchangePressed -= Exchange;
            Hovered = null;
        }

        private void Update() => Pick();

        private void Pick()
        {
            Hovered = null;
            Vector2 point = input.PointerPosition;
            if (!pickCamera || !pickCamera.pixelRect.Contains(point)) return;
            Ray ray = pickCamera.ScreenPointToRay(point);
            Hovered = Physics2D.GetRayIntersection(ray, Mathf.Infinity, pickLayers).collider;
        }

        private void Interact()
        {
            var target = InteractableBase.FindNearest(actorCollider);
            if (target) target.TryInteract(actorCollider);
        }

        private void Exchange()
        {
            Pick();
            if (!Hovered || player.isExchangeCooldown) return;
            var target = Hovered.GetComponentInParent<IGravityExchange>();
            if (target is Behaviour behaviour && behaviour.isActiveAndEnabled
                && !ReferenceEquals(target, player) && !target.isExchangeCooldown)
                player.ExchangeGravity(target);
        }
    }
}
