using System;
using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(SaveableEntity))]
    public sealed class CheckpointInteractable : InteractableBase
    {
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private SpriteRenderer visual;
        private bool activated;
        public string Id => GetComponent<SaveableEntity>().Id;

        [Serializable]
        private sealed class State : InteractionState { public bool activated; }

        public override bool CanInteract() =>
            base.CanInteract() && !activated && LevelSession.Instance && LevelSession.Instance.CanSave;

        private void Start() => RefreshState();

        protected override void OnInteract()
        {
            if (!respawnPoint)
            {
                Debug.LogError("Checkpoint requires a safe RespawnPoint.", this);
                return;
            }
            activated = true;
            if (!LevelSession.Instance.SaveCheckpoint(Id, respawnPoint.position)) activated = false;
            RefreshState();
        }

        public override string CaptureState()
        {
            var state = new State { activated = activated };
            CaptureInteraction(state);
            return JsonUtility.ToJson(state);
        }

        public override void RestoreState(string data)
        {
            var state = JsonUtility.FromJson<State>(data);
            activated = state.activated;
            RestoreInteraction(state);
        }

        public override void RefreshState()
        {
            if (!visual) visual = GetComponent<SpriteRenderer>();
            bool current = LevelSession.Instance && LevelSession.Instance.CurrentCheckpointId == Id;
            if (visual) visual.color = current ? Color.cyan : activated ? Color.gray : Color.yellow;
        }
    }
}
