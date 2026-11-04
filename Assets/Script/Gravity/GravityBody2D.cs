using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class GravityBody2D : MonoBehaviour, IGravityExchange
    {
        [Min(0.01f)] public float gravityScale = 3.5f;
        [Min(0f)] public float gravityExchangeCD = 0.3f;
        public bool startsUpward;

        public Vector2 gravityDir { get; private set; }
        public bool isGravityFlipped => gravityDir.y > 0f;
        public bool isExchangeCooldown => ExchangeLocked || Time.time < cooldownUntil;
        public bool ExchangeLocked { get; private set; }
        public float EnvironmentStrength { get; private set; } = 1f;
        private Rigidbody2D body;
        private float multiplier = 1f;
        private float cooldownUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            gravityDir = startsUpward ? Vector2.up : Vector2.down;
            ApplyGravity();
        }

        // The jump controller owns this multiplier; environment strength composes with it.
        public void SetMultiplier(float value)
        {
            multiplier = Mathf.Max(0f, value);
            ApplyGravity();
        }

        public void SetEnvironment(float strength, bool lockExchange, Vector2 forcedDirection)
        {
            EnvironmentStrength = Mathf.Max(0.01f, strength);
            ExchangeLocked = lockExchange;
            if (lockExchange && Mathf.Abs(forcedDirection.y) > 0.01f)
                SetDirection(forcedDirection);
            ApplyGravity();
        }

        public void ResetGravity(bool upward, float strength = 1f)
        {
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
            // Validate both participants before either direction is changed.
            if (target is GravityBody2D other && other.ExchangeLocked) return;
            Vector2 previous = gravityDir;
            Vector2 incoming = target.gravityDir;
            if (Mathf.Abs(incoming.y) < 0.01f) return;
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
            Vector2 normalized = direction.y > 0f ? Vector2.up : Vector2.down;
            bool changed = gravityDir != normalized;
            gravityDir = normalized;
            if (!body) body = GetComponent<Rigidbody2D>();
            if (changed && body) body.WakeUp();
        }

        private void ApplyGravity()
        {
            if (!body) body = GetComponent<Rigidbody2D>();
            if (gravityDir == Vector2.zero) gravityDir = startsUpward ? Vector2.up : Vector2.down;
            if (body) body.gravityScale = (isGravityFlipped ? -gravityScale : gravityScale)
                * multiplier * EnvironmentStrength;
        }
    }
}
