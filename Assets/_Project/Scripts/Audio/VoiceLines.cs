using System;
using UnityEngine;

namespace Lingjing.Audio
{
    /// <summary>
    /// 台词表：id → 字幕文本。
    /// 文本的权威是 docs/specs/audio.md §2.1，这份 JSON 是它在运行时的镜像；
    /// 音频本身按 id 从 Resources/Audio/Voice 取，两边靠同一个 id 对上。
    /// </summary>
    [Serializable]
    public sealed class VoiceLine
    {
        public string id;
        public string text;
    }

    [Serializable]
    public sealed class VoiceLineTable
    {
        public VoiceLine[] lines;

        /// <summary>取一条台词的字幕；找不到返回 null。</summary>
        public string TextOf(string id)
        {
            if (lines == null)
            {
                return null;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null && lines[i].id == id)
                {
                    return lines[i].text;
                }
            }

            return null;
        }

        public static VoiceLineTable Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonUtility.FromJson<VoiceLineTable>(json);
        }
    }
}
