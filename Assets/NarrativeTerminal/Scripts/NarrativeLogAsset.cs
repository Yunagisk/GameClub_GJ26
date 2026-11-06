using System.Collections.Generic;
using UnityEngine;

namespace Chuangyou.Narrative
{
    /// <summary>
    /// 可复用的日志内容资产。Project 窗口右键 Create > 创优工坊 > 剧情日志 创建。
    /// 同一个资产可以给多个终端共用。
    /// </summary>
    [CreateAssetMenu(menuName = "创优工坊/剧情日志", fileName = "NarrativeLog", order = 0)]
    public sealed class NarrativeLogAsset : ScriptableObject
    {
        [Header("终端标题")]
        [SerializeField] private string terminalTitle = "遗迹终端";
        [SerializeField] private string subtitle = "ARCHIVE // 可读取记录";

        [Header("日志条目（按顺序显示）")]
        [SerializeField] private List<NarrativeLogEntry> entries = new List<NarrativeLogEntry>();

        public string TerminalTitle
        {
            get { return string.IsNullOrWhiteSpace(terminalTitle) ? "剧情日志" : terminalTitle; }
        }

        public string Subtitle
        {
            get { return subtitle; }
        }

        public IReadOnlyList<NarrativeLogEntry> Entries
        {
            get { return entries; }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(terminalTitle))
            {
                terminalTitle = "遗迹终端";
            }

            if (entries == null)
            {
                entries = new List<NarrativeLogEntry>();
            }
        }
#endif
    }
}
