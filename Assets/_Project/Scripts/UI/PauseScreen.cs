using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.UI
{
    /// <summary>
    /// 暂停界面：全屏遮罩 + 三个按钮。是否真的暂停世界由 BattleRunner 决定，
    /// 这里只负责显示与把按钮点击转成回调。
    /// 「重新开始」是两步确认：第一次点击只改标签，约 2 秒内再点一次才真的重开。
    /// </summary>
    public sealed class PauseScreen : MonoBehaviour
    {
        /// <summary>两步确认时「重新开始」按钮上的替代文案；UiProbe 按这个常量核对。</summary>
        public const string RestartConfirmText = "确认重开？";

        /// <summary>第一次点击后等待第二次点击的时限（秒，未缩放时间）。</summary>
        const float RestartConfirmSeconds = 2f;

        Action _resume;
        Action _restart;
        Action _mainMenu;

        Text _restartLabel;
        bool _restartPending;
        float _restartDeadline;

        /// <summary>暂停界面自己的 Doozy 视图，叠在 HUD 之上，默认隐藏。</summary>
        public DoozyViewHost View { get; private set; }

        public bool Visible => View != null && View.IsVisible;

        public static PauseScreen Build(Action resume, Action restart, Action mainMenu)
        {
            Canvas canvas = DoozyViewHost.EnsureCanvas();
            DoozyViewHost view = DoozyViewHost.Create(
                canvas.transform, DoozyViewHost.PauseViewName, DoozyViewHost.PauseSortingOrder);

            var screen = view.Root.AddComponent<PauseScreen>();
            screen.View = view;
            screen._resume = resume;
            screen._restart = restart;
            screen._mainMenu = mainMenu;
            screen.BuildUi(view.Root.transform);
            return screen;
        }

        void BuildUi(Transform root)
        {
            // Dim 留在根节点满铺；卡片收进安全区，横屏圆角屏下按钮不被裁。
            UiKit.CreateFullScreen(root, "Dim", UiPalette.Dim);
            RectTransform safe = DoozyViewHost.CreateSafeArea(root);

            var center = new Vector2(0.5f, 0.5f);
            RectTransform card = UiKit.CreatePanel(safe, "PauseCard", center, center, Vector2.zero,
                new Vector2(420f, 360f), UiPalette.Panel);

            UiKit.CreateTitle(card, UiText.Pause, 42, TextAnchor.MiddleCenter, center, center,
                new Vector2(0f, 120f), new Vector2(360f, 56f), UiPalette.Text);

            UiKit.CreateButton(card, UiText.Resume, center, center,
                new Vector2(0f, 40f), new Vector2(260f, 54f), OnResume, 24);
            Button restart = UiKit.CreateButton(card, UiText.Restart, center, center,
                new Vector2(0f, -30f), new Vector2(260f, 54f), OnRestart, 24);
            _restartLabel = restart.GetComponentInChildren<Text>(true);
            UiKit.CreateButton(card, UiText.MainMenu, center, center,
                new Vector2(0f, -100f), new Vector2(260f, 54f), OnMainMenu, 24);
        }

        void Update()
        {
            // 超时没点第二次：恢复原标签，界面不停在确认态。用未缩放时间，暂停时（timeScale=0）照样计时。
            if (_restartPending && Time.unscaledTime >= _restartDeadline)
            {
                ResetRestartConfirm();
            }
        }

        public void Show()
        {
            ResetRestartConfirm();
            View.Show(true);
            View.FadeIn();
        }

        public void Hide()
        {
            ResetRestartConfirm();
            View.Hide(true);
        }

        void OnResume()
        {
            ResetRestartConfirm();
            Hide();
            _resume?.Invoke();
        }

        void OnRestart()
        {
            if (!_restartPending)
            {
                // 第一步只进入确认态：重开会丢当前进度，误触一次不该直接重开。
                _restartPending = true;
                _restartDeadline = Time.unscaledTime + RestartConfirmSeconds;
                if (_restartLabel != null)
                {
                    _restartLabel.text = RestartConfirmText;
                }

                return;
            }

            Hide();
            _restart?.Invoke();
        }

        void OnMainMenu()
        {
            ResetRestartConfirm();
            Hide();
            _mainMenu?.Invoke();
        }

        void ResetRestartConfirm()
        {
            _restartPending = false;
            if (_restartLabel != null)
            {
                _restartLabel.text = UiText.Restart;
            }
        }
    }
}
