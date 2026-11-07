using System;
using GameClub.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameClub.Puzzles
{
    public enum StoryLogKind { Notice, Maintenance, Terminal, Transmission }

    [DisallowMultipleComponent]
    public sealed class StoryLogInteractable : InteractableBase
    {
        public StoryLogKind kind;
        public string title = "现场记录";
        public string source;
        [TextArea(4, 16)] public string body;
        public string[] requiredFlags = new string[0];
        [TextArea(2, 8)] public string lockedBody;
        public string transmitLabel = "发回母船";
        public string followUpTitle;
        [TextArea(3, 12)] public string followUpBody;

        private PuzzleSession readerSession;
        private bool transmitted;
        private int openedFrame;
        private Vector2 scroll;
        private GUIStyle titleStyle, sourceStyle, textStyle, detailStyle, buttonStyle;

        public override bool CanInteract()
        {
            var session = PuzzleSession.Instance;
            return session && !session.InputBlocked && base.CanInteract();
        }

        protected override void OnInteract()
        {
            var session = PuzzleSession.Instance;
            if (!session || !session.BeginStory(this)) return;
            readerSession = session;
            openedFrame = Time.frameCount;
            scroll = Vector2.zero;
        }

        protected override void OnDisable()
        {
            Close(false);
            base.OnDisable();
        }

        private void Update()
        {
            if (!readerSession) return;
            if (!readerSession.IsReading(this) || readerSession.IsRespawning)
            {
                Close(false);
                return;
            }

            var keyboard = Keyboard.current;
            // The F press that opens the reader is still visible during this frame.
            if (keyboard == null || openedFrame == Time.frameCount) return;
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
                Close(true);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (CanTransmit()) Transmit();
                else if (transmitted) Close(true);
            }
        }

        private bool RequirementsMet()
        {
            if (!readerSession) return false;
            foreach (string flag in requiredFlags ?? Array.Empty<string>())
                if (!readerSession.Solved(flag)) return false;
            return true;
        }

        private bool CanTransmit() => kind == StoryLogKind.Transmission && !transmitted && RequirementsMet();

        private void Transmit()
        {
            if (!CanTransmit()) return;
            transmitted = true;
            scroll = Vector2.zero;
        }

        private void Close(bool finish)
        {
            var session = readerSession;
            if (!session) return;
            bool complete = finish && session.IsReading(this) && !session.IsRespawning && !session.Completed
                && kind == StoryLogKind.Transmission && transmitted && RequirementsMet();
            readerSession = null;
            session.EndStory(this);
            if (complete) session.CompleteTransmission();
        }

        [Serializable] private sealed class SavedState : InteractionState { public bool transmitted; }

        public override string CaptureState()
        {
            var state = new SavedState { transmitted = transmitted };
            CaptureInteraction(state);
            return JsonUtility.ToJson(state);
        }

        public override void RestoreState(string data)
        {
            var state = JsonUtility.FromJson<SavedState>(data);
            if (state == null) return;
            RestoreInteraction(state);
            transmitted = state.transmitted;
        }

        private void OnGUI()
        {
            if (!readerSession || !readerSession.IsReading(this) || Screen.width < 150 || Screen.height < 120) return;
            EnsureStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            try
            {
                GUI.depth = -120;
                GUI.color = Color.white;
                float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1050f, Screen.height / 620f), 0.62f, 1.35f);
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                float width = Screen.width / scale, height = Screen.height / scale;
                float panelWidth = Mathf.Min(820f, width - 24f), panelHeight = Mathf.Min(538f, height - 24f);
                DrawReader(new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight), width, height);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                GUI.depth = previousDepth;
            }
        }

        private void DrawReader(Rect panel, float width, float height)
        {
            DescribeKind(out string category, out Color accent);
            bool receipt = kind == StoryLogKind.Transmission && transmitted;
            bool unlocked = RequirementsMet();
            string displayTitle = receipt && !string.IsNullOrEmpty(followUpTitle) ? followUpTitle : title;
            string displayText = receipt ? followUpBody : unlocked ? body : lockedBody;
            if (string.IsNullOrWhiteSpace(displayText))
                displayText = receipt ? "母船已确认收到事故档案。" : "记录暂不可读取。";

            Fill(new Rect(0f, 0f, width, height), new Color(0.01f, 0.02f, 0.04f, 0.75f));
            Fill(panel, new Color(0.17f, 0.24f, 0.29f, 1f));
            Fill(new Rect(panel.x + 1f, panel.y + 1f, panel.width - 2f, panel.height - 2f), new Color(0.06f, 0.09f, 0.12f, 1f));
            Fill(new Rect(panel.x + 1f, panel.y + 1f, panel.width - 2f, 4f), accent);

            const float inset = 26f;
            float x = panel.x + inset, textWidth = panel.width - inset * 2f;
            float y = panel.y + 15f;
            sourceStyle.normal.textColor = accent;
            GUI.Label(new Rect(x, y, textWidth, 23f), receipt ? "对外通信 / 已送达" : category, sourceStyle);
            y += 27f;
            float titleHeight = Mathf.Max(34f, titleStyle.CalcHeight(new GUIContent(displayTitle), textWidth));
            GUI.Label(new Rect(x, y, textWidth, titleHeight), displayTitle, titleStyle);
            y += titleHeight + 3f;
            if (!string.IsNullOrWhiteSpace(source))
            {
                float sourceHeight = Mathf.Max(22f, detailStyle.CalcHeight(new GUIContent(source), textWidth));
                GUI.Label(new Rect(x, y, textWidth, sourceHeight), source, detailStyle);
                y += sourceHeight;
            }
            y += 9f;
            Fill(new Rect(x, y, textWidth, 1f), new Color(0.22f, 0.31f, 0.35f, 1f));

            float footerY = panel.yMax - 53f;
            var textViewport = new Rect(x, y + 14f, textWidth, Mathf.Max(1f, footerY - y - 24f));
            float bodyWidth = Mathf.Max(1f, textWidth - 18f);
            var content = new GUIContent(displayText);
            float bodyHeight = textStyle.CalcHeight(content, bodyWidth) + 8f;
            scroll = GUI.BeginScrollView(textViewport, scroll, new Rect(0, 0, bodyWidth, bodyHeight), false, bodyHeight > textViewport.height);
            GUI.Label(new Rect(0, 0, bodyWidth, bodyHeight), content, textStyle);
            GUI.EndScrollView();

            Fill(new Rect(x, footerY - 8f, textWidth, 1f), new Color(0.22f, 0.31f, 0.35f, 1f));
            float closeWidth = Mathf.Min(136f, textWidth * 0.4f);
            var closeButton = new Rect(panel.xMax - inset - closeWidth, footerY, closeWidth, 36f);
            if (CanTransmit())
            {
                float sendWidth = Mathf.Min(190f, textWidth - closeWidth - 8f);
                var sendButton = new Rect(closeButton.x - sendWidth - 8f, footerY, sendWidth, 36f);
                if (GUI.Button(sendButton, string.IsNullOrWhiteSpace(transmitLabel) ? "发回母船" : transmitLabel, buttonStyle)) Transmit();
            }
            else if (textWidth >= 375f)
                GUI.Label(new Rect(x, footerY + 5f, textWidth - closeWidth - 8f, 26f), "F / Esc 关闭", detailStyle);
            if (GUI.Button(closeButton, receipt ? "结束阅读" : "关闭", buttonStyle)) Close(true);
        }

        private void DescribeKind(out string category, out Color accent)
        {
            switch (kind)
            {
                case StoryLogKind.Notice:
                    category = "现场告示";
                    accent = new Color(0.89f, 0.72f, 0.42f);
                    break;
                case StoryLogKind.Maintenance:
                    category = "检修手记";
                    accent = new Color(0.95f, 0.66f, 0.36f);
                    break;
                case StoryLogKind.Transmission:
                    category = "对外通信";
                    accent = new Color(0.47f, 0.9f, 0.78f);
                    break;
                default:
                    category = "设备档案";
                    accent = new Color(0.45f, 0.78f, 0.95f);
                    break;
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            Font font = readerSession.GetReadingFont();
            titleStyle = Label(font, 27, new Color(0.95f, 0.98f, 1f), FontStyle.Bold);
            sourceStyle = Label(font, 15, Color.white, FontStyle.Bold);
            textStyle = Label(font, 21, new Color(0.88f, 0.93f, 0.95f));
            detailStyle = Label(font, 15, new Color(0.67f, 0.77f, 0.82f));
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17 };
        }

        private static GUIStyle Label(Font font, int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = size, fontStyle = weight, wordWrap = true,
                padding = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = color;
            return style;
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
