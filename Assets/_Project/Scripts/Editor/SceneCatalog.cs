using UnityEditor;

namespace Lingjing.Editor
{
    /// <summary>
    /// 场景清单的唯一权威：MainMenu 必须在第 0 位（启动场景）。
    /// 场景生成器都调 <see cref="Sync"/>，不要各自写 <c>EditorBuildSettings.scenes</c>——
    /// 否则谁后跑谁覆盖，APK 里会少场景。
    /// </summary>
    public static class SceneCatalog
    {
        public const string MainMenu = "Assets/_Project/Scenes/MainMenu.unity";
        public const string Level1 = "Assets/_Project/Scenes/M1.unity";
        public const string Level2 = "Assets/_Project/Scenes/M2.unity";

        public static readonly string[] All = { MainMenu, Level1, Level2 };

        public static void Sync()
        {
            var scenes = new EditorBuildSettingsScene[All.Length];
            for (int i = 0; i < All.Length; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(All[i], true);
            }

            EditorBuildSettings.scenes = scenes;
        }
    }
}
