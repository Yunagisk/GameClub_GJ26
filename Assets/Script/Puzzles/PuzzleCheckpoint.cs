using UnityEngine;

namespace GameClub.Puzzles
{
    [DisallowMultipleComponent, RequireComponent(typeof(Collider2D))]
    public sealed class PuzzleCheckpoint : MonoBehaviour
    {
        public PuzzleRoom room;
        public bool normalizeGravity = true;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
            room = GetComponentInParent<PuzzleRoom>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var session = PuzzleSession.Instance;
            if (!session || session.IsRespawning || !room || !session.IsPlayer(other)) return;
            if (session.CurrentRoom != room) session.SetCheckpoint(room, normalizeGravity);
        }
    }
}
