using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerGroundProbe), typeof(PlayerHorizontalMotor))]
    [RequireComponent(typeof(PlayerJump), typeof(PlayerDash), typeof(GravityBody2D))]
    [RequireComponent(typeof(PlayerInteractionPicker))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        private GravityBody2D gravityBody;
        public bool isGravityFlipped => gravityBody.isGravityFlipped;
        public bool isGrounded => ground.isGrounded;
        private PlayerInputReader input;
        private PlayerGroundProbe ground;
        private PlayerHorizontalMotor motor;
        private PlayerJump jump;
        private PlayerDash dash;

        private void Awake()
        {
            gravityBody = GetComponent<GravityBody2D>();
            input = GetComponent<PlayerInputReader>();
            ground = GetComponent<PlayerGroundProbe>();
            motor = GetComponent<PlayerHorizontalMotor>();
            jump = GetComponent<PlayerJump>();
            dash = GetComponent<PlayerDash>();
            var body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            if (!GetComponent<Collider2D>())
            {
                Debug.LogError("Add a solid Collider2D to the player root.", this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            input.Consume(out bool jumpPressed, out bool dashPressed, out int dashAxis);
            ground.Refresh();
            dash.Prepare(isGrounded, motor.SpeedLimit(isGrounded));
            jump.Step(jumpPressed, input.JumpHeld, ground);
            int direction = dashAxis != 0 ? dashAxis : motor.Facing;
            motor.Step(input.Horizontal, isGrounded);
            if (dashPressed) dash.TryBurst(direction, isGrounded);
        }
    }
}
