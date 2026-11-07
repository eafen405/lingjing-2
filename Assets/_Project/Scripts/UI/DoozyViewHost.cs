using System;
using Doozy.Engine.UI;
using UnityEngine;
#if UNITY_EDITOR
using System.Reflection;
using Doozy.Engine.UI.Settings;
#endif

namespace Lingjing.UI
{
    /// <summary>
    /// DoozyUI 薄封装：给 UiKit 在运行时长出来的界面根节点挂一个 <see cref="UIView"/>，
    /// 显隐交给 Doozy，布局仍然全部由 UiKit 生成。
    /// 这里只做四件事——挂组件并写好 ViewName/排序、把 Show/Hide 转给 UIView、Doozy 用不了时兜底 SetActive、
    /// 给界面根节点建安全区容器与 DOTween 淡入（显隐仍由 Doozy 决定，补间只管观感）。
    /// </summary>
    public sealed class DoozyViewHost
    {
        /// <summary>六个界面的固定 ViewName，探针与界面都从这里取。</summary>
        public const string MainMenuViewName = "MainMenu";
        public const string LevelSelectViewName = "LevelSelect";
        public const string HudViewName = "Hud";
        public const string PauseViewName = "Pause";
        public const string ChoiceViewName = "Choice";
        public const string ResultViewName = "Result";

        /// <summary>每个场景共用一个 Doozy 画布（与 UICanvas.MasterCanvasName 同名），随场景销毁。</summary>
        public const string CanvasName = "MasterCanvas";

        /// <summary>Doozy 画布的 sortingOrder：压在触屏摇杆那层（0）之上。</summary>
        const int CanvasSortingOrder = 1;

        // 画布内部的层级：HUD 常驻，暂停/三选一/结算必须叠在它上面，而不是互相顶掉。
        public const int MainMenuSortingOrder = 10;
        public const int LevelSelectSortingOrder = 20;
        public const int HudSortingOrder = 20;
        public const int PauseSortingOrder = 100;
        public const int ChoiceSortingOrder = 105;
        public const int ResultSortingOrder = 110;

        static Canvas s_canvas;
        static bool s_fallbackLogged;

        readonly GameObject _root;

        // 上一次淡入的结束时刻：淡入还在播（含同一帧里连续 Show）时只保留第一次，避免两条补间同写 alpha。
        float _fadeInUntil = -1f;

        DoozyViewHost(GameObject root, UIView view, string viewName)
        {
            _root = root;
            DoozyView = view;
            ViewName = viewName;
        }

        /// <summary>挂在界面根节点上的 UIView；Doozy 用不了时为 null，显隐走 SetActive 兜底。</summary>
        public UIView DoozyView { get; }

        public GameObject Root => _root;

        public string ViewName { get; }

        public bool UsingDoozy => DoozyView != null;

        public bool IsVisible => DoozyView != null
            ? DoozyView.IsVisible
            : _root != null && _root.activeSelf;

        /// <summary>当前场景的 Doozy 画布；没有就按 UiKit 的 960x540 建一个再挂 UICanvas。</summary>
        public static Canvas EnsureCanvas()
        {
            if (s_canvas != null)
            {
                return s_canvas;
            }

            UICanvas existing = FindCanvas();
            if (existing != null)
            {
                s_canvas = existing.Canvas;
                return s_canvas;
            }

            Canvas canvas = UiKit.CreateCanvas(CanvasName, CanvasSortingOrder);
            try
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    PreloadDoozySettings();
                }
#endif
                canvas.gameObject.AddComponent<UICanvas>().CanvasName = CanvasName;
            }
            catch (Exception e)
            {
                LogFallback("挂 UICanvas 失败（" + Describe(e) + "）");
            }

