using System;
using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerDash : MonoBehaviour
    {
        [Min(0f)] public float dashSpeed = 16f;
        [Min(0f)] public float dashCooldown = 0.32f;
        [Min(0)] public int airDashCountMax = 1;
        [Range(0f, 1f)] public float dashMomentumRetention = 0.85f;
        public bool dashCanOverwriteSpeed = true;
        public event Action Dashed;
        private int airDashesUsed;
        private float nextDash;
        private bool retainNextStep;
        private Rigidbody2D body;

        private void Awake() => body = GetComponent<Rigidbody2D>();

        public void Prepare(bool grounded, float speedLimit)
        {
            if (grounded) airDashesUsed = 0;
            if (!retainNextStep) return;
            retainNextStep = false;
            Vector2 velocity = body.velocity;
            float excess = Mathf.Max(0f, Mathf.Abs(velocity.x) - speedLimit);
            velocity.x -= Mathf.Sign(velocity.x) * excess * (1f - dashMomentumRetention);
            body.velocity = velocity;
        }

        public void TryBurst(int direction, bool grounded)
        {
            if (Time.fixedTime < nextDash || (!grounded && airDashesUsed >= airDashCountMax)) return;
            Vector2 velocity = body.velocity;
            float burst = direction * dashSpeed;
            velocity.x = dashCanOverwriteSpeed ? burst : velocity.x + burst;
            body.velocity = velocity;
            if (!grounded) airDashesUsed++;
            nextDash = Time.fixedTime + dashCooldown;
            retainNextStep = true;
            Dashed?.Invoke();
        }
    }
}
