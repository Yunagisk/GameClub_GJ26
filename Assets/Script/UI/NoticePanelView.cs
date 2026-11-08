using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GameClub.UI
{
    // Reusable presentation only. The caller owns pause, progression and interaction rules.
    [DisallowMultipleComponent]
    public sealed class NoticePanelView : MonoBehaviour
    {
        [SerializeField] private Text heading;
        [SerializeField] private Text sourceLabel;
        [SerializeField] private Text body;
        [SerializeField] private Text confirmLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private ScrollRect bodyScroll;
        private Action onConfirm;

        public Button ConfirmButton => confirmButton;
        public ScrollRect BodyScroll => bodyScroll;

        private void Awake() => confirmButton.onClick.AddListener(Confirm);
        private void OnDestroy()
        {
            if (confirmButton) confirmButton.onClick.RemoveListener(Confirm);
        }

        public void Show(string title, string source, string text, string buttonText, Action confirm)
        {
            EnsureEventSystem();
            heading.text = title;
            sourceLabel.text = source;
            body.text = text;
            confirmLabel.text = buttonText;
            onConfirm = confirm;
            gameObject.SetActive(true);
            confirmButton.interactable = true;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bodyScroll.content);
            bodyScroll.StopMovement();
            bodyScroll.verticalNormalizedPosition = 1f;
        }

        public void SetConfirmationEnabled(bool value)
        {
            confirmButton.interactable = value;
            if (value) confirmButton.Select();
        }

        public void Hide()
        {
            onConfirm = null;
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == confirmButton.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            gameObject.SetActive(false);
        }

        private void Confirm()
        {
            if (!confirmButton.interactable || onConfirm == null) return;
            confirmButton.interactable = false;
            onConfirm.Invoke();
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            // Scene-owned, independent from this view so it survives Hide/reopening.
            var events = new GameObject("UI EventSystem", typeof(EventSystem));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(events, gameObject.scene);
            events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
