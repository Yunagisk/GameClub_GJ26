using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(GravityBody2D))]
    public sealed class PlayerJump : MonoBehaviour
    {
        [Min(0f)] public float jumpForce = 12f;
        [Min(1f)] public float fallGravityMultiplier = 1.65f;
        [Min(1f)] public float lowJumpGravityMultiplier = 2f;
        private Rigidbody2D body;
        private GravityBody2D gravityBody;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            gravityBody = GetComponent<GravityBody2D>();
        }

        public void Step(bool pressed, bool held, PlayerGroundProbe ground)
        {
            if (pressed && ground.isGrounded)
            {
                body.AddForce(-gravityBody.gravityDir * jumpForce, ForceMode2D.Impulse);
                ground.LeaveGround();
            }
            float fallingSpeed = Vector2.Dot(body.velocity, gravityBody.gravityDir);
            float multiplier = 1f;
            if (!ground.isGrounded)
            {
                if (fallingSpeed > 0f) multiplier = fallGravityMultiplier;
                else if (!held) multiplier = lowJumpGravityMultiplier;
            }
            gravityBody.SetMultiplier(multiplier);
        }
    }
}
