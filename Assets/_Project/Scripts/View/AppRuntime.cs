using UnityEngine;

namespace Lingjing.View
{
    /// <summary>
    /// 进程级运行时设置。放在这里而不是某个场景的脚本上：任何场景（主菜单 / 关卡）都先跑一次，
    /// 不会再出现「主菜单 60 帧、进关掉到 30 帧」这种只在真机上才看得出的差异。
    /// 数值口径见 game-design §1：横屏、60fps。
    /// </summary>
    public static class AppRuntime
    {
        /// <summary>目标帧率。像素动画本身是 12fps，画面必须稳在 60 才不抖。</summary>
        public const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            // 移动端 Unity 默认不保证 60；不显式设就会被平台默认值按住。
            Application.targetFrameRate = TargetFrameRate;
            QualitySettings.vSyncCount = 0;

            // 2D 像素画面用不上 MSAA，开着只是白白吃掉带宽。
            QualitySettings.antiAliasing = 0;

            // 真机试玩时不要因为没人碰屏幕就掉亮度、锁屏。
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
