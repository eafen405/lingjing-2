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
    /// 这里只做三件事——挂组件并写好 ViewName/排序、把 Show/Hide 转给 UIView、Doozy 用不了时兜底 SetActive。
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
