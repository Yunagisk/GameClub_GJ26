using UnityEngine;

namespace GameClub.Player
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class DeathZone : MonoBehaviour
    {
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void OnTriggerEnter2D(Collider2D other) => Kill(other);
        private void OnTriggerStay2D(Collider2D other) => Kill(other);

        private void Kill(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (body && body.TryGetComponent<PlayerLifecycle>(out var player)) player.RequestDeath();
        }
    }
}
