using System.Collections;
using GameClub.Player;
using UnityEngine;

namespace GameClub.UI
{
    [DisallowMultipleComponent]
    public sealed class OpeningBriefingUI : MonoBehaviour, ILevelEntryPresentation
    {
        [SerializeField] private NoticePanelView panelPrefab;
        [SerializeField, TextArea(3, 8)] private string openingText =
            "人类把这颗行星叫作地球。\n\n" +
            "我从轨道呼叫了七次，地表没有回应。勘查已经确认，人类灭绝了。但在一片废墟下，一座地下基地还在发出微弱的信号。\n\n" +
            "我要找到他们留下的记录，查明他们为何消失，再把答案发回母船。";
        private NoticePanelView panel;
        private bool shown, confirmed, ownsPause, confirmationReady;
        private float previousTimeScale;
        public bool IsVisible => panel && panel.gameObject.activeSelf;

        public bool BeginPresentation()
        {
            if (shown) return false;
            shown = true;
            if (!panelPrefab || string.IsNullOrWhiteSpace(openingText))
            {
                Debug.LogError("OpeningBriefingUI requires a NoticePanel prefab and opening text.", this);
                return false;
            }
            previousTimeScale = Time.timeScale;
            ownsPause = true;
            Time.timeScale = 0f;
            panel = Instantiate(panelPrefab);
            panel.Show("调查记录 01", "勘查任务 / 地球", openingText, "进入调查", Confirm);
            panel.SetConfirmationEnabled(false);

            return true;
        }

        public IEnumerator WaitForDismissal()
        {
            confirmationReady = true;
            panel.SetConfirmationEnabled(true);
            while (this && isActiveAndEnabled && !confirmed) yield return null;
            // Keep gameplay blocked through the confirmation frame; discard its input edge.
            yield return null;
            if (this) ReleasePresentation();
        }

        public void Confirm()
        {
            if (!IsVisible || !confirmationReady || confirmed) return;
            confirmed = true;
            panel.SetConfirmationEnabled(false);
        }

        private void ReleasePresentation()
        {
            if (ownsPause)
            {
                Time.timeScale = previousTimeScale;
                ownsPause = false;
            }
            if (panel) panel.Hide();
        }

        private void OnDisable() => ReleasePresentation();

        private void OnDestroy()
        {
            if (panel) Destroy(panel.gameObject);
        }
    }
}
