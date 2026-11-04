using GameClub.Player;
using UnityEngine;

namespace GameClub.Puzzles
{
    public enum PuzzleTerminalAction { SetLowGravity, SetHighGravity, SolveFlag, Complete }

    [DisallowMultipleComponent]
    public sealed class PuzzleTerminal : InteractableBase
    {
        public PuzzleTerminalAction action;
        public string flag;
        public string[] requiredFlags = new string[0];
        public string prompt = "操作终端";
        public string lockedHint = "请先接通前面的设备";
        public float lowStrength = 0.28f;
        public float highStrength = 1.8f;
        public bool toggleFlag;

        public override bool CanInteract()
        {
            var session = PuzzleSession.Instance;
            return base.CanInteract() && session && !session.IsRespawning && !session.InputBlocked;
        }

        protected override void OnInteract()
        {
            var session = PuzzleSession.Instance;
            if (!session) return;
            if (requiredFlags != null)
                foreach (string requirement in requiredFlags)
                    if (!string.IsNullOrEmpty(requirement) && !session.Solved(requirement))
                    {

                        return;
                    }
            var environment = session.PlayerBody ? session.PlayerBody.GetComponent<PuzzlePlayerEnvironment>() : null;
            switch (action)
            {
                case PuzzleTerminalAction.SetLowGravity:
                    if (environment) environment.SetStrength(lowStrength);

                    break;
                case PuzzleTerminalAction.SetHighGravity:
                    if (environment) environment.SetStrength(highStrength);

                    break;
                case PuzzleTerminalAction.SolveFlag:
                    if (!string.IsNullOrEmpty(flag))
                        session.SetFlag(flag, !toggleFlag || !session.Solved(flag));

                    break;
                case PuzzleTerminalAction.Complete:
                    session.Complete();
                    break;
            }
        }
    }
}
