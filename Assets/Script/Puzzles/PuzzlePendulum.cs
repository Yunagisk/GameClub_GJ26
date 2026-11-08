using GameClub.Player;
using UnityEngine;

namespace GameClub.Puzzles
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D), typeof(GravityBody2D))]
    public sealed class PuzzlePendulum : MonoBehaviour, IPuzzleResettable
    {
        [Min(0.1f)] public float armLength = 4.5f;
        [Range(1f, 80f)] public float swingAmplitude = 42f;
        [Min(0.4f)] public float period = 3.8f;
        [Min(1f)] public float flipSpeed = 65f;
        public float phaseOffset;
        public Transform bob;
        public string upperFlag = "pendulum_routed";
        [Min(0f)] public float upperDockDelay = 0.25f;

        public Vector2 BobPosition => body.position + (Vector2)(Quaternion.Euler(0f, 0f, body.rotation) * (Vector3.down * armLength));
        public bool UpperLatched => upperLatched;

        private Rigidbody2D body;
        private GravityBody2D gravity;
        private Vector2 pivot;
        private bool initialUp;
        private bool captured;
        private float equilibrium;
        private float phase;
        private float upperTime;
        private bool upperLatched;

        private void Awake() => Cache();
        private void Start() => CaptureInitialState();

        private void Cache()
        {
            if (!body) body = GetComponent<Rigidbody2D>();
            if (!gravity) gravity = GetComponent<GravityBody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.useFullKinematicContacts = true;
            if (bob) bob.localPosition = new Vector3(0f, -armLength, bob.localPosition.z);
        }

        public void CaptureInitialState()
        {
            if (captured) return;
            Cache();
            pivot = body.position;
            initialUp = gravity.gravityDir.sqrMagnitude > 0.5f ? gravity.isGravityFlipped : gravity.startsUpward;
            equilibrium = initialUp ? 180f : 0f;
            phase = phaseOffset;
            body.rotation = equilibrium + Mathf.Sin(phase) * swingAmplitude;
            captured = true;
        }

        private void FixedUpdate()
        {
            if (!captured) CaptureInitialState();
            phase = Mathf.Repeat(phase + Time.fixedDeltaTime * Mathf.PI * 2f / Mathf.Max(0.4f, period), Mathf.PI * 2f);
            equilibrium = Mathf.MoveTowardsAngle(equilibrium, gravity.isGravityFlipped ? 180f : 0f, flipSpeed * Time.fixedDeltaTime);
            float angle = equilibrium + Mathf.Sin(phase) * swingAmplitude;
            body.MovePosition(pivot);
            body.MoveRotation(angle);
            bool inReceiver = gravity.isGravityFlipped && BobPosition.y >= pivot.y + armLength * 0.8f;
            upperTime = inReceiver ? upperTime + Time.fixedDeltaTime : 0f;
            upperLatched |= inReceiver && upperTime >= upperDockDelay;
            if (upperLatched && !string.IsNullOrEmpty(upperFlag) && PuzzleSession.Instance)
                PuzzleSession.Instance.Solve(upperFlag);
        }

        public void ResetPuzzleState()
        {
            if (!captured) CaptureInitialState();
            phase = phaseOffset;
            equilibrium = initialUp ? 180f : 0f;
            upperTime = 0f;
            upperLatched = false;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = pivot;
            body.rotation = equilibrium + Mathf.Sin(phase) * swingAmplitude;
            transform.position = new Vector3(pivot.x, pivot.y, transform.position.z);
            gravity.ResetGravity(initialUp);
            if (!string.IsNullOrEmpty(upperFlag) && PuzzleSession.Instance)
                PuzzleSession.Instance.SetFlag(upperFlag, false);
        }
    }
}
