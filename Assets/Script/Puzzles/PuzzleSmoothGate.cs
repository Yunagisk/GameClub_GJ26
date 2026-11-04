using UnityEngine;

namespace GameClub.Puzzles
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class PuzzleSmoothGate : MonoBehaviour, IPuzzleResettable
    {
        public Vector2 openOffset = Vector2.up * 8f;
        public string[] requiredFlags;
        [Min(0.05f)] public float travelTime = 0.65f;
        public bool latchOpen = true;
        [Min(0f)] public float safetyMargin = 0.12f;

        public float OpenAmount => Smooth(progress);
        public bool IsOpen => OpenAmount >= 0.995f;
        public bool ClosingBlocked { get; private set; }

        private Rigidbody2D body;
        private Collider2D[] leafColliders;
        private Vector2 closedPosition;
        private float initialRotation;
        private float progress;
        private bool latched;
        private bool captured;

        private void Awake() => Cache();
        private void Start() => CaptureInitialState();

        private void Cache()
        {
            if (!body) body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.useFullKinematicContacts = true;
            leafColliders = GetComponentsInChildren<Collider2D>(true);
        }

        public void CaptureInitialState()
        {
            if (captured) return;
            Cache();
            closedPosition = body.position;
            initialRotation = body.rotation;
            captured = true;
        }

        private bool RequirementsMet()
        {
            var session = PuzzleSession.Instance;
            if (!session || requiredFlags == null || requiredFlags.Length == 0) return false;
            foreach (string flag in requiredFlags)
                if (string.IsNullOrEmpty(flag) || !session.Solved(flag)) return false;
            return true;
        }

        private void FixedUpdate()
        {
            if (!captured) CaptureInitialState();
            bool requested = RequirementsMet();
            if (requested && latchOpen) latched = true;
            bool opening = requested || (latchOpen && latched);
            float next = Mathf.MoveTowards(progress, opening ? 1f : 0f, Time.fixedDeltaTime / Mathf.Max(0.05f, travelTime));
            Vector2 desired = closedPosition + openOffset * Smooth(next);
            ClosingBlocked = next < progress && PlayerInClosingSweep(desired - body.position);
            if (ClosingBlocked)
            {
                body.MovePosition(body.position);
                return;
            }
            progress = next;
            body.MovePosition(desired);
        }

        private bool PlayerInClosingSweep(Vector2 delta)
        {
            var session = PuzzleSession.Instance;
            if (!session || !session.PlayerBody) return false;
            var playerColliders = session.PlayerBody.GetComponentsInChildren<Collider2D>();
            foreach (var leaf in leafColliders)
            {
                if (!leaf || !leaf.enabled || leaf.isTrigger) continue;
                Bounds swept = leaf.bounds;
                swept.Encapsulate(leaf.bounds.min + (Vector3)delta);
                swept.Encapsulate(leaf.bounds.max + (Vector3)delta);
                swept.Expand(new Vector3(safetyMargin * 2f, safetyMargin * 2f, 0f));
                foreach (var playerCollider in playerColliders)
                {
                    if (!playerCollider || !playerCollider.enabled || playerCollider.isTrigger) continue;
                    // Physics2D ignores rendering depth. Do not let a different Z used
                    // for sprite ordering bypass the gate's anti-crush protection.
                    Bounds playerBounds = playerCollider.bounds;
                    bool overlapsX = swept.min.x <= playerBounds.max.x && swept.max.x >= playerBounds.min.x;
                    bool overlapsY = swept.min.y <= playerBounds.max.y && swept.max.y >= playerBounds.min.y;
                    if (overlapsX && overlapsY) return true;
                }
            }
            return false;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        public void ResetPuzzleState()
        {
            if (!captured) CaptureInitialState();
            progress = 0f;
            latched = ClosingBlocked = false;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = closedPosition;
            body.rotation = initialRotation;
            transform.position = new Vector3(closedPosition.x, closedPosition.y, transform.position.z);
        }
    }
}
