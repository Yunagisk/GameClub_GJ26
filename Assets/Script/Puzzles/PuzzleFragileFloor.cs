using UnityEngine;

namespace GameClub.Puzzles
{
    [DisallowMultipleComponent, RequireComponent(typeof(Collider2D))]
    public sealed class PuzzleFragileFloor : MonoBehaviour, IPuzzleResettable
    {
        public float minimumImpactSpeed = 24f;
        public float requiredStrength = 1.4f;
        public string solvedFlag = "strength_done";
        public bool Broken { get; private set; }
        private Collider2D mainCollider;
        private Collider2D[] colliders;
        private SpriteRenderer[] sprites;
        private bool[] colliderStates;
        private bool[] spriteStates;
        private bool captured;

        private void Awake() => mainCollider = GetComponent<Collider2D>();
        private void Start() => CaptureInitialState();

        public void CaptureInitialState()
        {
            if (captured) return;
            mainCollider = GetComponent<Collider2D>();
            colliders = GetComponentsInChildren<Collider2D>(true);
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            colliderStates = new bool[colliders.Length];
            spriteStates = new bool[sprites.Length];
            for (int i = 0; i < colliders.Length; i++) colliderStates[i] = colliders[i].enabled;
            for (int i = 0; i < sprites.Length; i++) spriteStates[i] = sprites[i].enabled;
            captured = true;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            var session = PuzzleSession.Instance;
            if (Broken || !session || session.IsRespawning || !session.IsPlayer(collision.collider)) return;
            var environment = session.PlayerBody.GetComponent<PuzzlePlayerEnvironment>();
            if (!environment || environment.strength < requiredStrength || session.Gravity.isGravityFlipped) return;
            // Require a landing on the top face. A sideways bump or upward jump must not break it.
            Bounds bounds = mainCollider.bounds;
            if (collision.collider.bounds.center.y < bounds.center.y) return;
            bool onTop = false;
            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).point.y >= bounds.max.y - 0.2f) onTop = true;
            if (!onTop) return;
            float impactSpeed = Mathf.Max(-environment.LastFixedVelocity.y, Mathf.Abs(collision.relativeVelocity.y));
            if (impactSpeed < minimumImpactSpeed) return;
            Break(impactSpeed);
        }

        private void Break(float speed)
        {
            CaptureInitialState();
            Broken = true;
            foreach (var item in colliders) if (item) item.enabled = false;
            foreach (var item in sprites) if (item) item.enabled = false;
            var session = PuzzleSession.Instance;
            session.Solve(solvedFlag);
            session.PlayerBody.GetComponent<PuzzlePlayerEnvironment>().SetStrength(1f);
            session.PlayerBody.velocity = new Vector2(session.PlayerBody.velocity.x, -Mathf.Min(speed, 24f));

        }

        public void ResetPuzzleState()
        {
            CaptureInitialState();
            Broken = false;
            for (int i = 0; i < colliders.Length; i++) if (colliders[i]) colliders[i].enabled = colliderStates[i];
            for (int i = 0; i < sprites.Length; i++) if (sprites[i]) sprites[i].enabled = spriteStates[i];
            if (PuzzleSession.Instance && !string.IsNullOrEmpty(solvedFlag))
                PuzzleSession.Instance.SetFlag(solvedFlag, false);
        }
    }
}
