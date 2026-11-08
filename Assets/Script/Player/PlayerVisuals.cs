using UnityEngine;

namespace GameClub.Player
{
    // Keep these values in sync with the Animator's integer State parameter.
    public enum PlayerVisualState
    {
        Idle = 0,
        Run = 1,
        Rise = 2,
        Fall = 3,
        Land = 4,
        Dash = 5,
        Interact = 6,
        GravityFlip = 7,
        Death = 8,
        Revive = 9,
        IdleReady = 10
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
        [SerializeField] private AnimationClip gravityFlipClip;
        [SerializeField] private AnimationClip deathClip;
        [SerializeField] private AnimationClip reviveClip;
        [Tooltip("Seconds standing still before playing the head-down idle once.")]
        [SerializeField, Min(0f)] private float idleDelay = 1.5f;
        [Tooltip("Minimum duration of the flying dash pose on the ground.")]
        [SerializeField, Min(0f)] private float dashPoseDuration = 0.35f;
        [SerializeField] private bool holdAirDashPoseUntilLanding = true;
        [SerializeField, Min(0f)] private float movementThreshold = 0.1f;

        public PlayerVisualState CurrentState { get; private set; }
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
        private static readonly int FlipState = Animator.StringToHash("Base Layer.GravityFlip");
        private static readonly int DeathState = Animator.StringToHash("Base Layer.Death");
        private static readonly int ReviveState = Animator.StringToHash("Base Layer.Revive");

        private Rigidbody2D body;
        private PlayerHorizontalMotor motor;
        private GravityBody2D gravityBody;
        private PlayerGroundProbe ground;
        private PlayerInputReader input;
        private PlayerDash dash;
        private PlayerInteractionPicker interaction;
        private PlayerLifecycle lifecycle;
        private PlayerAnimationState animationState;
        private Vector3 spriteBaseLocalPosition;
        private bool hasSeparateVisual;
        private float dashUntil;
        private float interactionUntil;
        private float landingUntil;
        private float flipUntil;
        private float idleStartedAt = -1f;
        private bool dashFlightActive;
        private float gravityChangedAtFixedTime = float.NegativeInfinity;
        private bool sampledGravity;
        private bool lastGravityFlipped;
        private bool sampledGround;
        private bool wasGrounded;
        private bool restartDash;
        private bool restartInteraction;
        private bool restartFlip;
        private bool sampledLife;
        private PlayerLifeState lastLife;
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
            lifecycle = GetComponent<PlayerLifecycle>();
            animationState = GetComponent<PlayerAnimationState>();
            if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (animator && animator.GetComponent<SpriteRenderer>()) sprite = animator.GetComponent<SpriteRenderer>();
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
            dashUntil = interactionUntil = landingUntil = flipUntil = 0f;
            idleStartedAt = -1f;
            dashFlightActive = false;
            gravityChangedAtFixedTime = float.NegativeInfinity;
            sampledGravity = sampledGround = wasGrounded = sampledLife = false;
            restartDash = restartInteraction = restartFlip = appliedState = false;
            CurrentState = PlayerVisualState.Idle;
            IsGrounded = false;
            VerticalSpeed = 0f;
        }

        private void OnDashed()
        {
            if (Life != PlayerLifeState.Playing) return;
            dashUntil = Time.time + Mathf.Max(dashPoseDuration, Duration(dashClip, 0.35f));
            dashFlightActive = true;
            idleStartedAt = -1f;
            interactionUntil = landingUntil = flipUntil = 0f;
            restartDash = true;
        }

        private void OnInteracted()
        {
            // Do not queue a delayed interaction pose behind a dash.
            if (Life != PlayerLifeState.Playing || dashFlightActive || Time.time < dashUntil || Time.time < flipUntil) return;
            interactionUntil = Time.time + Duration(interactionClip, 0.55f);
            landingUntil = 0f;
            restartInteraction = true;
        }

        private static float Duration(AnimationClip clip, float fallback)
        {
            return clip && clip.length > 0f ? clip.length : fallback;
        }

        private PlayerLifeState Life => animationState ? animationState.Life
            : lifecycle ? lifecycle.State : PlayerLifeState.Playing;

