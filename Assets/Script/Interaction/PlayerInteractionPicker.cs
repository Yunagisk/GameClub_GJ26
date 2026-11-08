using System;
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
        [Tooltip("Require the selected object to remain inside the gameplay camera before exchanging gravity.")]
        public bool requireVisibleTargetForExchange = true;
        public Collider2D Hovered { get; private set; }
        public Behaviour SelectedTarget => selected;
        public event Action Interacted;
        private Behaviour selected;
        private Collider2D selectedCollider;
        private GravitySelectionMarker marker;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private readonly Plane[] frustumPlanes = new Plane[6];
        private PlayerInputReader input;
        private IGravityExchange player;
        private PlayerLifecycle lifecycle;

        private bool Blocked
        {
            get
            {
                if (!isActiveAndEnabled || (lifecycle && !lifecycle.CanControl)) return true;
                var session = GameClub.Puzzles.PuzzleSession.Instance;
                return session && session.InputBlocked;
            }
        }

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            if (!actorCollider) actorCollider = GetComponent<Collider2D>();
            player = GetComponent<IGravityExchange>();
            lifecycle = GetComponent<PlayerLifecycle>();
            marker = GetComponent<GravitySelectionMarker>();
            var playerInput = GetComponent<PlayerInput>();
            if (!pickCamera && playerInput) pickCamera = playerInput.camera;
            if (!pickCamera) pickCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (!input) input = GetComponent<PlayerInputReader>();
            if (!input) return;
            input.InteractPressed += Interact;
            input.SelectPressed += SelectTarget;
            input.ExchangePressed += Exchange;
        }

        private void OnDisable()
        {
            if (input)
            {
                input.InteractPressed -= Interact;
                input.SelectPressed -= SelectTarget;
                input.ExchangePressed -= Exchange;
            }
            Hovered = null;
            ClearSelection();
        }

        private void Update()
        {
            if (Blocked)
            {
                Hovered = null;
                ClearSelection();
                return;
            }
            if (!ReferenceEquals(selected, null) && (!selected || !selected.isActiveAndEnabled
                || (requireVisibleTargetForExchange && !SelectedIsVisible()))) ClearSelection();
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
            var pointer = new PointerEventData(events) { position = input.PointerPosition };
            events.RaycastAll(pointer, uiHits);
            foreach (var hit in uiHits)
                if (!(hit.module is PhysicsRaycaster)) return true;
            return false;
        }

        private void SelectTarget()
        {
            if (Blocked || PointerHitsUI()) return;
            Pick();
            var target = Hovered ? Hovered.GetComponentInParent<IGravityExchange>() : null;
            if (target is Behaviour behaviour && behaviour.isActiveAndEnabled && !ReferenceEquals(target, player))
            {
                selected = behaviour;
                selectedCollider = Hovered;
                marker.Show(selected, Hovered);
            }
            else ClearSelection();
        }

        public void ClearSelection()
        {
            selected = null;
            selectedCollider = null;
            if (marker) marker.Hide();
        }

        private bool SelectedIsVisible()
        {
            if (!pickCamera || !selectedCollider || !selectedCollider.isActiveAndEnabled) return false;
            GeometryUtility.CalculateFrustumPlanes(pickCamera, frustumPlanes);
            return GeometryUtility.TestPlanesAABB(frustumPlanes, selectedCollider.bounds);
        }

        private void Interact()
        {
            if (Blocked) return;
            var target = InteractableBase.FindNearest(actorCollider);
            if (target && target.TryInteract(actorCollider)) Interacted?.Invoke();
        }

        private void Exchange()
        {
            if (Blocked || player == null || PointerHitsUI() || !selected || !selected.isActiveAndEnabled || player.isExchangeCooldown) return;
            if (requireVisibleTargetForExchange && !SelectedIsVisible()) { ClearSelection(); return; }
            var target = (IGravityExchange)selected;
            if (!target.isExchangeCooldown) player.ExchangeGravity(target);
        }
    }
}
