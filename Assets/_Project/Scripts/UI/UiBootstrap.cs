using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lingjing.UI
{
    /// <summary>
    /// MainMenu 场景的唯一条目：运行时用 UiKit 把主菜单长出来。
    /// 场景文件只保存这个组件，不保存任何 Image/Text，避免运行时生成的 Sprite 丢失引用。
    /// </summary>
    public sealed class UiBootstrap : MonoBehaviour
    {
        public MainMenuScreen Menu { get; private set; }

        bool _built;

        void Awake()
        {
            Build();
        }

        /// <summary>幂等；编辑器探针在非播放模式下也走这个入口。</summary>
        public void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            UiKit.EnsureEventSystem();
            Canvas canvas = UiKit.CreateCanvas("MenuCanvas");
            Menu = MainMenuScreen.Build(canvas);
        }
    }

    /// <summary>
    /// 场景切换的唯一入口：切场景前统一把 timeScale 恢复成 1，
    /// 避免从暂停/结算界面把 0 带进下一个场景。
    /// </summary>
    public static class UiNav
    {
        public static void LoadLevel(int level)
        {
            if (!GameFlow.IsUnlocked(level) || !GameFlow.HasScene(level))
            {
                return;
            }

            Load(GameFlow.SceneOf(level));
        }

        public static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        public static void Restart()
        {
            Load(SceneManager.GetActiveScene().name);
        }

        public static void MainMenu()
        {
            Load(GameFlow.MainMenuScene);
        }
    }
}
