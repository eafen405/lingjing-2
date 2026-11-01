using UnityEngine;

namespace Lingjing.UI
{
    /// <summary>
    /// 关卡进度与分数。PlayerPrefs 是唯一存储，读写都从这里过，界面不直接碰 PlayerPrefs。
    /// </summary>
    public static class GameSave
    {
        const string KeyUnlocked = "lingjing.unlockedLevel";
        const string KeyHighScore = "lingjing.highScore";
        const string KeyTotalScore = "lingjing.totalScore";

        /// <summary>已解锁的最大关卡序号，从 1 开始；默认只开第一关。</summary>
        public static int UnlockedLevel
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyUnlocked, 1), 1, GameFlow.LevelCount);
            set
            {
                int clamped = Mathf.Clamp(value, 1, GameFlow.LevelCount);
                if (clamped <= PlayerPrefs.GetInt(KeyUnlocked, 1))
                {
                    return;
                }

                PlayerPrefs.SetInt(KeyUnlocked, clamped);
                PlayerPrefs.Save();
            }
        }

        public static int HighScore => PlayerPrefs.GetInt(KeyHighScore, 0);

        public static void SubmitScore(int score)
        {
            int total = PlayerPrefs.GetInt(KeyTotalScore, 0) + Mathf.Max(0, score);
            PlayerPrefs.SetInt(KeyTotalScore, total);

            if (score > HighScore)
            {
                PlayerPrefs.SetInt(KeyHighScore, score);
            }

            PlayerPrefs.Save();
        }

        public static int TotalScore => PlayerPrefs.GetInt(KeyTotalScore, 0);

        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(KeyUnlocked);
            PlayerPrefs.DeleteKey(KeyHighScore);
            PlayerPrefs.DeleteKey(KeyTotalScore);
            PlayerPrefs.Save();
        }
    }

    /// <summary>场景名与关卡序号的唯一映射。</summary>
    public static class GameFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const int LevelCount = 2;

        /// <summary>关卡序号 → 场景名。两关各一个场景，关卡内容由 levels.json 的 id 决定。</summary>
        public static string SceneOf(int level) => level switch
        {
            1 => "M1",
            2 => "M2",
            _ => null
        };

        public static string LevelName(int level) => level switch
        {
            1 => UiText.Level1,
            2 => UiText.Level2,
            _ => "?"
        };

        public static bool IsUnlocked(int level) => level <= GameSave.UnlockedLevel;

        public static bool HasScene(int level) => SceneOf(level) != null;
    }
}
