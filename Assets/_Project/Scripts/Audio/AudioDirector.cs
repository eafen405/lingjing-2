using System.Collections.Generic;
using UnityEngine;

namespace Lingjing.Audio
{
    /// <summary>
    /// 声音的唯一出口：语音一条、音效一路，都按名字从 Resources 取，代码里不写资产路径。
    /// 音效目前是合成占位（tools/audio/make_sfx.py），换真素材只要覆盖同名文件。
    /// 字幕挂在语音上：台词在播的时候它才有内容，播完自己消失。
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const string VoiceRoot = "Audio/Voice/";
        public const string SfxRoot = "Audio/Sfx/";

        /// <summary>同一句台词的最短重复间隔，避免同一事件在一秒内喊三次。</summary>
        public const float DefaultVoiceCooldown = 6f;

        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> _readyAt = new Dictionary<string, float>();

        /// <summary>每路音效各自的下次可播时间：不同 id 各按各的窗口节流，不再共用一个时间戳。</summary>
        readonly Dictionary<string, float> _sfxReadyAt = new Dictionary<string, float>();

        AudioSource _voice;
        AudioSource _sfx;
        VoiceLineTable _lines;
        string _subtitle;
        float _subtitleUntil;

        /// <summary>当前台词字幕；按时间到期而不是查 isPlaying——播放器状态在无声设备上不可靠。</summary>
        public string Subtitle => Time.unscaledTime < _subtitleUntil ? _subtitle : null;

        public static AudioDirector Attach(GameObject host, VoiceLineTable lines)
        {
            var director = host.AddComponent<AudioDirector>();
            director._lines = lines;

            director._voice = host.AddComponent<AudioSource>();
            director._voice.playOnAwake = false;
            director._voice.loop = false;
            director._voice.volume = 1f;

            director._sfx = host.AddComponent<AudioSource>();
            director._sfx.playOnAwake = false;
            director._sfx.volume = 0.7f;

            return director;
        }

        /// <summary>按 id 播一句台词并挂上字幕。冷却中或缺素材则返回 false。</summary>
        public bool Say(string id, float cooldown = DefaultVoiceCooldown)
        {
            if (_voice == null || string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (_readyAt.TryGetValue(id, out float readyAt) && Time.unscaledTime < readyAt)
            {
                return false;
            }

            AudioClip clip = Load(VoiceRoot, id);
            if (clip == null)
            {
                return false;
            }

            _readyAt[id] = Time.unscaledTime + cooldown;
            _voice.clip = clip;
            _voice.Play();
            _subtitle = _lines != null ? _lines.TextOf(id) : null;
            _subtitleUntil = Time.unscaledTime + clip.length + 0.2f;
            return true;
        }

        public void PlaySfx(string id, float volume = 1f)
        {
            if (_sfx == null)
            {
                return;
            }

            AudioClip clip = Load(SfxRoot, id);
            if (clip != null)
            {
                _sfx.PlayOneShot(clip, volume);
            }
        }

        /// <summary>密集事件音效：每路 id 各记各的最短间隔，命中 45ms 与受击 300ms 不再互相挤占。</summary>
        public void PlaySfxThrottled(string id, float minInterval, float volume = 1f)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            float now = Time.unscaledTime;
            if (_sfxReadyAt.TryGetValue(id, out float readyAt) && now < readyAt)
            {
                return;
            }

            _sfxReadyAt[id] = now + minInterval;
            PlaySfx(id, volume);
        }

        /// <summary>外部直接把字幕打出来（例如结算旁白），三秒后自动收起。</summary>
        public void ShowSubtitle(string text)
        {
            _subtitle = text;
            _subtitleUntil = Time.unscaledTime + 3f;
        }

        AudioClip Load(string root, string id)
        {
            string key = root + id;
            if (_clips.TryGetValue(key, out AudioClip cached))
            {
                return cached;
            }

            AudioClip clip = Resources.Load<AudioClip>(key);
            if (clip == null)
            {
                Debug.LogWarning($"[audio] 找不到音频：Resources/{key}");
            }

            _clips[key] = clip;
            return clip;
        }
    }
}
