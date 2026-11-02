using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerHorizontalMotor), typeof(GravityBody2D))]
    public sealed class PlayerVisuals : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sprite;
        [SerializeField] private Animator animator;
        private static readonly int Flipped = Animator.StringToHash("isGravityFlipped");
        private PlayerHorizontalMotor motor;
        private GravityBody2D gravityBody;

        private void Awake()
        {
            gravityBody = GetComponent<GravityBody2D>();
            motor = GetComponent<PlayerHorizontalMotor>();
            if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();
            if (!animator) animator = GetComponentInChildren<Animator>();
        }

        private void LateUpdate()
        {
            if (sprite)
            {
                sprite.flipX = motor.Facing < 0;
                sprite.flipY = gravityBody.isGravityFlipped;
            }
            if (animator) animator.SetBool(Flipped, gravityBody.isGravityFlipped);
        }
    }
}
