using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class GravityBody2D : MonoBehaviour, IGravityExchange
    {
        [Min(0.01f)] public float gravityScale = 3.5f;
        [Min(0f)] public float gravityExchangeCD = 0.25f;
        public bool startsUpward;

        public Vector2 gravityDir { get; private set; }
        public bool isGravityFlipped => gravityDir.y > 0f;
        public bool isExchangeCooldown => Time.time < cooldownUntil;
        private Rigidbody2D body;
        private float multiplier = 1f;
        private float cooldownUntil;

        private void Awake()
        {
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
    }
}
