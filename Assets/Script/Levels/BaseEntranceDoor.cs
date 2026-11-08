using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent]
    public sealed class BaseEntranceDoor : InteractableBase
    {
        [SerializeField] private string destinationScene = "TestScene";

        public override bool CanInteract() =>
            base.CanInteract() && LevelSession.Instance && LevelSession.Instance.CanTravel;

        protected override void OnInteract() => LevelSession.Instance.TryRequestTravel(destinationScene);
    }
}