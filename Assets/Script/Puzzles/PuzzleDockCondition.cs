using UnityEngine;

namespace GameClub.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class PuzzleDockCondition : MonoBehaviour, IPuzzleResettable
    {
        public PuzzleRailBody first;
        public PuzzleRailBody second;
        public bool firstRequireTop;
        public bool secondRequireTop = true;
        public string solvedFlag;
        public bool latch = true;
        [Min(0f)] public float settleTime = 0.2f;

        public bool IsSatisfied { get; private set; }
        private float dwell;
        private bool latched;

        public void CaptureInitialState() { }

        private void FixedUpdate()
        {
            bool firstReady = first && (firstRequireTop ? first.AtTop : first.AtBottom) && Mathf.Abs(first.RailSpeed) < 0.01f;
            bool secondReady = !second || ((secondRequireTop ? second.AtTop : second.AtBottom) && Mathf.Abs(second.RailSpeed) < 0.01f);
            bool both = firstReady && secondReady;
            dwell = both ? dwell + Time.fixedDeltaTime : 0f;
            bool stable = both && dwell >= settleTime;
            latched |= stable;
            IsSatisfied = latch ? latched : stable;
            if (!string.IsNullOrEmpty(solvedFlag) && PuzzleSession.Instance)
                PuzzleSession.Instance.SetFlag(solvedFlag, IsSatisfied);
        }

        public void ResetPuzzleState()
        {
            dwell = 0f;
            latched = IsSatisfied = false;
            if (!string.IsNullOrEmpty(solvedFlag) && PuzzleSession.Instance)
                PuzzleSession.Instance.SetFlag(solvedFlag, false);
        }
    }
}
