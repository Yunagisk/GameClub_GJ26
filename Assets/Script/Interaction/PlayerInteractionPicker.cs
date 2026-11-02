using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerInputReader), typeof(GravityBody2D))]
    [RequireComponent(typeof(GravitySelectionMarker))]
    public sealed class PlayerInteractionPicker : MonoBehaviour
    {
        [SerializeField] private Camera pickCamera;
        [SerializeField] private Collider2D actorCollider;
        [SerializeField] private LayerMask pickLayers = (1 << 8) | (1 << 9);
        public Collider2D Hovered { get; private set; }
        private Behaviour selected;
        private GravitySelectionMarker marker;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private PlayerInputReader input;
        private IGravityExchange player;

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            if (!actorCollider) actorCollider = GetComponent<Collider2D>();
            player = GetComponent<IGravityExchange>();
            marker = GetComponent<GravitySelectionMarker>();
            if (!pickCamera) pickCamera = GetComponent<PlayerInput>().camera;
            if (!pickCamera) pickCamera = Camera.main;
        }

        private void OnEnable()
        {
            input.InteractPressed += Interact;
            input.SelectPressed += SelectTarget;
            input.ExchangePressed += Exchange;
        }

        private void OnDisable()
        {
            input.InteractPressed -= Interact;
            input.SelectPressed -= SelectTarget;
            input.ExchangePressed -= Exchange;
            Hovered = null;
            ClearSelection();
        }

        private void Update()
        {
            if (!ReferenceEquals(selected, null) && (!selected || !selected.isActiveAndEnabled))
                ClearSelection();
            Pick();
        }

        private void Pick()
        {
            Hovered = null;
            Vector2 point = input.PointerPosition;
            if (!pickCamera || !pickCamera.pixelRect.Contains(point)) return;
            Ray ray = pickCamera.ScreenPointToRay(point);
            if (actorCollider)
            {
                var plane = new Plane(Vector3.forward, actorCollider.bounds.center);
                if (plane.Raycast(ray, out float distance) && actorCollider.OverlapPoint(ray.GetPoint(distance))) return;
            }
            Hovered = Physics2D.GetRayIntersection(ray, Mathf.Infinity, pickLayers).collider;
        }

        private bool PointerHitsUI()
        {
            var events = EventSystem.current;
            if (!events) return false;
            // Query at this click's position; the input module's cached hover can be one frame old.
            var pointer = new PointerEventData(events) { position = input.PointerPosition };
            events.RaycastAll(pointer, uiHits);
            foreach (var hit in uiHits)
                if (!(hit.module is PhysicsRaycaster)) return true;
            return false;
        }

        private void SelectTarget()
        {
            if (PointerHitsUI()) return;
            Pick();
            var target = Hovered ? Hovered.GetComponentInParent<IGravityExchange>() : null;
            if (target is Behaviour behaviour && behaviour.isActiveAndEnabled && !ReferenceEquals(target, player))
            {
                selected = behaviour;
                marker.Show(selected, Hovered);
            }
            else ClearSelection();
        }

        private void ClearSelection()
        {
            selected = null;
            if (marker) marker.Hide();
        }

        private void Interact()
        {
            var target = InteractableBase.FindNearest(actorCollider);
            if (target) target.TryInteract(actorCollider);
        }

        private void Exchange()
        {
            if (PointerHitsUI() || !selected || !selected.isActiveAndEnabled || player.isExchangeCooldown) return;
            var target = (IGravityExchange)selected;
            if (!target.isExchangeCooldown) player.ExchangeGravity(target);
        }
    }
}
