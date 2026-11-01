using Lingjing.Level;
using UnityEngine;

namespace Lingjing.UI
{
    /// <summary>UI 文案的唯一来源：画面里的字都从这里取，改文案只改这一处。</summary>
    public static class UiText
    {
        public const string GameTitle = "灵境";
        public const string Subtitle = "伊埃斯的空洞";

        public const string StartGame = "开始游戏";
        public const string LevelSelect = "关卡选择";
        public const string Quit = "退出游戏";
        public const string Back = "返回";
        public const string Continue = "继续";
        public const string Restart = "重新开始";
        public const string MainMenu = "返回主菜单";
        public const string Pause = "暂停";
        public const string Resume = "继续游戏";

        public const string Level1 = "第一关";
        public const string Level2 = "第二关";
        public const string Locked = "未解锁";
        public const string Cleared = "已通关";

        public const string Hp = "生命";
        public const string Energy = "充能";
        public const string FairyTakeover = "Fairy 代行中";

        public const string ResultClear = "通关";
        public const string ResultFail = "失败";
        public const string Score = "得分";
        public const string HighScore = "最高分";

        public const string MoveHint = "左侧摇杆移动";
        public const string SkillHint = "右下技能键：代行";
        public const string ReverseHint = "右下上键：正反切";

        public const string LevelUp = "升级";
        public const string PickUpgrade = "选择一项强化";
        public const string Choose = "选择";

        /// <summary>HUD 上的节进度，如「第 2/4 节」。</summary>
        public static string SectionProgress(int current, int total) => $"第 {current}/{total} 节";

        /// <summary>换节时中间那个大提示。</summary>
        public static string SectionBanner(int current) => $"第 {current} 节";

        /// <summary>三选一卡面的类别名。内核不认识中文，名字只在这里。</summary>
        public static string GrowthName(GrowthKind kind) => kind switch
        {
            GrowthKind.Damage => "攻击力",
            GrowthKind.BladeCount => "刀数",
            GrowthKind.Omega => "角速度",
            GrowthKind.Speed => "移速",
            GrowthKind.FairyDuration => "代行时长",
            _ => "?"
        };

        /// <summary>增量的显示文本。数值本身从 GrowthRules 取，改平衡不会让卡面说谎。</summary>
        public static string GrowthEffect(GrowthKind kind) => kind switch
        {
            GrowthKind.BladeCount => "+1",
            GrowthKind.FairyDuration => $"+{GrowthRules.FairyDurationPerLevelSeconds:0}秒",
            _ => $"+{(GrowthRules.MultiplierOf(kind) - 1.0) * 100.0:0}%"
        };
    }
}
