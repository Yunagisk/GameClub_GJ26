using UnityEngine;
using UnityEngine.UI;

namespace GameClub.Player
{
    public sealed class TransitionOverlay : MonoBehaviour
    {
        private CanvasGroup group;

        public float Alpha
        {
            get { Initialize(); return group.alpha; }
            set { Initialize(); group.alpha = Mathf.Clamp01(value); }
        }

        public bool BlocksInput
        {
            get { Initialize(); return group.blocksRaycasts; }
            set { Initialize(); group.blocksRaycasts = value; }
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (group) return;
            var root = new GameObject("Transition Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup), typeof(GraphicRaycaster));
            root.layer = 5;
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var panel = new GameObject("Black Screen", typeof(RectTransform), typeof(Image));
            panel.layer = 5;
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = Color.black;
        }
    }
}
