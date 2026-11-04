using System;
using UnityEngine;

namespace GameClub.Player
{
    [DisallowMultipleComponent]
    public sealed class SaveableEntity : MonoBehaviour
    {
        [SerializeField] private string persistentId;
        public string Id => persistentId;

        private void Reset() => persistentId = Guid.NewGuid().ToString("N");

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(persistentId)) persistentId = Guid.NewGuid().ToString("N");
        }

        [ContextMenu("Generate new save ID for duplicate")]
        private void GenerateId() => persistentId = Guid.NewGuid().ToString("N");
#endif

        public EntitySnapshot Capture()
        {
            var data = new EntitySnapshot
            {
                id = Id, active = gameObject.activeSelf,
                position = transform.localPosition, rotation = transform.localRotation, scale = transform.localScale
            };
            var body = GetComponent<Rigidbody2D>();
            if (body)
            {
                data.velocity = body.velocity;
                data.angularVelocity = body.angularVelocity;
                data.simulated = body.simulated;
            }
            foreach (var component in GetComponents<MonoBehaviour>())
            {
                if (!(component is ICheckpointState state)) continue;
                string type = component.GetType().FullName;
                if (data.componentTypes.Contains(type))
                    throw new InvalidOperationException("Duplicate snapshot component on " + name);
                data.componentTypes.Add(type);
                data.componentStates.Add(state.CaptureState());
            }
            return data;
        }

        public void Restore(EntitySnapshot data)
        {
            transform.localPosition = data.position;
            transform.localRotation = data.rotation;
            transform.localScale = data.scale;
            var body = GetComponent<Rigidbody2D>();
            if (body)
            {
                body.position = transform.position;
                body.rotation = transform.eulerAngles.z;
                body.velocity = data.velocity;
                body.angularVelocity = data.angularVelocity;
                body.simulated = data.simulated;
            }
            foreach (var component in GetComponents<MonoBehaviour>())
            {
                if (!(component is ICheckpointState state)) continue;
                int index = data.componentTypes.IndexOf(component.GetType().FullName);
                if (index < 0) throw new InvalidOperationException("Missing snapshot component on " + name);
                state.RestoreState(data.componentStates[index]);
            }
        }

        public void RefreshState()
        {
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is ICheckpointState state) state.RefreshState();
        }
    }
}
