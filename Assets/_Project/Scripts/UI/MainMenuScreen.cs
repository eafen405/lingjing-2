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

        static readonly Color TextColor = new Color(0.92f, 0.94f, 0.96f, 1f);
        static readonly Color MutedColor = new Color(0.62f, 0.68f, 0.74f, 1f);

        Text _highScore;

        public LevelSelectPanel LevelSelect { get; private set; }

        public static MainMenuScreen Build(Canvas canvas)
        {
            var screen = canvas.gameObject.AddComponent<MainMenuScreen>();
            screen.BuildUi(canvas.transform);
            return screen;
        }

        void BuildUi(Transform root)
        {
            UiKit.CreateFullScreen(root, "Backdrop", new Color(0.035f, 0.043f, 0.055f, 1f));

            UiKit.CreatePanel(root, "Accent", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -Margin), new Vector2(96f, 6f),
                new Color(0.79f, 0.95f, 0.30f, 1f), rounded: false);

            UiKit.CreateLabel(root, UiText.GameTitle, 72, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -66f), new Vector2(560f, 90f), TextColor);

            UiKit.CreateLabel(root, UiText.Subtitle, 26, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -158f), new Vector2(560f, 36f), MutedColor);

            _highScore = UiKit.CreateLabel(root, string.Empty, 24, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-Margin, -50f), new Vector2(260f, 30f), MutedColor);
            RefreshHighScore();

            var center = new Vector2(0.5f, 0.5f);
            UiKit.CreateButton(root, UiText.StartGame, center, center,
                new Vector2(0f, 10f), new Vector2(320f, 64f), () => UiNav.LoadLevel(1), 28);
            UiKit.CreateButton(root, UiText.LevelSelect, center, center,
                new Vector2(0f, -70f), new Vector2(320f, 64f), () => LevelSelect.Show(), 28);
            UiKit.CreateButton(root, UiText.Quit, center, center,
                new Vector2(0f, -150f), new Vector2(320f, 64f), QuitGame, 28);

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
