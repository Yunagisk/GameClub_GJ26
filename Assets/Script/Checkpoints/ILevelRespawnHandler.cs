namespace GameClub.Player
{
    // Optional room-based restoration adapter. Snapshot levels keep their original path.
    public interface ILevelRespawnHandler
    {
        string CurrentCheckpointId { get; }
        void RestoreCheckpoint(PlayerLifecycle player);
    }
}
