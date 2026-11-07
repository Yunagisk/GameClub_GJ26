using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chuangyou.Narrative
{
    /// <summary>
    /// 剧情日志的显示层。挂在 Canvas 根物体上，由 NarrativeLogTerminal 调用。
    /// 不依赖 TextMeshPro，使用内置 uGUI Text，方便直接导入任意 Unity 2021+ 工程。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NarrativeTerminalUI : MonoBehaviour
    {
        public static NarrativeTerminalUI Instance { get; private set; }

        [Header("交互提示")]
        [SerializeField] private CanvasGroup promptGroup;
        [SerializeField] private Text promptLabel;

        [Header("终端窗口")]
        [SerializeField] private CanvasGroup terminalGroup;
        [SerializeField] private GameObject terminalRoot;
        [SerializeField] private RectTransform windowRect;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text subtitleLabel;
        [SerializeField] private Text entryIndexLabel;
        [SerializeField] private Text entryTitleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Text pageLabel;
        [SerializeField] private ScrollRect bodyScroll;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("表现")]
        [SerializeField] private float fadeSpeed = 14f;
        [SerializeField] private bool manageCursor = false;

        private readonly List<NarrativeLogEntry> displayEntries = new List<NarrativeLogEntry>();
        private int pageIndex;
        private bool isOpen;
        private bool promptVisible;
        private Coroutine resetScrollRoutine;
        private Color defaultEntryAccent = new Color(0.89f, 0.69f, 0.37f, 1f);

        private bool cursorCached;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLockState;

        public bool IsOpen
        {
            get { return isOpen; }
        }

        public event Action CloseRequested;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[NarrativeTerminalUI] 场景里出现了多个日志 UI，只会使用第一个。", this);
            }
            else
            {
                Instance = this;
            }

            if (entryIndexLabel != null)
            {
                defaultEntryAccent = entryIndexLabel.color;
            }

            if (promptGroup != null)
            {
                promptGroup.alpha = 0f;
                promptGroup.interactable = false;
                promptGroup.blocksRaycasts = false;
            }

            if (terminalRoot != null)
            {
                terminalRoot.SetActive(false);
            }

            if (terminalGroup != null)
            {
                terminalGroup.alpha = 0f;
                terminalGroup.interactable = false;
                terminalGroup.blocksRaycasts = false;
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(RequestClose);
            }

            if (previousButton != null)
            {
                previousButton.onClick.AddListener(PreviousPage);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(NextPage);
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (promptGroup != null)
            {
                float promptTarget = promptVisible && !isOpen ? 1f : 0f;
                promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, promptTarget, fadeSpeed * 2.2f * dt);
            }

            if (terminalGroup == null || terminalRoot == null)
            {
                return;
            }

            if (isOpen)
            {
                terminalGroup.alpha = Mathf.MoveTowards(terminalGroup.alpha, 1f, fadeSpeed * dt);
                terminalGroup.interactable = terminalGroup.alpha > 0.8f;
                terminalGroup.blocksRaycasts = terminalGroup.alpha > 0.8f;

                if (windowRect != null)
                {
                    windowRect.localScale = Vector3.Lerp(
                        windowRect.localScale,
                        Vector3.one,
                        1f - Mathf.Exp(-18f * dt));
                }
            }
            else
            {
                terminalGroup.alpha = Mathf.MoveTowards(terminalGroup.alpha, 0f, fadeSpeed * 1.6f * dt);
                terminalGroup.interactable = false;
                terminalGroup.blocksRaycasts = false;

                if (windowRect != null)
                {
                    windowRect.localScale = Vector3.Lerp(
                        windowRect.localScale,
                        new Vector3(0.985f, 0.985f, 1f),
                        1f - Mathf.Exp(-18f * dt));
                }

                if (terminalGroup.alpha <= 0.001f && terminalRoot.activeSelf)
                {
                    terminalRoot.SetActive(false);
                }
            }
        }

        public void ShowPrompt(string text)
        {
            if (isOpen)
            {
                return;
            }

            promptVisible = true;
            if (promptLabel != null && !string.IsNullOrEmpty(text))
            {
                promptLabel.text = text;
            }
        }

        public void HidePrompt()
        {
            promptVisible = false;
        }

        public void Open(
            NarrativeLogAsset asset,
            IReadOnlyList<NarrativeLogEntry> fallbackEntries,
            string fallbackTitle,
            string fallbackSubtitle)
        {
            BuildDisplayEntries(asset, fallbackEntries);

            pageIndex = 0;
            isOpen = true;
            promptVisible = false;

            if (titleLabel != null)
            {
                titleLabel.text = asset != null ? asset.TerminalTitle : fallbackTitle;
            }

            if (subtitleLabel != null)
            {
                string subtitle = asset != null ? asset.Subtitle : fallbackSubtitle;
                subtitleLabel.text = subtitle;
                subtitleLabel.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            }

            if (terminalRoot != null)
            {
                terminalRoot.SetActive(true);
            }

            if (terminalGroup != null)
            {
                terminalGroup.alpha = 0f;
                terminalGroup.interactable = false;
                terminalGroup.blocksRaycasts = false;
            }

            if (windowRect != null)
            {
                windowRect.localScale = new Vector3(0.975f, 0.975f, 1f);
            }

            ApplyCursor(true);
            RefreshPage();
        }

        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            if (terminalGroup != null)
            {
                terminalGroup.interactable = false;
                terminalGroup.blocksRaycasts = false;
            }

            ApplyCursor(false);
        }

        public void NextPage()
        {
            if (!isOpen || displayEntries.Count <= 1)
            {
                return;
            }

            pageIndex = (pageIndex + 1) % displayEntries.Count;
            RefreshPage();
        }

        public void PreviousPage()
        {
            if (!isOpen || displayEntries.Count <= 1)
            {
                return;
            }

            pageIndex = (pageIndex - 1 + displayEntries.Count) % displayEntries.Count;
            RefreshPage();
        }

        /// <summary>按归一化位置滚动正文。正数向上。</summary>
        public void Scroll(float normalizedDelta)
        {
            if (bodyScroll == null)
            {
                return;
            }

            bodyScroll.verticalNormalizedPosition = Mathf.Clamp01(
                bodyScroll.verticalNormalizedPosition + normalizedDelta);
        }

        public void RequestClose()
        {
            if (CloseRequested != null)
            {
                CloseRequested.Invoke();
            }
        }

        private void BuildDisplayEntries(
            NarrativeLogAsset asset,
            IReadOnlyList<NarrativeLogEntry> fallbackEntries)
        {
            displayEntries.Clear();

            IReadOnlyList<NarrativeLogEntry> source = null;
            if (asset != null && asset.Entries != null && asset.Entries.Count > 0)
            {
                source = asset.Entries;
            }
            else if (fallbackEntries != null && fallbackEntries.Count > 0)
            {
                source = fallbackEntries;
            }

            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    if (source[i] != null)
                    {
                        displayEntries.Add(source[i]);
                    }
                }
            }

            if (displayEntries.Count == 0)
            {
                displayEntries.Add(new NarrativeLogEntry(
                    "无记录",
                    "ARCHIVE // EMPTY",
                    "终端里还没有可读取的日志。\n\n在 Inspector 中为日志资产添加条目后，这里会自动显示。"));
            }
        }

        private void RefreshPage()
        {
            if (displayEntries.Count == 0)
            {
                return;
            }

            pageIndex = Mathf.Clamp(pageIndex, 0, displayEntries.Count - 1);
            NarrativeLogEntry entry = displayEntries[pageIndex];

            if (entryIndexLabel != null)
            {
                entryIndexLabel.text = string.IsNullOrEmpty(entry.timestamp)
                    ? "LOG " + (pageIndex + 1).ToString("00")
                    : entry.timestamp;
                entryIndexLabel.color = entry.accent.a <= 0.01f ? defaultEntryAccent : entry.accent;
            }

            if (entryTitleLabel != null)
            {
                entryTitleLabel.text = entry.title;
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = entry.body;
            }

            if (pageLabel != null)
            {
                pageLabel.text = (pageIndex + 1).ToString("00") + " / " + displayEntries.Count.ToString("00");
            }

            bool hasMultiplePages = displayEntries.Count > 1;
            if (previousButton != null)
            {
                previousButton.interactable = hasMultiplePages;
            }

            if (nextButton != null)
            {
                nextButton.interactable = hasMultiplePages;
            }

            if (bodyScroll != null && bodyScroll.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(bodyScroll.content);
            }

            Canvas.ForceUpdateCanvases();

            if (resetScrollRoutine != null)
            {
                StopCoroutine(resetScrollRoutine);
            }

            resetScrollRoutine = StartCoroutine(ResetScrollNextFrame());
        }

        private IEnumerator ResetScrollNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (bodyScroll != null)
            {
                bodyScroll.verticalNormalizedPosition = 1f;
            }

            resetScrollRoutine = null;
        }

        private void ApplyCursor(bool open)
        {
            if (!manageCursor)
            {
                return;
            }

            if (open)
            {
                if (!cursorCached)
                {
                    previousCursorVisible = Cursor.visible;
                    previousCursorLockState = Cursor.lockState;
                    cursorCached = true;
                }

                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else if (cursorCached)
            {
                Cursor.visible = previousCursorVisible;
                Cursor.lockState = previousCursorLockState;
                cursorCached = false;
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(RequestClose);
            }

            if (previousButton != null)
            {
                previousButton.onClick.RemoveListener(PreviousPage);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(NextPage);
            }

            ApplyCursor(false);

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
