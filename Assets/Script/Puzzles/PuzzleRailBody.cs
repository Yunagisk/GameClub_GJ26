using GameClub.Player;
using UnityEngine;

namespace GameClub.Puzzles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(GravityBody2D))]
    public sealed class PuzzleRailBody : MonoBehaviour, IPuzzleResettable
    {
        public float minY;
        public float maxY = 5f;
        [Min(0.1f)] public float maxSpeed = 8f;
        [Min(0.1f)] public float acceleration = 18f;
        [Min(0f)] public float dockDelay = 0.2f;
        public string topFlag;
        public string bottomFlag;
        public bool latchDock = true;

        public bool AtTop => body && Mathf.Abs(body.position.y - maxY) <= 0.035f;
        public bool AtBottom => body && Mathf.Abs(body.position.y - minY) <= 0.035f;
        public float RailSpeed => speed;

        private Rigidbody2D body;
        private GravityBody2D gravity;
        private bool captured;
        private Vector2 initialPosition;
        private float initialRotation;
        private bool initialUp;
        private float speed;
        private float topTime;
        private float bottomTime;
        private bool topLatched;
        private bool bottomLatched;

        private void Awake() => Cache();
        private void Start() => CaptureInitialState();

        private void Cache()
        {
            if (!body) body = GetComponent<Rigidbody2D>();
            if (!gravity) gravity = GetComponent<GravityBody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.useFullKinematicContacts = true;
            if (maxY < minY) { float swap = minY; minY = maxY; maxY = swap; }
        }

        public void CaptureInitialState()
        {
            if (captured) return;
            Cache();
            initialPosition = body.position;
            initialPosition.y = Mathf.Clamp(initialPosition.y, minY, maxY);
            initialRotation = body.rotation;
            initialUp = gravity.gravityDir.sqrMagnitude > 0.5f ? gravity.isGravityFlipped : gravity.startsUpward;
            body.position = initialPosition;
            speed = 0f;
            captured = true;
        }

        private void FixedUpdate()
        {
            if (!captured) CaptureInitialState();
            float direction = gravity.gravityDir.y >= 0f ? 1f : -1f;
            speed = Mathf.MoveTowards(speed, direction * Mathf.Max(0.1f, maxSpeed), Mathf.Max(0.1f, acceleration) * Time.fixedDeltaTime);
            float nextY = Mathf.Clamp(body.position.y + speed * Time.fixedDeltaTime, minY, maxY);
            if ((nextY <= minY && speed < 0f) || (nextY >= maxY && speed > 0f)) speed = 0f;
            body.MovePosition(new Vector2(initialPosition.x, nextY));

            bool stopped = Mathf.Abs(speed) < 0.01f;
            topTime = AtTop && stopped ? topTime + Time.fixedDeltaTime : 0f;
            bottomTime = AtBottom && stopped ? bottomTime + Time.fixedDeltaTime : 0f;
            bool topStable = AtTop && stopped && topTime >= dockDelay;
            bool bottomStable = AtBottom && stopped && bottomTime >= dockDelay;
            topLatched |= topStable;
            bottomLatched |= bottomStable;
            var session = PuzzleSession.Instance;
            if (session)
            {
                if (!string.IsNullOrEmpty(topFlag)) session.SetFlag(topFlag, latchDock ? topLatched : topStable);
                if (!string.IsNullOrEmpty(bottomFlag)) session.SetFlag(bottomFlag, latchDock ? bottomLatched : bottomStable);
            }
        }

        public void ResetPuzzleState()
        {
            if (!captured) CaptureInitialState();
            speed = topTime = bottomTime = 0f;
            topLatched = bottomLatched = false;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = initialPosition;
            body.rotation = initialRotation;
            transform.position = new Vector3(initialPosition.x, initialPosition.y, transform.position.z);
            gravity.ResetGravity(initialUp);
            var session = PuzzleSession.Instance;
            if (session)
            {
                if (!string.IsNullOrEmpty(topFlag)) session.SetFlag(topFlag, false);
                if (!string.IsNullOrEmpty(bottomFlag)) session.SetFlag(bottomFlag, false);
            }
        }
    }
}
