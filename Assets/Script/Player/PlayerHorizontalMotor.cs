using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerHorizontalMotor : MonoBehaviour
    {
        [Min(0f)] public float groundMaxSpeed = 7f;
        [Min(0f)] public float airMaxSpeed = 6.5f;
        [Min(0f)] public float groundAccel = 55f;
        [Min(0f)] public float airAccel = 32f;
        [Min(0f)] public float groundDecel = 70f;
        [Min(0f)] public float airDecel = 20f;
        public int Facing { get; private set; } = 1;
        private Rigidbody2D body;

        private void Awake() => body = GetComponent<Rigidbody2D>();

        public void ResetForRespawn() => Facing = 1;

        public float SpeedLimit(bool grounded) => grounded ? groundMaxSpeed : airMaxSpeed;

        public void Step(int axis, bool grounded)
        {
            if (axis != 0) Facing = axis;
            float target = axis * SpeedLimit(grounded);
            Vector2 velocity = body.velocity;
            bool braking = axis == 0 || axis * velocity.x > Mathf.Abs(target);
            float rate = braking ? (grounded ? groundDecel : airDecel) : (grounded ? groundAccel : airAccel);
            velocity.x = Mathf.MoveTowards(velocity.x, target, rate * Time.fixedDeltaTime);
            body.velocity = velocity;
        }
    }
}
