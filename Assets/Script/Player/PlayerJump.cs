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
        private Vector2 jumpGravity;
        private bool jumpCut;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            gravityBody = GetComponent<GravityBody2D>();
        }

        public void Step(bool pressed, bool held, PlayerGroundProbe ground)
        {
            Vector2 down = gravityBody.gravityDir;
            if (ground.isGrounded || jumpGravity != down) jumpGravity = Vector2.zero;

            if (pressed && ground.isGrounded)
            {
                body.AddForce(-down * jumpForce, ForceMode2D.Impulse);
                ground.LeaveGround();
                jumpGravity = down;
                jumpCut = false;
            }

            Vector2 velocity = body.velocity;
            float fallingSpeed = Vector2.Dot(velocity, down);
            if (fallingSpeed >= 0f) jumpGravity = Vector2.zero;
            bool risingFromJump = jumpGravity != Vector2.zero;
            if (risingFromJump && !held && !jumpCut)
            {
                float cutSpeed = fallingSpeed / Mathf.Sqrt(Mathf.Max(1f, lowJumpGravityMultiplier));
                body.velocity = velocity + down * (cutSpeed - fallingSpeed);
                jumpCut = true;
            }

            float multiplier = 1f;
            if (!ground.isGrounded)
            {
                if (fallingSpeed > 0f) multiplier = fallGravityMultiplier;
                // Pressing jump again in midair cannot lengthen an already released jump.
                else if (!held || (risingFromJump && jumpCut)) multiplier = lowJumpGravityMultiplier;
            }
            gravityBody.SetMultiplier(multiplier);
        }
    }
}
