using GameClub.Player;
using UnityEngine;

namespace GameClub.Puzzles
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent, RequireComponent(typeof(GravityBody2D), typeof(Rigidbody2D))]
    public sealed class PuzzlePlayerEnvironment : MonoBehaviour
    {
        [Range(0.05f, 3f)] public float strength = 1f;
        public PuzzleGravityField ActiveField { get; private set; }
        public Vector2 LastFixedVelocity { get; private set; }
        private GravityBody2D gravity;
        private Rigidbody2D body;

        private void Awake()
        {
            gravity = GetComponent<GravityBody2D>();
            body = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            LastFixedVelocity = body.velocity;
            if (PuzzleSession.Instance && PuzzleSession.Instance.IsRespawning) return;
            ApplyEnvironment();
        }

        public void SetStrength(float value)
        {
            strength = Mathf.Clamp(value, 0.05f, 3f);
            if (gravity) ApplyEnvironment();
        }

        public void ResetEnvironment(float value = 1f)
        {
            strength = Mathf.Clamp(value, 0.05f, 3f);
            ActiveField = null;
            LastFixedVelocity = Vector2.zero;
            if (gravity) gravity.SetEnvironment(strength, false, Vector2.zero);
        }

        public void ApplyEnvironment()
        {
            if (!gravity || !body) return;
            PuzzleGravityField chosen = null;
            var session = PuzzleSession.Instance;
            foreach (var field in PuzzleGravityField.ActiveFields)
            {
                if (!field || !field.isActiveAndEnabled || !field.Powered(session)
                    || !field.Contains(body.worldCenterOfMass)) continue;
                if (!chosen || field.priority > chosen.priority
                    || (field.priority == chosen.priority && field.GetInstanceID() < chosen.GetInstanceID()))
                    chosen = field;
            }
            ActiveField = chosen;
            gravity.SetEnvironment(strength, chosen, chosen ? chosen.Direction(session) : Vector2.zero);
        }

        private void OnDisable()
        {
            if (gravity) gravity.SetEnvironment(1f, false, Vector2.zero);
            ActiveField = null;
        }
    }
}
