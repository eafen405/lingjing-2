using Lingjing.Fairy;
using Lingjing.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingjing.View
{
    /// <summary>
    /// 触屏指令源：全屏浮动摇杆给移动，右下两个按钮给技能与正反切。
    /// 编辑器里鼠标就是手指，桌面和手机走同一条代码路径。
    /// 按钮的身份标签、按下反馈、技能就绪呼吸都在这一层挂好，读取方只管动作本身。
    /// </summary>
    public sealed class TouchInputController : MonoBehaviour, ICommandSource
    {
        /// <summary>按钮按下的统一音效 id（AudioProbe 已盯住这条资源）。</summary>
        public const string ClickSfx = "sfx_ui_click";

        public VirtualJoystick moveStick;
        public TouchButton skillButton;
        public TouchButton reverseButton;

        /// <summary>
        /// 按钮按下时发一次，参数是音效 id；播放归 BattleRunner 的 AudioDirector，
        /// View 层不建 AudioSource，音效资产也只在 Audio 层加载。
        /// </summary>
        public event System.Action<string> SfxRequested;

        public Command CurrentCommand { get; private set; } = Command.None;

        int _spinSign = 1;
        bool _skillQueued;

        public bool TakeSkillPress()
        {
            bool queued = _skillQueued;
            _skillQueued = false;
            return queued;
        }

        /// <summary>技能满槽且未在代行时打开技能键的呼吸提示；false 时立即恢复常态。</summary>
        public void SetSkillReady(bool ready)
        {
            if (skillButton != null)
            {
                skillButton.SetReady(ready);
            }
        }

        /// <summary>充能不足却按了技能键：技能键短暂偏红，告诉玩家「按到了，但还开不了」。</summary>
        public void FlashSkillRejected()
        {
            if (skillButton != null)
            {
                skillButton.FlashRejected();
            }
        }

        /// <summary>
        /// 把内部旋转方向直接对齐到世界。Fairy 代行期间规划器会自己翻转刀环，
        /// 交还控制权时不同步的话，世界会把「方向不一致」当成一次正反切而多反一次。
        /// 顺手刷新当前指令，交还的那一 tick 就用上（传 0 视为「不知道」，保持原值）。
        /// </summary>
        public void SyncSpinSign(int sign)
        {
            if (sign == 0)
            {
                return;
            }

            _spinSign = sign;
            CurrentCommand = new Command(CurrentCommand.Move, sign, CurrentCommand.RequestReverse);
        }

        /// <summary>按钮按下 → 请求点击音效。TouchHud 里接线，探针也走这条路径。</summary>
        internal void RequestClickSfx()
        {
            SfxRequested?.Invoke(ClickSfx);
        }

        void OnDestroy()
        {
            if (skillButton != null)
            {
                skillButton.Pressed -= RequestClickSfx;
            }

            if (reverseButton != null)
            {
                reverseButton.Pressed -= RequestClickSfx;
            }
        }

        void Update()
        {
            Vector2 move = moveStick != null ? moveStick.Value : Vector2.zero;
            bool reverse = false;

            if (reverseButton != null && reverseButton.PressedThisFrame)
            {
                reverse = true;
                _spinSign = -_spinSign;
                reverseButton.ConsumeFrame();
            }

            if (skillButton != null)
            {
                if (skillButton.PressedThisFrame)
                {
                    _skillQueued = true;
                }

                skillButton.ConsumeFrame();
            }

            CurrentCommand = new Command(new Lingjing.Core.Vec2(move.x, move.y), _spinSign, reverse);
        }
    }

    /// <summary>
    /// 运行期搭 HUD：Canvas + EventSystem + 全屏摇杆可触层 + 安全区容器里的两个按钮。
    /// 不依赖外部 UI 图，圆与方块都在代码里生成，保证 APK 里一定有。
    /// 层级顺序就是命中顺序：摇杆层在最下（铺满屏幕），安全区容器建在它之后，按钮压在上面。
    /// </summary>
    public static class TouchHud
    {
        /// <summary>摇杆可触层在 Canvas 里的排序：压在世界之上、Doozy 界面（1）之下。</summary>
        public const int SortingOrder = 0;

        static Sprite _circle;

        public static TouchInputController Build()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("TouchHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;

            _circle ??= MakeCircle(96);

            var controller = canvasGo.AddComponent<TouchInputController>();
            controller.moveStick = BuildJoystick(canvasGo.transform);

            // 两个按钮收进安全区：刘海/圆角机右下角不会被裁；摇杆层保持全屏，不跟着安全区缩。
            RectTransform safeArea = BuildSafeArea(canvasGo.transform);
            controller.reverseButton = BuildButton(safeArea, "ReverseButton", new Vector2(-220f, 120f),
                new Color(0.5f, 0.62f, 0.82f, 0.5f), UiText.ReverseButton);
            controller.skillButton = BuildButton(safeArea, "SkillButton", new Vector2(-110f, 80f),
                new Color(0.6f, 0.88f, 0.32f, 0.55f), UiText.SkillButton);

            // 点击音效只发意图：正反切与技能按下各触发一次 sfx_ui_click。
            controller.reverseButton.Pressed += controller.RequestClickSfx;
            controller.skillButton.Pressed += controller.RequestClickSfx;

            // 按钮附近不激活摇杆：拇指想按技能却把角色带偏是最伤的误触。
            controller.moveStick.blockers = new[]
            {
                (RectTransform)controller.reverseButton.transform,
                (RectTransform)controller.skillButton.transform
            };

            return controller;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        /// <summary>全屏可触层 + 一个随触点出现的底座。</summary>
        static VirtualJoystick BuildJoystick(Transform parent)
        {
            var areaGo = new GameObject("JoystickArea", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
            var area = (RectTransform)areaGo.transform;
            area.SetParent(parent, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;

            var areaImage = areaGo.GetComponent<Image>();
            areaImage.color = new Color(0f, 0f, 0f, 0f);
            areaImage.raycastTarget = true;

            var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            var ring = (RectTransform)ringGo.transform;
            ring.SetParent(area, false);
            ring.sizeDelta = new Vector2(200f, 200f);

            var ringImage = ringGo.GetComponent<Image>();
            ringImage.sprite = _circle;
            ringImage.color = new Color(1f, 1f, 1f, 0.18f);
            ringImage.raycastTarget = false;

            var knobGo = new GameObject("Knob", typeof(RectTransform), typeof(Image));
            var knob = (RectTransform)knobGo.transform;
            knob.SetParent(ring, false);
            knob.sizeDelta = new Vector2(84f, 84f);

            var knobImage = knobGo.GetComponent<Image>();
            knobImage.sprite = _circle;
            knobImage.color = new Color(0.86f, 0.9f, 0.95f, 0.55f);
            knobImage.raycastTarget = false;

            var stick = areaGo.GetComponent<VirtualJoystick>();
            stick.ring = ring;
            stick.knob = knob;
            stick.radiusPixels = 90f;
            stick.Bind();
            return stick;
        }

        /// <summary>
        /// 安全区容器：先按全屏锚点搭好再挂 UiSafeArea——设备上组件把锚点收进安全区，
        /// 桌面上 safeArea 就是整屏，等于没改。顺序不能反：UiSafeArea 的 Awake 挂上就会算一次。
        /// </summary>
        static RectTransform BuildSafeArea(Transform parent)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<UiSafeArea>();
            return rect;
        }

        static TouchButton BuildButton(RectTransform parent, string name, Vector2 anchored, Color color, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchored;
            rect.sizeDelta = new Vector2(96f, 96f);

            var image = go.GetComponent<Image>();
            image.sprite = _circle;
            image.color = color;

            // 短名压在圆图之上：子节点总在父节点的 Image 之后绘制，字不会被圆盖住。
            Text label = UiKit.CreateLabel(rect, text, 20, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 40f), Color.white);
            label.name = name + "Label";

            // 组件最后加：这样 Awake 缓存到的是配好色之后的常态外观；
            // 批处理探针在编辑器里跑、Awake 不执行，所以再显式 Bind 一次。
            var button = go.AddComponent<TouchButton>();
            button.label = label;
            button.Bind();
            return button;
        }

        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            float r = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
