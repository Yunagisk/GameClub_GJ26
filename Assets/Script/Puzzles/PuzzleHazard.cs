using UnityEngine;

namespace GameClub.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class PuzzleHazard : MonoBehaviour
    {
        public bool active = true;
        public string reason = "碰到了危险设施，已返回检查点";
        private void OnTriggerEnter2D(Collider2D other) => Hit(other);
        private void OnTriggerStay2D(Collider2D other) => Hit(other);
        private void OnCollisionEnter2D(Collision2D collision) => Hit(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => Hit(collision.collider);
        private void Hit(Collider2D other)
        {
            var session = PuzzleSession.Instance;
            if (isActiveAndEnabled && active && session && session.IsPlayer(other)) session.KillPlayer(reason);
        }
    }
}
