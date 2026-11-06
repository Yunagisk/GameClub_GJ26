using System;
using UnityEngine;

namespace Chuangyou.Narrative
{
    /// <summary>
    /// 一条可显示在剧情日志终端里的记录。
    /// 直接在 Inspector 里改字段即可，不需要改代码。
    /// </summary>
    [Serializable]
    public sealed class NarrativeLogEntry
    {
        [Tooltip("条目标题，例如：第三次勘探记录")]
        public string title = "未命名记录";

        [Tooltip("可选。显示在标题上方的小字，例如：DAY 03 / 07:42")]
        public string timestamp = string.Empty;

        [TextArea(4, 16)]
        [Tooltip("日志正文。支持 Unity 富文本，例如 <color=#E3B15F>重点</color>。")]
        public string body = string.Empty;

        [Tooltip("可选。标题上方编号的强调色。")]
        public Color accent = new Color(0.89f, 0.69f, 0.37f, 1f);

        public NarrativeLogEntry()
        {
        }

        public NarrativeLogEntry(string title, string timestamp, string body)
        {
            this.title = title;
            this.timestamp = timestamp;
            this.body = body;
        }
    }
}
