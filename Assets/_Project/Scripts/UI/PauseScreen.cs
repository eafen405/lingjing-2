using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.UI
{
    /// <summary>
    /// 暂停界面：全屏遮罩 + 三个按钮。是否真的暂停世界由 BattleRunner 决定，
    /// 这里只负责显示与把按钮点击转成回调。
    /// </summary>
    public sealed class PauseScreen : MonoBehaviour
    {
        Canvas _canvas;
        Action _resume;
        Action _restart;
        Action _mainMenu;

        public bool Visible => _canvas != null && _canvas.gameObject.activeSelf;

        public static PauseScreen Build(Action resume, Action restart, Action mainMenu)
        {
            Canvas canvas = UiKit.CreateCanvas("PauseCanvas", 100);
            var screen = canvas.gameObject.AddComponent<PauseScreen>();
            screen._canvas = canvas;
            screen._resume = resume;
            screen._restart = restart;
            screen._mainMenu = mainMenu;
            screen.BuildUi(canvas.transform);
            canvas.gameObject.SetActive(false);
            return screen;
        }

        void BuildUi(Transform root)
        {
            UiKit.CreateFullScreen(root, "Dim", new Color(0f, 0f, 0f, 0.68f));

            var center = new Vector2(0.5f, 0.5f);
            RectTransform card = UiKit.CreatePanel(root, "PauseCard", center, center, Vector2.zero,
                new Vector2(420f, 360f), new Color(0.10f, 0.13f, 0.17f, 0.98f));

            UiKit.CreateLabel(card, UiText.Pause, 40, TextAnchor.MiddleCenter, center, center,
                new Vector2(0f, 120f), new Vector2(360f, 56f), new Color(0.92f, 0.94f, 0.96f, 1f));

            UiKit.CreateButton(card, UiText.Resume, center, center,
                new Vector2(0f, 40f), new Vector2(260f, 54f), OnResume, 24);
            UiKit.CreateButton(card, UiText.Restart, center, center,
                new Vector2(0f, -30f), new Vector2(260f, 54f), OnRestart, 24);
            UiKit.CreateButton(card, UiText.MainMenu, center, center,
                new Vector2(0f, -100f), new Vector2(260f, 54f), OnMainMenu, 24);
        }

        public void Show()
        {
            _canvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _canvas.gameObject.SetActive(false);
        }

        void OnResume()
        {
            Hide();
            _resume?.Invoke();
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
