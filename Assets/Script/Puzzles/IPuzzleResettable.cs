namespace GameClub.Puzzles
{
    public interface IPuzzleResettable
    {
        void CaptureInitialState();
        void ResetPuzzleState();
    }
}
