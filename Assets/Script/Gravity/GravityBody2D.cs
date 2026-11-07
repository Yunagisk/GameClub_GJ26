using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class GravityBody2D : MonoBehaviour, IGravityExchange, ICheckpointState
    {
        [Min(0.01f)] public float gravityScale = 3.5f;
        [Min(0f)] public float gravityExchangeCD = 0.25f;
        public bool startsUpward;
        public Vector2 gravityDir { get; private set; }
        public bool isGravityFlipped => gravityDir.y > 0f;
        public bool isExchangeCooldown => ExchangeLocked || Time.time < cooldownUntil;
        public bool ExchangeLocked { get; private set; }
        public float EnvironmentStrength { get; private set; } = 1f;
        private Rigidbody2D body;
        private float multiplier = 1f;
        private float cooldownUntil;

        private void Awake() { Initialize(); ApplyGravity(); }

        private void Initialize()
        {
            if (body) return;
            body = GetComponent<Rigidbody2D>();
            if (gravityDir == Vector2.zero) gravityDir = startsUpward ? Vector2.up : Vector2.down;
        }

        // Jump and environment multipliers compose rather than overwriting one another.
        public void SetMultiplier(float value)
        {
            multiplier = Mathf.Max(0f, value);
            ApplyGravity();
        }

        public void SetEnvironment(float strength, bool lockExchange, Vector2 forcedDirection)
        {
            Initialize();
            EnvironmentStrength = Mathf.Max(0.01f, strength);
            ExchangeLocked = lockExchange;
            if (lockExchange && Mathf.Abs(forcedDirection.y) > 0.01f) SetDirection(forcedDirection);
            ApplyGravity();
        }

        public void ResetGravity(bool upward, float strength = 1f)
        {
            Initialize();
            ExchangeLocked = false;
            EnvironmentStrength = Mathf.Max(0.01f, strength);
            multiplier = 1f;
            cooldownUntil = 0f;
            SetDirection(upward ? Vector2.up : Vector2.down);
            ApplyGravity();
        }

        public void ExchangeGravity(IGravityExchange target)
        {
            if (!isActiveAndEnabled || target == null || ReferenceEquals(target, this)
                || isExchangeCooldown || target.isExchangeCooldown) return;
            if (target is Behaviour behaviour && (!behaviour || !behaviour.isActiveAndEnabled)) return;
            if (target is GravityBody2D other && other.ExchangeLocked) return;
            Vector2 incoming = target.gravityDir;
            if (Mathf.Abs(incoming.y) < 0.01f) return;
            Vector2 previous = gravityDir;
            ReceiveGravity(incoming);
            target.ReceiveGravity(previous);
        }

        public void ReceiveGravity(Vector2 direction)
        {
            if (ExchangeLocked || Mathf.Abs(direction.y) < 0.01f) return;
            cooldownUntil = Time.time + gravityExchangeCD;
            SetDirection(direction);
            ApplyGravity();
        }

        private void SetDirection(Vector2 direction)
        {
            Initialize();
            Vector2 normalized = direction.y > 0f ? Vector2.up : Vector2.down;
            bool changed = gravityDir != normalized;
            gravityDir = normalized;
            if (changed && body) body.WakeUp();
        }

        private void ApplyGravity()
        {
            Initialize();
            if (body) body.gravityScale = (isGravityFlipped ? -gravityScale : gravityScale)
                * multiplier * EnvironmentStrength;
        }

        [System.Serializable]
        private class State
        {
            public Vector2 direction;
            public float scale, multiplier, remaining;
            public float environmentStrength = 1f;
            public bool exchangeLocked;
        }

        public string CaptureState()
        {
            Initialize();
            return JsonUtility.ToJson(new State {
                direction = gravityDir, scale = gravityScale, multiplier = multiplier,
                remaining = Mathf.Max(0f, cooldownUntil - Time.time),
                environmentStrength = EnvironmentStrength, exchangeLocked = ExchangeLocked
            });
        }

        public void RestoreState(string data)
        {
            Initialize();
            var state = JsonUtility.FromJson<State>(data);
            if (state == null) return;
            SetDirection(Mathf.Abs(state.direction.y) > 0.01f ? state.direction : Vector2.down);
            gravityScale = Mathf.Max(0.01f, state.scale);
            multiplier = Mathf.Max(0f, state.multiplier);
            cooldownUntil = Time.time + Mathf.Max(0f, state.remaining);
            EnvironmentStrength = state.environmentStrength > 0f ? state.environmentStrength : 1f;
            ExchangeLocked = state.exchangeLocked;
            ApplyGravity();
        }

        public void RefreshState() => ApplyGravity();
        public void ResetForRespawn() => ResetGravity(false);
    }
}
