using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.UI
{
    /// <summary>
    /// 结算界面：全屏遮罩 + 失败/通关标题 + 得分 + 最高分 + 两个出口。
    /// 分数由 BattleRunner 按占位口径算好再灌进来，这里不碰 PlayerPrefs。
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        Text _title;
        Text _score;
        Text _highScore;
        Action _restart;
        Action _mainMenu;

        /// <summary>结算界面自己的 Doozy 视图，叠在 HUD 之上，默认隐藏。</summary>
        public DoozyViewHost View { get; private set; }

        public bool Visible => View != null && View.IsVisible;

        public static ResultScreen Build(Action restart, Action mainMenu)
        {
            Canvas canvas = DoozyViewHost.EnsureCanvas();
            DoozyViewHost view = DoozyViewHost.Create(
                canvas.transform, DoozyViewHost.ResultViewName, DoozyViewHost.ResultSortingOrder);

            var screen = view.Root.AddComponent<ResultScreen>();
            screen.View = view;
            screen._restart = restart;
            screen._mainMenu = mainMenu;
            screen.BuildUi(view.Root.transform);
            return screen;
        }

        void BuildUi(Transform root)
        {
            UiKit.CreateFullScreen(root, "Dim", new Color(0f, 0f, 0f, 0.78f));

            var center = new Vector2(0.5f, 0.5f);
            RectTransform card = UiKit.CreatePanel(root, "ResultCard", center, center, Vector2.zero,
                new Vector2(440f, 360f), new Color(0.10f, 0.13f, 0.17f, 0.98f));

            _title = UiKit.CreateLabel(card, string.Empty, 44, TextAnchor.MiddleCenter, center, center,
                new Vector2(0f, 120f), new Vector2(380f, 60f), new Color(0.95f, 0.45f, 0.45f, 1f));

            _score = UiKit.CreateLabel(card, string.Empty, 30, TextAnchor.MiddleCenter, center, center,
                new Vector2(0f, 52f), new Vector2(380f, 40f), new Color(0.92f, 0.94f, 0.96f, 1f));

            _highScore = UiKit.CreateLabel(card, string.Empty, 24, TextAnchor.MiddleCenter, center, center,
                new Vector2(0f, 12f), new Vector2(380f, 32f), new Color(0.62f, 0.68f, 0.74f, 1f));

            UiKit.CreateButton(card, UiText.Restart, center, center,
                new Vector2(0f, -58f), new Vector2(260f, 54f), OnRestart, 24);
            UiKit.CreateButton(card, UiText.MainMenu, center, center,
                new Vector2(0f, -128f), new Vector2(260f, 54f), OnMainMenu, 24);
        }

        public void Show(bool clear, int score, int highScore)
        {
            _title.text = clear ? UiText.ResultClear : UiText.ResultFail;
            _title.color = clear
                ? new Color(0.55f, 0.90f, 0.65f, 1f)
                : new Color(0.95f, 0.45f, 0.45f, 1f);
            _score.text = $"{UiText.Score} {score}";
            _highScore.text = $"{UiText.HighScore} {highScore}";
            View.Show(true);
        }

        public void Hide()
        {
            View.Hide(true);
        }

        void OnRestart()
        {
            Hide();
            _restart?.Invoke();
        }

        void OnMainMenu()
        {
            Hide();
            _mainMenu?.Invoke();
        }
    }
}
