using UnityEngine;

namespace GameClub.Player
{
    // F uses the inherited event. The sibling GravityBody2D owns the exchange capability.
    [DisallowMultipleComponent, RequireComponent(typeof(GravityBody2D), typeof(GravityBoxPush))]
    public sealed class GravityBox : EventInteractable
    {
        [SerializeField] private Color markedColor = new Color(1f, 0.75f, 0.2f);
        private SpriteRenderer sprite;
        private Color originalColor;
        private bool marked;
        private bool initialized;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite) originalColor = sprite.color;
        }

        protected override void OnInteract()
        {
            marked = !marked;
            if (sprite) sprite.color = marked ? markedColor : originalColor;
            base.OnInteract();
        }

        [System.Serializable]
        private class State : InteractionState { public bool marked; }

        public override string CaptureState()
        {
            Initialize();
            var state = new State { marked = marked };
            CaptureInteraction(state);
            return JsonUtility.ToJson(state);
        }

        public override void RestoreState(string data)
        {
            Initialize();
            var state = JsonUtility.FromJson<State>(data);
            RestoreInteraction(state);
            marked = state.marked;
        }

        public override void RefreshState()
        {
            Initialize();
            if (sprite) sprite.color = marked ? markedColor : originalColor;
        }

        private void Reset()
        {
            GetComponent<GravityBody2D>().startsUpward = true;
            int boxLayer = LayerMask.NameToLayer("GravityBox");
            if (boxLayer >= 0) gameObject.layer = boxLayer;
        }
    }
}
