using System.Collections.Generic;
using UnityEngine;

namespace GameClub.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class PuzzleGravityField : MonoBehaviour
    {
        public Vector2 size = new Vector2(6f, 10f);
        public bool upward = true;
        public int priority;
        public string enableFlag;
        public string invertFlag;
        public static readonly List<PuzzleGravityField> ActiveFields = new List<PuzzleGravityField>();

        private void OnEnable()
        {
            if (!ActiveFields.Contains(this)) ActiveFields.Add(this);
        }
        private void OnDisable() => ActiveFields.Remove(this);

        public bool Powered(PuzzleSession session) =>
            string.IsNullOrEmpty(enableFlag) || (session && session.Solved(enableFlag));

        public Vector2 Direction(PuzzleSession session)
        {
            bool flipped = !string.IsNullOrEmpty(invertFlag) && session && session.Solved(invertFlag);
            return upward != flipped ? Vector2.up : Vector2.down;
        }

        public bool Contains(Vector2 point)
        {
            Vector2 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) < Mathf.Abs(size.x) * 0.5f
                && Mathf.Abs(local.y) < Mathf.Abs(size.y) * 0.5f;
        }

        private void OnDrawGizmosSelected()
        {
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = upward ? new Color(0.65f, 0.45f, 1f, 0.3f) : new Color(0.25f, 0.75f, 1f, 0.3f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, size.y, 0.05f));
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.05f));
            Gizmos.matrix = previous;
        }
    }
}
