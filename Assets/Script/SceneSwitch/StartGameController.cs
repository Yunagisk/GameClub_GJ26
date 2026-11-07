using GameClub.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GameClub.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class StartGameController : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "Assets/Scenes/SceneA.unity";
        [SerializeField] private BlinkText promptBlink;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [SerializeField, Min(0f)] private float minimumBlackDuration = 0.15f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.45f;

        private Button startButton;
        public bool IsStarting { get; private set; }

        private void Awake() => startButton = GetComponent<Button>();
        private void OnEnable()
        {
            if (!startButton) startButton = GetComponent<Button>();
            startButton.onClick.AddListener(StartGame);
        }
        private void OnDisable()
        {
            if (startButton) startButton.onClick.RemoveListener(StartGame);
        }

        public void StartGame()
        {
            if (IsStarting || !startButton.interactable) return;
            if (!LevelSession.Instance || !LevelSession.Instance.TryStartGame(destinationScene,
                    fadeOutDuration, fadeInDuration, minimumBlackDuration)) return;

            IsStarting = true;
            startButton.interactable = false;
            if (promptBlink) promptBlink.enabled = false;
        }
    }
}
