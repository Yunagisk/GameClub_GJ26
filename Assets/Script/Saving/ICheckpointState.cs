namespace GameClub.Player
{

    public interface ICheckpointState
    {
        string CaptureState();
        void RestoreState(string data);
        void RefreshState();
    }
}
