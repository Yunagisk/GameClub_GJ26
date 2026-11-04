using System.Collections.Generic;
using UnityEngine;

namespace GameClub.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class PuzzleRoom : MonoBehaviour
    {
        public string roomId;
        public string displayName;
        [TextArea] public string objective;
        public Vector2 spawnPoint;
        public bool spawnUpward;
        public string[] resetFlags = new string[0];
        public Transform[] extraResetRoots = new Transform[0];
        public bool HasCaptured { get; private set; }
        private readonly List<IPuzzleResettable> resetObjects = new List<IPuzzleResettable>();

        private void Start() => CaptureInitialState();

        public void CaptureInitialState()
        {
            if (HasCaptured) return;
            resetObjects.Clear();
            var seen = new HashSet<MonoBehaviour>();
            Gather(transform, seen);
            if (extraResetRoots != null)
                foreach (var extra in extraResetRoots)
                    if (extra) Gather(extra, seen);
            foreach (var item in resetObjects) item.CaptureInitialState();
            HasCaptured = true;
        }

        private void Gather(Transform root, HashSet<MonoBehaviour> seen)
        {
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!component || !seen.Add(component)) continue;
                if (component is IPuzzleResettable resettable) resetObjects.Add(resettable);
            }
        }

        public void ResetRoomState()
        {
            CaptureInitialState();
            if (PuzzleSession.Instance && resetFlags != null)
                foreach (string flag in resetFlags) PuzzleSession.Instance.SetFlag(flag, false);
            foreach (var item in resetObjects)
            {
                if (item is Object unityObject && !unityObject) continue;
                item.ResetPuzzleState();
            }
        }
    }
}
