using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class GravityBody2D : MonoBehaviour, IGravityExchange, ICheckpointState
    {
        [SerializeField,Min(0.01f)] private float gravityScale = 3.5f;
        [SerializeField, Min(0f)] private float gravityExchangeCD = 0.3f;
        public bool startsUpward;

        public Vector2 gravityDir { get; private set; }
        public bool isGravityFlipped => gravityDir.y > 0f;

        private float cooldownUntil;
        public bool isExchangeCooldown => Time.time < cooldownUntil;
        private Rigidbody2D body;
        private float multiplier = 1f;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (body) return;
            body = GetComponent<Rigidbody2D>();

            gravityDir = startsUpward ? Vector2.up : Vector2.down;
            SetMultiplier(1f);
        }

        public void SetMultiplier(float value)
        {
            multiplier = value;
            body.gravityScale = (isGravityFlipped ? -gravityScale : gravityScale) * multiplier;
        }

        public void ExchangeGravity(IGravityExchange target)
        {
            if (!isActiveAndEnabled || target == null || ReferenceEquals(target, this)
                || isExchangeCooldown || target.isExchangeCooldown) return;

            Vector2 previous = gravityDir;
            Vector2 incoming = target.gravityDir;
            ReceiveGravity(incoming);
            target.ReceiveGravity(previous);
        }

        public void ReceiveGravity(Vector2 direction)
        {
            cooldownUntil = Time.time + gravityExchangeCD;
            if (gravityDir == direction) return;
            gravityDir = direction;
            SetMultiplier(multiplier);
            body.WakeUp();
        }

        [System.Serializable]
        private class State
        {
            public Vector2 direction;
            public float scale, multiplier, remaining;
        }

        public string CaptureState()
        {
            Initialize();
            return JsonUtility.ToJson(new State
            {
                direction = gravityDir, scale = gravityScale, multiplier = multiplier,
                remaining = Mathf.Max(0f, cooldownUntil - Time.time)
            });
        }

        public void RestoreState(string data)
        {
            Initialize();
            var state = JsonUtility.FromJson<State>(data);
            gravityDir = state.direction;
            gravityScale = state.scale;
            cooldownUntil = Time.time + state.remaining;
            SetMultiplier(state.multiplier);
        }

        public void RefreshState() { }

        public void ResetForRespawn()
        {
            Initialize();
            gravityDir = Vector2.down;
            cooldownUntil = 0f;
            SetMultiplier(1f);
        }
    }
}
