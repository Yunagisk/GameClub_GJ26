using System.Collections.Generic;
using UnityEngine;

namespace GameClub.Player
{
    public abstract class InteractableBase : MonoBehaviour, ICheckpointState
    {
        private static readonly List<InteractableBase> activeTargets = new List<InteractableBase>();
        public bool interactableLocked = false;
        [SerializeField, Min(0f)] private float interactionRadius = 2f;
        [SerializeField] private Vector2 interactionCenterOffset = Vector2.zero;
        [SerializeField, Min(0f)] private float interactionCooldown = 0.1f;

        private float nextInteractionTime;
        private Vector3 InteractionCenter => transform.TransformPoint(interactionCenterOffset);

        protected virtual void OnEnable() => activeTargets.Add(this);
        protected virtual void OnDisable() => activeTargets.Remove(this);

        public virtual bool CanInteract() =>
            isActiveAndEnabled && !interactableLocked && Time.time >= nextInteractionTime;

        private bool InRange(Collider2D actor, out float distanceSquared)
        {
            Vector2 center = InteractionCenter;
            distanceSquared = (actor.ClosestPoint(center) - center).sqrMagnitude;
            return distanceSquared <= interactionRadius * interactionRadius;
        }

        internal static InteractableBase FindNearest(Collider2D actor)
        {
            if (!actor || !actor.isActiveAndEnabled) return null;
            InteractableBase nearest = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var target in activeTargets)
            {
                if (target.CanInteract() && target.InRange(actor, out float distance) && distance < bestDistance)
                {
                    nearest = target;
                    bestDistance = distance;
                }
            }
            return nearest;
        }

        public bool TryInteract(Collider2D actor)
        {
            if (!actor || !actor.isActiveAndEnabled || !CanInteract() || !InRange(actor, out _)) return false;
            nextInteractionTime = Time.time + interactionCooldown;
            OnInteract();
            return true;
        }

        protected abstract void OnInteract();

        [System.Serializable]
        protected class InteractionState
        {
            public bool locked;
            public float remaining;
        }

        protected void CaptureInteraction(InteractionState state)
        {
            state.locked = interactableLocked;
            state.remaining = Mathf.Max(0f, nextInteractionTime - Time.time);
        }

        protected void RestoreInteraction(InteractionState state)
        {
            interactableLocked = state.locked;
            nextInteractionTime = Time.time + state.remaining;
        }

        public virtual string CaptureState()
        {
            var state = new InteractionState();
            CaptureInteraction(state);
            return JsonUtility.ToJson(state);
        }

        public virtual void RestoreState(string data) => RestoreInteraction(JsonUtility.FromJson<InteractionState>(data));
        public virtual void RefreshState() { }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.DrawWireDisc(InteractionCenter, Vector3.forward, interactionRadius);
        }
#endif
    }
}
