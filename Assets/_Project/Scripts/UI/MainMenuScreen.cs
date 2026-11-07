using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.UI
{
    /// <summary>
    /// 主菜单：标题、三个入口与最高分。控件全部由 UiKit 在运行时生成，
    /// 场景里只留 UiBootstrap，所以不会出现序列化后丢 Sprite 的情况。
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        /// <summary>左右安全边距（参考像素）。</summary>
        const float Margin = 40f;

        static readonly Color TextColor = UiPalette.Text;
        static readonly Color MutedColor = UiPalette.Muted;

        Text _highScore;

        public LevelSelectPanel LevelSelect { get; private set; }

        /// <summary>主菜单自己的 Doozy 视图，显隐由它负责。</summary>
        public DoozyViewHost View { get; private set; }

        public static MainMenuScreen Build(Canvas canvas)
        {
            DoozyViewHost view = DoozyViewHost.Create(
                canvas.transform, DoozyViewHost.MainMenuViewName, DoozyViewHost.MainMenuSortingOrder);

            var screen = view.Root.AddComponent<MainMenuScreen>();
            screen.View = view;
            screen.BuildUi(view.Root.transform);
            view.Show(true);
            // Doozy 的实例动画保持 instant，DOTween 只补一层淡入观感。
            view.FadeIn();
            return screen;
        }

        void BuildUi(Transform root)
        {
            // 全屏背景留在根节点保持满铺；可读内容收进安全区，刘海/圆角屏下标题、最高分与按钮不被裁。
            UiKit.CreateFullScreen(root, "Backdrop", UiPalette.Backdrop);
            RectTransform safe = DoozyViewHost.CreateSafeArea(root);

            UiKit.CreatePanel(safe, "Accent", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -Margin), new Vector2(96f, 6f),
                UiPalette.Signal, rounded: false);

            UiKit.CreateTitle(safe, UiText.GameTitle, 78, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -66f), new Vector2(560f, 96f), TextColor);

            UiKit.CreateLabel(safe, UiText.Subtitle, 26, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -158f), new Vector2(560f, 36f), MutedColor);

            _highScore = UiKit.CreateLabel(safe, string.Empty, 24, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-Margin, -50f), new Vector2(260f, 30f), MutedColor);
            RefreshHighScore();

            var center = new Vector2(0.5f, 0.5f);
            UiKit.CreateButton(safe, UiText.StartGame, center, center,
                new Vector2(0f, 10f), new Vector2(320f, 64f), () => UiNav.LoadLevel(1), 28);
            UiKit.CreateButton(safe, UiText.LevelSelect, center, center,
                new Vector2(0f, -70f), new Vector2(320f, 64f), () => LevelSelect.Show(), 28);
            UiKit.CreateButton(safe, UiText.Quit, center, center,
                new Vector2(0f, -150f), new Vector2(320f, 64f), QuitGame, 28);

            // 关卡面板自带全屏 Dim 与自己的 Safe，仍挂根节点（不能收进主菜单的 Safe，否则遮罩不满铺）。
            LevelSelect = LevelSelectPanel.Build(root);
        }

        /// <summary>最高分只在进入主菜单时读一次；关卡结算后回到主菜单会重建这个界面。</summary>
        public void RefreshHighScore()
        {
            if (_highScore != null)
            {
                _highScore.text = $"{UiText.HighScore} {GameSave.HighScore}";
            }
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
