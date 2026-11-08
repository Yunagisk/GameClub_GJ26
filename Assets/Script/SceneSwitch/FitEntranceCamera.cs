using UnityEngine;

namespace GameClub.UI
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class FitEntranceCamera : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer background;

        private Camera view;

        private void OnEnable()
        {
            view = GetComponent<Camera>();
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (!background || view.aspect <= 0f) return;
            Vector3 halfSize = background.bounds.extents;
            float size = Mathf.Max(halfSize.y, halfSize.x / view.aspect);
            if (!Mathf.Approximately(view.orthographicSize, size)) view.orthographicSize = size;
        }
    }
}