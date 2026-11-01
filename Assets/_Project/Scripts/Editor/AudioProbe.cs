using Lingjing.Audio;
using Lingjing.View;
using UnityEditor;
using UnityEngine;

namespace Lingjing.Editor
{
    /// <summary>
    /// 音频层无头检查：台词表能解析、每条台词都有对应的 OGG、14 条音效都在。
    /// 缺素材时 AudioDirector 只会打一句 warning 然后静音，画面照跑——所以必须由探针来盯。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.AudioProbe.Run
    /// </summary>
    public static class AudioProbe
    {
        static readonly string[] VoiceIds =
        {
            "vo_fairy_level_start_1",
            "vo_fairy_first_sight_airdrop",
            "vo_fairy_boss_intro",
            "vo_fairy_daika_start",
            "vo_fairy_daika_end",
            "vo_fairy_low_hp",
            "vo_fairy_death",
            "vo_fairy_tut_knife",
            "vo_fairy_menu_idle",
            "vo_fairy_menu_idle_2"
        };

        static readonly string[] SfxIds =
        {
            "sfx_hit",
            "sfx_kill",
            "sfx_hurt",
            "sfx_pickup_health",
            "sfx_pickup_energy",
            "sfx_fairy_start",
            "sfx_fairy_end",
            "sfx_level_up",
            "sfx_choose",
            "sfx_ui_click",
            "sfx_section_clear",
            "sfx_boss_intro",
            "sfx_death",
            "sfx_win"
        };

        public static void Run()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(M1SceneBuilder.VoiceDataPath);
            if (json == null)
            {
                Fail($"找不到台词数据 {M1SceneBuilder.VoiceDataPath}");
                return;
            }

            VoiceLineTable table = VoiceLineTable.Parse(json.text);
            if (table?.lines == null || table.lines.Length == 0)
            {
                Fail("voice.json 解析成空");
                return;
            }

            int missingVoice = 0;
            int missingText = 0;
            for (int i = 0; i < VoiceIds.Length; i++)
            {
                if (Resources.Load<AudioClip>(AudioDirector.VoiceRoot + VoiceIds[i]) == null)
                {
                    Debug.LogError($"[audio] 缺语音：{VoiceIds[i]}");
                    missingVoice++;
                }

                if (string.IsNullOrEmpty(table.TextOf(VoiceIds[i])))
                {
                    Debug.LogError($"[audio] 缺字幕文本：{VoiceIds[i]}");
                    missingText++;
                }
            }

            int missingSfx = 0;
            for (int i = 0; i < SfxIds.Length; i++)
            {
                if (Resources.Load<AudioClip>(AudioDirector.SfxRoot + SfxIds[i]) == null)
                {
                    Debug.LogError($"[audio] 缺音效：{SfxIds[i]}");
                    missingSfx++;
                }
            }

            Debug.Log($"[audio] voice={VoiceIds.Length - missingVoice}/{VoiceIds.Length} " +
                      $"text={VoiceIds.Length - missingText}/{VoiceIds.Length} " +
                      $"sfx={SfxIds.Length - missingSfx}/{SfxIds.Length} " +
                      $"linesInTable={table.lines.Length}");

            bool pass = missingVoice == 0 && missingText == 0 && missingSfx == 0;
            Debug.Log(pass ? "[audio] PROBE PASS" : "[audio] PROBE FAIL");
            if (!pass)
            {
                EditorApplication.Exit(1);
            }
        }

        static void Fail(string reason)
        {
            Debug.LogError($"[audio] PROBE FAIL: {reason}");
            EditorApplication.Exit(1);
        }
    }
}
