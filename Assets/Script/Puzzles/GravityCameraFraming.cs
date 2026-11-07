using Cinemachine;
using UnityEngine;

namespace GameClub.Puzzles
{
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    public sealed class GravityCameraFraming : MonoBehaviour
    {
        public Transform player;
        public Rect pendulumBounds = new Rect(26f, -37f, 29f, 17f);
        public Vector2 pendulumFocus = new Vector2(40f, -29f);
        public Vector2 maximumOffset = new Vector2(3.5f, 3.8f);
        [Min(0.01f)] public float offsetSmoothTime = 0.22f;

        private CinemachineVirtualCamera view;
        private CinemachineFramingTransposer framing;
        private Vector2 currentOffset;
        private Vector2 offsetVelocity;
        private Vector2 lastPlayerPosition;
        private bool sampledPosition;

        private void Awake() => Cache();
        private void OnEnable() => Cache();

        private void Cache()
        {
            if (!view) view = GetComponent<CinemachineVirtualCamera>();
            if (view) framing = view.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (!player && view) player = view.Follow;
        }

        private void LateUpdate()
        {
            if (!framing || !player) { Cache(); if (!framing || !player) return; }
            Vector2 position = player.position;
            Vector2 desired = Vector2.zero;
            if (pendulumBounds.Contains(position))
                desired = new Vector2(
                    Mathf.Clamp(pendulumFocus.x - position.x, -Mathf.Abs(maximumOffset.x), Mathf.Abs(maximumOffset.x)),
                    Mathf.Clamp(pendulumFocus.y - position.y, -Mathf.Abs(maximumOffset.y), Mathf.Abs(maximumOffset.y)));

            // Teleports and checkpoint revival should not pan through the old room.
            if (!sampledPosition || (position - lastPlayerPosition).sqrMagnitude > 64f)
            {
                currentOffset = desired;
                offsetVelocity = Vector2.zero;
                if (sampledPosition && view) view.PreviousStateIsValid = false;
            }
            else
                currentOffset = Vector2.SmoothDamp(currentOffset, desired, ref offsetVelocity,
                    Mathf.Max(0.01f, offsetSmoothTime), Mathf.Infinity, Time.deltaTime);
            // Cinemachine applies this offset in target-local space; keep the desired framing in world space even when gravity flips the player.
            framing.m_TrackedObjectOffset = Quaternion.Inverse(player.rotation) * new Vector3(currentOffset.x, currentOffset.y, 0f);
            sampledPosition = true;
            lastPlayerPosition = position;
        }

        private void OnDisable()
        {
            if (framing) framing.m_TrackedObjectOffset = Vector3.zero;
            currentOffset = offsetVelocity = Vector2.zero;
            sampledPosition = false;
        }
    }
}