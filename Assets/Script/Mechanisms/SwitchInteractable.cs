using System;
using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(SaveableEntity))]
    public sealed class SwitchInteractable : InteractableBase
    {
        [SerializeField] private DoorController door;
        [SerializeField] private bool isOn;
        [SerializeField] private SpriteRenderer visual;

        [Serializable]
        private sealed class State : InteractionState { public bool on; }

        private void Start()
        {
            if (!door) Debug.LogError("Switch requires a door reference.", this);
            RefreshState();
        }

        protected override void OnInteract()
        {
            isOn = !isOn;
            RefreshState();
        }

        public override string CaptureState()
        {
            var state = new State { on = isOn };
            CaptureInteraction(state);
            return JsonUtility.ToJson(state);
        }

        public override void RestoreState(string data)
        {
            var state = JsonUtility.FromJson<State>(data);
            RestoreInteraction(state);
            isOn = state.on;
        }

        public override void RefreshState()
        {
            if (door) door.SetOpen(isOn);
            if (!visual) visual = GetComponent<SpriteRenderer>();
            if (visual) visual.color = isOn ? Color.green : Color.red;
        }
    }
}
