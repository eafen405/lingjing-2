using Lingjing.Fairy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingjing.View
{
    /// <summary>
    /// 触屏指令源：全屏浮动摇杆给移动，右下两个按钮给技能与正反切。
    /// 编辑器里鼠标就是手指，桌面和手机走同一条代码路径。
    /// </summary>
    public sealed class TouchInputController : MonoBehaviour, ICommandSource
    {
        public VirtualJoystick moveStick;
        public TouchButton skillButton;
        public TouchButton reverseButton;

        public Command CurrentCommand { get; private set; } = Command.None;

        int _spinSign = 1;
        bool _skillQueued;

        public bool TakeSkillPress()
        {
            bool queued = _skillQueued;
            _skillQueued = false;
            return queued;
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
    /// 运行期搭 HUD：Canvas + EventSystem + 全屏摇杆可触层 + 两个按钮。
    /// 不依赖外部 UI 图，圆与方块都在代码里生成，保证 APK 里一定有。
    /// 层级顺序就是命中顺序：摇杆层在最下（铺满屏幕），按钮建在它之后，压在上面。
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
            controller.reverseButton = BuildButton(canvasGo.transform, "ReverseButton", new Vector2(-220f, 120f), new Color(0.85f, 0.85f, 0.9f, 0.35f));
            controller.skillButton = BuildButton(canvasGo.transform, "SkillButton", new Vector2(-110f, 80f), new Color(0.79f, 0.95f, 0.3f, 0.45f));

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

        static TouchButton BuildButton(Transform parent, string name, Vector2 anchored, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TouchButton));
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
            return go.GetComponent<TouchButton>();
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