            s_canvas = canvas;
            return canvas;
        }

        /// <summary>场景里已有的 Doozy 画布，不创建；探针用它验证画布确实存在。</summary>
        public static UICanvas FindCanvas()
        {
            UICanvas[] found = UnityEngine.Object.FindObjectsOfType<UICanvas>(true);
            for (int i = 0; i < found.Length; i++)
            {
                UICanvas candidate = found[i];
                if (candidate != null && candidate.Canvas != null && candidate.Canvas.isRootCanvas)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>在父节点下建全屏界面根节点，挂 UIView 并统一从隐藏开始；要显示的一方显式 Show(true)。</summary>
        public static DoozyViewHost Create(Transform parent, string viewName, int sortingOrder)
        {
            var go = new GameObject(viewName + "View", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            DoozyViewHost host = Attach(go, viewName, sortingOrder);
            // 初始状态用 InstantHide 而不是 Hide(true)：父节点还没激活时 StartCoroutine 会直接
            // 报 "game object is inactive" 并留下「可见」的旧状态（关卡面板就踩过这个坑）。
            host.HideInstantly();
            return host;
        }

        public static DoozyViewHost Attach(GameObject root, string viewName, int sortingOrder)
        {
            UIView view = null;
            try
            {
                view = root.GetComponent<UIView>();
                if (view == null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        PreloadDoozySettings();
                    }
#endif
                    view = root.AddComponent<UIView>();
                }

                view.ViewName = viewName;
                // 运行时默认值是 null，Doozy 的静态 ShowView/HideView 会拿它 Equals 崩掉。
                view.ViewCategory = UIView.DefaultViewCategory;
                // 全屏根节点必须固定起点：Doozy 的 Hide 即使关了 Move 动画也会按 Direction 把
                // RectTransform 挪出屏幕，未固定起点时下一次 Show 会把这个偏移当成 StartPosition（视图失踪）。
                view.UseCustomStartAnchoredPosition = true;
                view.CustomStartAnchoredPosition = Vector3.zero;
                // 没有动画预设时，这三个开关决定 Hide 之后画面是否真的消失。
                view.DisableCanvasWhenHidden = true;
                view.DisableGraphicRaycasterWhenHidden = true;
                view.DisableGameObjectWhenHidden = true;
                // Doozy 默认动画全关，非 instant 的 Show()/Hide() 会直接 return；界面全代码生成，统一走零时长。
                view.ShowBehavior.InstantAnimation = true;
                view.HideBehavior.InstantAnimation = true;

                // UIView 自带嵌套 Canvas，父级 sortingOrder 管不到它，只能靠 overrideSorting 压层。
                Canvas canvas = root.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = sortingOrder;
                }
            }
            catch (Exception e)
            {
                view = null;
                LogFallback("挂 UIView 失败（" + Describe(e) + "）");
            }

            return new DoozyViewHost(root, view, viewName);
        }

        public void Show(bool instantAction = true)
        {
            Apply(true, instantAction);
        }

        public void Hide(bool instantAction = true)
        {
            Apply(false, instantAction);
        }

        /// <summary>
        /// 界面淡入：显隐与层级仍归 Doozy，alpha 过渡交给 DOTween（未缩放时间，暂停时也能播完）。
        /// 根节点还没有 CanvasGroup 时先补一个——Doozy 隐藏再显示后 alpha 可能仍停在隐藏时的值，
        /// 这里每次都从 0 补到 1，避免界面永久半透明。
        /// </summary>
        public void FadeIn(float seconds = 0.16f)
        {
            if (_root == null)
            {
                return;
            }

            CanvasGroup group = _root.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = _root.AddComponent<CanvasGroup>();
            }

            if (!Application.isPlaying)
            {
                // 编辑模式（UiProbe 直接调 Build/Show）没有 Update 推进 DOTween，补间会停在 alpha=0；
                // 这里直接落终态，探针校验的就是真机淡入结束后的状态。
                group.alpha = 1f;
                return;
            }

            // TweenFx.FadeIn 里的补间没有 target，group.DOKill() 停不掉上一条；淡入没播完就只沿用，
            // 同一帧里被连续 Show 也不会叠出两条补间抢着写 alpha。
            float now = Time.unscaledTime;
            if (now < _fadeInUntil)
            {
                return;
            }

            _fadeInUntil = now + seconds;
            TweenFx.FadeIn(group, seconds);
        }

        /// <summary>
        /// 给界面根节点建 full-stretch 的「Safe」内容容器并挂 <see cref="UiSafeArea"/>：
        /// 全屏 Backdrop/Dim 留在根节点保持满铺，可读控件挂 Safe 下，刘海/圆角屏才不会裁到边。
        /// 布局数字仍按 960x540 给，只换父节点——桌面整屏时 Safe 的锚点就是 0..1，等于没改。
        /// </summary>
        internal static RectTransform CreateSafeArea(Transform parent, string name = "Safe")
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<UiSafeArea>();
            return rect;
        }

        /// <summary>不经过协程的同步隐藏，只用于构造时的初始状态。</summary>
        void HideInstantly()
        {
            if (DoozyView != null)
            {
                DoozyView.InstantHide();
                return;
            }

            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        void Apply(bool visible, bool instantAction)
        {
            if (DoozyView == null)
            {
                // 兜底：DoozyUI 用不了时只保证界面还在，不承担动画与层级。
                if (_root != null)
                {
                    _root.SetActive(visible);
                }

                return;
            }

            if (visible)
            {
                DoozyView.Show(instantAction);
            }
            else
            {
                DoozyView.Hide(instantAction);
            }
        }

        static void LogFallback(string message)
        {
            if (s_fallbackLogged)
            {
                return;
            }

            s_fallbackLogged = true;
            Debug.LogWarning("[ui] DoozyUI 不可用：" + message + "；界面显隐回退 GameObject.SetActive");
        }

        static string Describe(Exception e) => e.GetType().Name + ": " + e.Message;

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器里 AddComponent 会触发 Doozy 的 Reset()，而本工程没有生成 UIViewSettings/UICanvasSettings 资产，
        /// Doozy 会顺手在 Assets/Doozy 下新建资产。先用内存实例占住 s_instance，保证不往包里写文件。
        /// </summary>
        static void PreloadDoozySettings()
        {
            PreloadSetting(typeof(UIViewSettings));
            PreloadSetting(typeof(UICanvasSettings));
        }

        static void PreloadSetting(Type settingsType)
        {
            FieldInfo field = settingsType.GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null && field.GetValue(null) == null)
            {
                field.SetValue(null, ScriptableObject.CreateInstance(settingsType));
            }
        }
#endif
    }
}
