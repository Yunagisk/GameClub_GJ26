using GameClub.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GameClub.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class StartGameController : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "TestScene";
        [SerializeField] private BlinkText promptBlink;
        [SerializeField] private Font openingFont;
        [SerializeField, TextArea(3, 8)] private string openingText =
            "人类把这颗行星叫作地球。\n\n" +
            "我从轨道呼叫了七次，地表没有回应。勘查已经确认，人类灭绝了。但在一片废墟下，一座地下基地还在发出微弱的信号。\n\n" +
            "我要找到他们留下的记录，查明他们为何消失，再把答案发回母船。";
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [SerializeField, Min(0f)] private float minimumBlackDuration = 0.15f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.45f;

        private Button startButton;
        private Button continueButton;
        public bool IsStarting { get; private set; }

        private void Awake() => startButton = GetComponent<Button>();
        private void OnEnable()
        {
            if (!startButton) startButton = GetComponent<Button>();
            startButton.onClick.AddListener(StartGame);
            if (continueButton) continueButton.onClick.AddListener(LoadDestination);
        }
        private void OnDisable()
        {
            if (startButton) startButton.onClick.RemoveListener(StartGame);
            if (continueButton) continueButton.onClick.RemoveListener(LoadDestination);
        }

        public void StartGame()
        {
            if (IsStarting || !startButton.interactable) return;
            if (openingFont && !string.IsNullOrWhiteSpace(openingText) && TryShowOpening()) return;
            LoadDestination();
        }

        private void LoadDestination()
        {
            if (IsStarting) return;
            if (!LevelSession.Instance || !LevelSession.Instance.TryStartGame(destinationScene,
                    fadeOutDuration, fadeInDuration, minimumBlackDuration)) return;

            IsStarting = true;
            startButton.interactable = false;
            if (continueButton) continueButton.interactable = false;
            if (promptBlink) promptBlink.enabled = false;
        }

        private bool TryShowOpening()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (!canvas) return false;

            var shade = CreateElement("Opening briefing", canvas.transform, Vector2.zero, Vector2.one, typeof(Image));
            shade.GetComponent<Image>().color = new Color(.012f, .026f, .042f, .93f);
            var panel = CreateElement("Briefing panel", shade.transform, new Vector2(.13f, .11f), new Vector2(.87f, .89f), typeof(Image));
            panel.GetComponent<Image>().color = new Color(.037f, .071f, .097f, .97f);

            var edge = CreateElement("Accent", panel.transform, new Vector2(.055f, .82f), new Vector2(.945f, .826f), typeof(Image));
            edge.GetComponent<Image>().color = new Color(.28f, .66f, .73f, .82f);
            var title = AddLabel(panel.transform, "Heading", "调查记录 01", new Vector2(.055f, .842f), new Vector2(.945f, .948f), 50, TextAnchor.MiddleLeft);
            title.color = new Color(.50f, .83f, .85f);
            var body = AddLabel(panel.transform, "Opening text", openingText, new Vector2(.055f, .22f), new Vector2(.945f, .773f), 42, TextAnchor.MiddleLeft);
            body.lineSpacing = 1.28f;

            var proceed = CreateElement("Continue", panel.transform, new Vector2(.32f, .065f), new Vector2(.68f, .183f), typeof(Image), typeof(Button));
            var graphic = proceed.GetComponent<Image>();
            graphic.color = new Color(.14f, .32f, .38f);
            continueButton = proceed.GetComponent<Button>();
            continueButton.targetGraphic = graphic;
            continueButton.onClick.AddListener(LoadDestination);
            AddLabel(proceed.transform, "Label", "进入基地", Vector2.zero, Vector2.one, 36, TextAnchor.MiddleCenter);

            startButton.interactable = false;
            if (promptBlink) promptBlink.gameObject.SetActive(false);
            continueButton.Select();
            return true;
        }

        private Text AddLabel(Transform parent, string name, string value, Vector2 min, Vector2 max, int fontSize, TextAnchor alignment)
        {
            var label = CreateElement(name, parent, min, max, typeof(Text)).GetComponent<Text>();
            label.font = openingFont;
            label.text = value;
            label.color = new Color(.89f, .93f, .95f);
            label.alignment = alignment;
            label.fontSize = fontSize;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = fontSize / 2;
            label.resizeTextMaxSize = fontSize;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform CreateElement(string name, Transform parent, Vector2 min, Vector2 max, params System.Type[] components)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.layer = parent.gameObject.layer;
            var rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            foreach (var component in components) item.AddComponent(component);
            return rect;
        }
    }
}