        private void Update()
        {
            PlayerLifeState life = Life;
            bool lifeChanged = sampledLife && life != lastLife;
            if (lifeChanged || life != PlayerLifeState.Playing)
            {
                dashUntil = interactionUntil = landingUntil = flipUntil = 0f;
                idleStartedAt = -1f;
                dashFlightActive = false;
                wasGrounded = false;
                sampledGround = false;
            }
            bool flipped = gravityBody.isGravityFlipped;
            if (sampledGravity && flipped != lastGravityFlipped)
            {
                // Input can swap gravity after the last physics step. The old floor
                // probe is invalid until PlayerController2D runs its next FixedUpdate.
                gravityChangedAtFixedTime = Time.fixedTime;
                wasGrounded = false;
                interactionUntil = landingUntil = dashUntil = 0f;
                dashFlightActive = false;
                idleStartedAt = -1f;
                if (life == PlayerLifeState.Playing && !lifeChanged && gravityFlipClip)
                {
                    flipUntil = Time.time + Duration(gravityFlipClip, 0.35f);
                    restartFlip = true;
                }
            }
            sampledGravity = true;
            lastGravityFlipped = flipped;

            Vector2 velocity = body.velocity;
            VerticalSpeed = Vector2.Dot(velocity, -gravityBody.gravityDir);
            IsGrounded = ground.isGrounded && Time.fixedTime > gravityChangedAtFixedTime;
            PlayerMotionState motion = animationState ? animationState.Motion
                : IsGrounded ? (Mathf.Abs(velocity.x) > movementThreshold ? PlayerMotionState.Running : PlayerMotionState.Idle)
                : VerticalSpeed > 0.05f ? PlayerMotionState.Rising : PlayerMotionState.Falling;
            bool moving = (input && input.Horizontal != 0) || Mathf.Abs(velocity.x) > movementThreshold
                || (animationState && motion == PlayerMotionState.Running);

            if (sampledGround && !wasGrounded && IsGrounded && life == PlayerLifeState.Playing)
                landingUntil = Time.time + Duration(landingClip, 0.14f);

            // These are visual interruptions only: never lock movement or change physics.
            if (!IsGrounded || moving)
                interactionUntil = landingUntil = 0f;

            if (dashFlightActive && Time.time >= dashUntil
                && (IsGrounded || !holdAirDashPoseUntilLanding || Mathf.Abs(velocity.x) <= movementThreshold))
                dashFlightActive = false;
            bool dashPoseActive = Time.time < dashUntil || dashFlightActive;
            bool resting = life == PlayerLifeState.Playing && IsGrounded && !moving
                && !dashPoseActive && Time.time >= flipUntil
                && Time.time >= interactionUntil && Time.time >= landingUntil;
            if (!resting) idleStartedAt = -1f;
            else if (idleStartedAt < 0f) idleStartedAt = Time.time;

            PlayerVisualState next;
            if (life == PlayerLifeState.Dying)
                next = deathClip ? PlayerVisualState.Death : PlayerVisualState.Idle;
            else if (life == PlayerLifeState.Reviving)
                next = reviveClip ? PlayerVisualState.Revive : PlayerVisualState.Idle;
            else if (life == PlayerLifeState.Restoring)
                next = PlayerVisualState.IdleReady;
            else if (Time.time < flipUntil)
                next = PlayerVisualState.GravityFlip;
            else if (dashPoseActive)
                next = PlayerVisualState.Dash;
            else if (!IsGrounded)
            {
                // Motion is the incoming component's read-only animation interface.
                // Resolve its LateUpdate one-frame boundary with current physics velocity.
                bool rising = motion == PlayerMotionState.Rising;
                if (motion == PlayerMotionState.Idle || motion == PlayerMotionState.Running
                    || (rising && VerticalSpeed <= 0f) || (!rising && VerticalSpeed > 0.05f))
                    rising = VerticalSpeed > 0.05f;
                next = rising ? PlayerVisualState.Rise : PlayerVisualState.Fall;
            }
            else if (Time.time < interactionUntil)
                next = PlayerVisualState.Interact;
            else if (Time.time < landingUntil)
                next = PlayerVisualState.Land;
            else
                next = moving ? PlayerVisualState.Run
                    : Time.time - idleStartedAt < idleDelay ? PlayerVisualState.IdleReady : PlayerVisualState.Idle;

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
                    if (next == PlayerVisualState.Dash && restartDash && animator.HasState(0, DashState))
                        animator.Play(DashState, 0, 0f);
                    else if (next == PlayerVisualState.Interact && restartInteraction && animator.HasState(0, InteractState))
                        animator.Play(InteractState, 0, 0f);
                    else if (next == PlayerVisualState.GravityFlip && restartFlip && animator.HasState(0, FlipState))
                        animator.Play(FlipState, 0, 0f);
                    else if (next == PlayerVisualState.Death && lifeChanged && animator.HasState(0, DeathState))
                        animator.Play(DeathState, 0, 0f);
                    else if (next == PlayerVisualState.Revive && lifeChanged && animator.HasState(0, ReviveState))
                        animator.Play(ReviveState, 0, 0f);
                }
            }

            CurrentState = next;
            appliedState = true;
            restartDash = restartInteraction = restartFlip = false;
            sampledGround = true;
            wasGrounded = IsGrounded;
            sampledLife = true;
            lastLife = life;
        }

        private void LateUpdate()
        {
            if (!sprite) return;
            sprite.flipX = spriteFacesLeft ? motor.Facing > 0 : motor.Facing < 0;
            // The artist's seven-frame flip goes from upside down to upright.
            // Flipping the whole clip by the target direction gives the opposite
            // turn when needed. Do not add a second Transform rotation.
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
