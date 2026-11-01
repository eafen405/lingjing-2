using UnityEngine;

namespace Lingjing.UI
{
    /// <summary>
    /// Fairy 层的取色板，取自 OurFairy 的 CRT 工业 HUD（见 docs/agents/fairy-visual.md）。
    /// 这是**非像素**的那一层：所有界面都从这里取色，不要把颜色散回各个界面里。
    /// </summary>
    public static class UiPalette
    {
        // 底色
        public static readonly Color Backdrop = Hex("08111F");
        public static readonly Color Window = Hex("050A18");
        public static readonly Color Panel = Hex("0A1426", 0.96f);
        public static readonly Color Capsule = Hex("101C2E");
        public static readonly Color Button = Hex("142238");
        public static readonly Color HudBackdrop = Hex("050A18", 0.82f);
        public static readonly Color BarTrack = Hex("050A18", 0.92f);

        // 强调
        public static readonly Color Signal = Hex("72D7FF");
        public static readonly Color Electric = Hex("1F55E0");
        public static readonly Color Glow = Hex("3D8BFF");
        public static readonly Color Link = Hex("8FB8FF");

        // 文字
        public static readonly Color Text = Hex("EAF3FF");
        public static readonly Color Muted = Hex("A9B8D8");
        public static readonly Color Disabled = Hex("5B6A86");

        // 语义
        public static readonly Color Success = Hex("5BE49B");
        public static readonly Color Danger = Hex("FF5A4E");
        public static readonly Color Warning = Hex("F2C14E");

        // 线与遮罩
        public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color DimStrong = new Color(0f, 0f, 0f, 0.80f);

        /// <summary>#RRGGBB → Color；alpha 可覆盖。</summary>
        public static Color Hex(string hex, float alpha = 1f)
        {
            int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f, alpha);
        }
    }
}
