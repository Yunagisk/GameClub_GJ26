using UnityEngine;

namespace GameClub.Player
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class LevelExit : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "CheckpointTestB";

        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (body && body.TryGetComponent<PlayerLifecycle>(out var player) && player.CanControl)
                LevelSession.Instance.RequestTravel(destinationScene);
        }

    }
}
