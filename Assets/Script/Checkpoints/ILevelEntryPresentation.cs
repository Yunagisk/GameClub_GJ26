using System.Collections;

namespace GameClub.Player
{
    // Runs only on fresh entry, before LevelSession releases gameplay control.
    public interface ILevelEntryPresentation
    {
        bool BeginPresentation();
        IEnumerator WaitForDismissal();
    }
}
