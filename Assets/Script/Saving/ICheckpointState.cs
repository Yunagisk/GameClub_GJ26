namespace GameClub.Player
{
    // Each component owns its data. RefreshState runs after every object has been restored.
    public interface ICheckpointState
    {
        string CaptureState();
        void RestoreState(string data);
        void RefreshState();
    }
}
