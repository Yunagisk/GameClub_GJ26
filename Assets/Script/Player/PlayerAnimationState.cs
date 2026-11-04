using UnityEngine;

namespace GameClub.Player
{
    [RequireComponent(typeof(PlayerLifecycle))]
    public sealed class PlayerAnimationState : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer visual;
        public PlayerMotionState Motion { get; private set; }
        public PlayerLifeState Life => lifecycle.State;
        private PlayerLifecycle lifecycle;
        private Rigidbody2D body;
        private PlayerGroundProbe ground;
        private GravityBody2D gravityBody;
        private Color normalColor;

        private void Awake()
        {
            lifecycle = GetComponent<PlayerLifecycle>();
            body = GetComponent<Rigidbody2D>();
            ground = GetComponent<PlayerGroundProbe>();
            gravityBody = GetComponent<GravityBody2D>();
            if (!visual) visual = GetComponentInChildren<SpriteRenderer>();
            if (visual) normalColor = visual.color;
        }

        private void LateUpdate()
        {
            float fallSpeed = Vector2.Dot(body.velocity, gravityBody.gravityDir);
            Motion = ground.isGrounded
                ? (Mathf.Abs(body.velocity.x) > 0.05f ? PlayerMotionState.Running : PlayerMotionState.Idle)
                : (fallSpeed < 0f ? PlayerMotionState.Rising : PlayerMotionState.Falling);
            if (visual)
                visual.color = Life == PlayerLifeState.Dying ? Color.red
                    : Life == PlayerLifeState.Reviving ? Color.cyan : normalColor;
        }
    }
}
