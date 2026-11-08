using UnityEngine;

namespace GameClub.Player
{
    public enum PlayerLifeState { Playing, Dying, Restoring, Reviving }
    public enum PlayerMotionState { Idle, Running, Rising, Falling }

    [DisallowMultipleComponent, RequireComponent(typeof(PlayerController2D))]
    public sealed class PlayerLifecycle : MonoBehaviour
    {
        [Min(0f)] public float deathDuration = 0.3f;
        [Min(0f)] public float reviveDuration = 0.6f;
        public PlayerLifeState State { get; private set; } = PlayerLifeState.Restoring;
        public bool CanControl => State == PlayerLifeState.Playing;
        private Rigidbody2D body;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            SetState(PlayerLifeState.Restoring);
        }

        public void RequestDeath()
        {
            if (!CanControl || !LevelSession.Instance || !LevelSession.Instance.RequestDeath(this)) return;
            SetState(PlayerLifeState.Dying);
        }

        public void SetState(PlayerLifeState state)
        {
            State = state;
            bool playing = CanControl;
            GetComponent<PlayerInputReader>().SetGameplayEnabled(playing);
            GetComponent<PlayerController2D>().enabled = playing;
            GetComponent<PlayerInteractionPicker>().enabled = playing;
            if (!playing)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            body.simulated = playing;
        }

        public void PlaceForRevival(Vector3 position) => PlaceAt(position, PlayerLifeState.Reviving);

        public void PlaceForSceneEntry(Vector3 position) => PlaceAt(position, PlayerLifeState.Restoring);

        private void PlaceAt(Vector3 position, PlayerLifeState state)
        {
            transform.position = position;
            body.position = position;
            GetComponent<GravityBody2D>().ResetForRespawn();
            GetComponent<PlayerDash>().ResetForRespawn();
            GetComponent<PlayerHorizontalMotor>().ResetForRespawn();
            GetComponent<PlayerGroundProbe>().LeaveGround();
            SetState(state);
        }
    }
}
