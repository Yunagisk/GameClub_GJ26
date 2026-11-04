using UnityEngine;

namespace GameClub.Player
{
    // Keep these values in sync with the Animator's integer State parameter.
    public enum PlayerAnimationState
    {
        Idle = 0,
        Run = 1,
        Rise = 2,
        Fall = 3,
        Land = 4,
        Dash = 5,
        Interact = 6
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHorizontalMotor), typeof(GravityBody2D), typeof(PlayerGroundProbe))]
    public sealed class PlayerVisuals : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sprite;
        [SerializeField] private Animator animator;
        [SerializeField] private bool spriteFacesLeft = true;
        [Tooltip("Use the same clip as the Animator's Land state.")]
        [SerializeField] private AnimationClip landingClip;
        [Tooltip("Use the same clip as the Animator's Dash state.")]
        [SerializeField] private AnimationClip dashClip;
        [Tooltip("Use the same clip as the Animator's Interact state.")]
        [SerializeField] private AnimationClip interactionClip;
        [SerializeField, Min(0f)] private float movementThreshold = 0.1f;

        public PlayerAnimationState CurrentState { get; private set; }
        public bool IsGrounded { get; private set; }
        // Positive means moving away from the current gravity surface, including on ceilings.
        public float VerticalSpeed { get; private set; }

        private static readonly int State = Animator.StringToHash("State");
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Vertical = Animator.StringToHash("VerticalSpeed");
        private static readonly int Grounded = Animator.StringToHash("isGrounded");
        private static readonly int Flipped = Animator.StringToHash("isGravityFlipped");
        private static readonly int DashState = Animator.StringToHash("Base Layer.Dash");
        private static readonly int InteractState = Animator.StringToHash("Base Layer.Interact");

        private Rigidbody2D body;
        private PlayerHorizontalMotor motor;
        private GravityBody2D gravityBody;
        private PlayerGroundProbe ground;
        private PlayerInputReader input;
        private PlayerDash dash;
        private PlayerInteractionPicker interaction;
        private Vector3 spriteBaseLocalPosition;
        private bool hasSeparateVisual;
        private float dashUntil;
        private float interactionUntil;
        private float landingUntil;
        private float gravityChangedAtFixedTime = float.NegativeInfinity;
        private bool sampledGravity;
        private bool lastGravityFlipped;
        private bool sampledGround;
        private bool wasGrounded;
        private bool restartDash;
        private bool restartInteraction;
        private bool appliedState;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            gravityBody = GetComponent<GravityBody2D>();
            motor = GetComponent<PlayerHorizontalMotor>();
            ground = GetComponent<PlayerGroundProbe>();
            input = GetComponent<PlayerInputReader>();
            dash = GetComponent<PlayerDash>();
            interaction = GetComponent<PlayerInteractionPicker>();
            if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            hasSeparateVisual = sprite && sprite.transform != transform;
            if (hasSeparateVisual) spriteBaseLocalPosition = sprite.transform.localPosition;
        }

        private void OnEnable()
        {
            ResetPlayback();
            if (dash) dash.Dashed += OnDashed;
            if (interaction) interaction.Interacted += OnInteracted;
        }

        private void OnDisable()
        {
            if (dash) dash.Dashed -= OnDashed;
            if (interaction) interaction.Interacted -= OnInteracted;
            ResetPlayback();
        }

        private void ResetPlayback()
        {
            dashUntil = interactionUntil = landingUntil = 0f;
            gravityChangedAtFixedTime = float.NegativeInfinity;
            sampledGravity = sampledGround = wasGrounded = false;
            restartDash = restartInteraction = appliedState = false;
            CurrentState = PlayerAnimationState.Idle;
            IsGrounded = false;
            VerticalSpeed = 0f;
        }

        private void OnDashed()
        {
            dashUntil = Time.time + Duration(dashClip, 0.35f);
            interactionUntil = landingUntil = 0f;
            restartDash = true;
        }

        private void OnInteracted()
        {
            // Do not queue a delayed interaction pose behind a dash.
            if (Time.time < dashUntil) return;
            interactionUntil = Time.time + Duration(interactionClip, 0.55f);
            landingUntil = 0f;
            restartInteraction = true;
        }

        private static float Duration(AnimationClip clip, float fallback)
        {
            return clip && clip.length > 0f ? clip.length : fallback;
        }

        private void Update()
        {
            bool flipped = gravityBody.isGravityFlipped;
            if (sampledGravity && flipped != lastGravityFlipped)
            {
                // Input can swap gravity after the last physics step. The old floor
                // probe is invalid until PlayerController2D runs its next FixedUpdate.
                gravityChangedAtFixedTime = Time.fixedTime;
                wasGrounded = false;
                interactionUntil = landingUntil = 0f;
            }
            sampledGravity = true;
            lastGravityFlipped = flipped;

            Vector2 velocity = body.velocity;
            VerticalSpeed = Vector2.Dot(velocity, -gravityBody.gravityDir);
            IsGrounded = ground.isGrounded && Time.fixedTime > gravityChangedAtFixedTime;
            bool moving = (input && input.Horizontal != 0) || Mathf.Abs(velocity.x) > movementThreshold;

            if (sampledGround && !wasGrounded && IsGrounded)
                landingUntil = Time.time + Duration(landingClip, 0.14f);

            // These are visual interruptions only: never lock movement or change physics.
            if (!IsGrounded || moving)
                interactionUntil = landingUntil = 0f;

            PlayerAnimationState next;
            if (Time.time < dashUntil)
                next = PlayerAnimationState.Dash;
            else if (!IsGrounded)
                next = VerticalSpeed > 0.05f ? PlayerAnimationState.Rise : PlayerAnimationState.Fall;
            else if (Time.time < interactionUntil)
                next = PlayerAnimationState.Interact;
            else if (Time.time < landingUntil)
                next = PlayerAnimationState.Land;
            else
                next = moving ? PlayerAnimationState.Run : PlayerAnimationState.Idle;

            if (animator && animator.runtimeAnimatorController)
            {
                animator.SetFloat(Speed, Mathf.Abs(velocity.x));
                animator.SetFloat(Vertical, VerticalSpeed);
                animator.SetBool(Grounded, IsGrounded);
                animator.SetBool(Flipped, flipped);
                animator.SetInteger(State, (int)next);

                // AnyState transitions must disable Can Transition To Self. Replay
                // the same action only when a new successful gameplay event arrives.
                if (appliedState && next == CurrentState)
                {
                    if (next == PlayerAnimationState.Dash && restartDash && animator.HasState(0, DashState))
                        animator.Play(DashState, 0, 0f);
                    else if (next == PlayerAnimationState.Interact && restartInteraction && animator.HasState(0, InteractState))
                        animator.Play(InteractState, 0, 0f);
                }
            }

            CurrentState = next;
            appliedState = true;
            restartDash = restartInteraction = false;
            sampledGround = true;
            wasGrounded = IsGrounded;
        }

        private void LateUpdate()
        {
            if (!sprite) return;
            sprite.flipX = spriteFacesLeft ? motor.Facing > 0 : motor.Facing < 0;
            sprite.flipY = gravityBody.isGravityFlipped;
            if (hasSeparateVisual)
            {
                sprite.transform.localPosition = new Vector3(
                    sprite.flipX ? -spriteBaseLocalPosition.x : spriteBaseLocalPosition.x,
                    sprite.flipY ? -spriteBaseLocalPosition.y : spriteBaseLocalPosition.y,
                    spriteBaseLocalPosition.z);
            }
        }
    }
}
