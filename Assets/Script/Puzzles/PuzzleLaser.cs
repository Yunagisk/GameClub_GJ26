using UnityEngine;

namespace GameClub.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class PuzzleLaser : MonoBehaviour, IPuzzleResettable
    {
        public Transform origin;
        public Vector2 direction = Vector2.up;
        [Min(0f)] public float maxLength = 12f;
        public LayerMask blockers = ~0;
        [Min(0.02f)] public float beamWidth = 0.12f;
        [Min(0f)] public float originOffset = 0.035f;
        [Tooltip("Zero means a continuous laser. A positive value is the full off/warning/on cycle.")]
        [Min(0f)] public float period;
        [Min(0f)] public float onDuration = 2f;
        [Min(0f)] public float warningTime = 0.6f;
        [Min(0f)] public float startDelay = 0.8f;
        public bool showGuideWhenOff = true;
        public LineRenderer beam;
        public Material beamMaterial;
        public int sortingOrder = 24;
        public Color activeColor = new Color(1f, 0.22f, 0.19f, 0.95f);
        public Color warningColor = new Color(1f, 0.67f, 0.18f, 0.8f);

        public bool IsLethal { get; private set; }
        public bool IsWarning { get; private set; }
        public float ActualLength { get; private set; }
        public Vector2 BeamStart { get; private set; }
        public Vector2 BeamEnd => BeamStart + BeamDirection * ActualLength;

        private Vector2 BeamDirection => direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        private Material ownedMaterial;
        private float elapsed;

        private void Awake() => EnsureVisual();
        private void OnDisable()
        {
            IsLethal = IsWarning = false;
            if (beam) beam.enabled = false;
        }

        private void EnsureVisual()
        {
            if (!beam)
            {
                var visual = new GameObject("Beam_VisibleLength");
                visual.transform.SetParent(transform, false);
                beam = visual.AddComponent<LineRenderer>();
            }
            if (beamMaterial) beam.sharedMaterial = beamMaterial;
            else if (!ownedMaterial)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader)
                {
                    ownedMaterial = new Material(shader) { name = "PuzzleLaser_Runtime" };
                    beam.sharedMaterial = ownedMaterial;
                }
                else Debug.LogError("PuzzleLaser requires an unlit line material.", this);
            }
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.numCapVertices = 4;
            beam.numCornerVertices = 2;
            beam.sortingOrder = sortingOrder;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;
        }

        public void CaptureInitialState() { }

        private void FixedUpdate()
        {
            elapsed += Time.fixedDeltaTime;
            EvaluateBeam();
            if (IsLethal && HitsPlayer()) PuzzleSession.Instance.KillPlayer("激光");
        }

        private void LateUpdate() => EvaluateBeam();

        private void EvaluateBeam()
        {
            if (!beam) EnsureVisual();
            EvaluatePhase();
            Vector2 rayDirection = BeamDirection;
            BeamStart = (Vector2)(origin ? origin.position : transform.position) + rayDirection * originOffset;
            ActualLength = Mathf.Max(0f, maxLength);
            var session = PuzzleSession.Instance;
            Rigidbody2D player = session ? session.PlayerBody : null;
            var hits = Physics2D.RaycastAll(BeamStart, rayDirection, ActualLength, blockers);
            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (!collider || collider.isTrigger || !collider.enabled) continue;
                if (player && collider.attachedRigidbody == player) continue;
                if (collider.transform == transform || collider.transform.IsChildOf(transform)) continue;
                ActualLength = Mathf.Min(ActualLength, Mathf.Max(0f, hit.distance));
            }

            if (!beam) return;
            beam.enabled = ActualLength > 0.005f && (IsLethal || IsWarning || showGuideWhenOff);
            Vector2 end = BeamEnd;
            float z = origin ? origin.position.z : transform.position.z;
            beam.SetPosition(0, new Vector3(BeamStart.x, BeamStart.y, z));
            beam.SetPosition(1, new Vector3(end.x, end.y, z));
            Color color = IsLethal ? activeColor : IsWarning ? warningColor : new Color(activeColor.r, activeColor.g, activeColor.b, 0.1f);
            float width = IsLethal ? beamWidth : IsWarning ? beamWidth * 0.6f : beamWidth * 0.22f;
            beam.startColor = beam.endColor = color;
            beam.startWidth = beam.endWidth = Mathf.Max(0.012f, width);
        }

        private void EvaluatePhase()
        {
            if (elapsed < startDelay)
            {
                IsLethal = false;
                IsWarning = elapsed >= Mathf.Max(0f, startDelay - warningTime) && period <= 0f;
                return;
            }
            if (period <= 0f)
            {
                IsLethal = true;
                IsWarning = false;
                return;
            }
            float cycle = Mathf.Max(0.1f, period);
            float on = Mathf.Clamp(onDuration, 0f, cycle);
            float off = cycle - on;
            float time = Mathf.Repeat(elapsed - startDelay, cycle);
            IsLethal = on > 0f && time >= off;
            IsWarning = on > 0f && !IsLethal && time >= Mathf.Max(0f, off - warningTime);
        }

        private bool HitsPlayer()
        {
            var session = PuzzleSession.Instance;
            if (!session || !session.PlayerBody || ActualLength <= 0.005f) return false;
            // A circle sweep matches the visible line's half-width. The shield truncates
            // this same segment before damage is queried, so nothing behind it is hit.
            float distance = Mathf.Max(0f, ActualLength - beamWidth * 0.5f - 0.005f);
            var hits = Physics2D.CircleCastAll(BeamStart, beamWidth * 0.5f, BeamDirection, distance, ~0);
            foreach (var hit in hits)
                if (hit.collider && hit.collider.enabled && !hit.collider.isTrigger && hit.collider.attachedRigidbody == session.PlayerBody) return true;
            return false;
        }

        public void ResetPuzzleState()
        {
            elapsed = 0f;
            IsLethal = IsWarning = false;
            EvaluateBeam();
        }

        private void OnDestroy()
        {
            if (ownedMaterial) Destroy(ownedMaterial);
        }
    }
}
