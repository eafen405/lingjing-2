using Lingjing.Fairy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingjing.View
{
    /// <summary>
    /// 触屏指令源：左下摇杆给移动，右下两个按钮给技能与正反切。
    /// 编辑器里鼠标就是手指，桌面和手机走同一条代码路径。
    /// </summary>
    public sealed class TouchInputController : MonoBehaviour, ICommandSource
    {
        public VirtualJoystick moveStick;
        public TouchButton skillButton;
        public TouchButton reverseButton;

        public Command CurrentCommand { get; private set; } = Command.None;

        int _spinSign = 1;

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
                skillButton.ConsumeFrame();
            }

            CurrentCommand = new Command(new Lingjing.Core.Vec2(move.x, move.y), _spinSign, reverse);
        }
    }

    /// <summary>
    /// 运行期搭 HUD：Canvas + EventSystem + 摇杆 + 两个按钮。
    /// 不依赖外部 UI 图，圆与方块都在代码里生成，保证 APK 里一定有。
    /// </summary>
    public static class TouchHud
    {
        static Sprite _circle;
        static Sprite _square;

        public static TouchInputController Build()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;

            _circle ??= MakeCircle(96);
            _square ??= MakeSquare(8);

            var controller = canvasGo.AddComponent<TouchInputController>();
            controller.moveStick = BuildJoystick(canvasGo.transform);
            controller.reverseButton = BuildButton(canvasGo.transform, "ReverseButton", new Vector2(-220f, 120f), new Color(0.85f, 0.85f, 0.9f, 0.35f));
            controller.skillButton = BuildButton(canvasGo.transform, "SkillButton", new Vector2(-110f, 80f), new Color(0.79f, 0.95f, 0.3f, 0.45f));

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

        static VirtualJoystick BuildJoystick(Transform parent)
        {
            var rootGo = new GameObject("Joystick", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
            var root = (RectTransform)rootGo.transform;
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(150f, 130f);
            root.sizeDelta = new Vector2(200f, 200f);

            var baseImage = rootGo.GetComponent<Image>();
            baseImage.sprite = _circle;
            baseImage.color = new Color(1f, 1f, 1f, 0.18f);
            baseImage.raycastTarget = true;

            var knobGo = new GameObject("Knob", typeof(RectTransform), typeof(Image));
            var knob = (RectTransform)knobGo.transform;
            knob.SetParent(root, false);
            knob.sizeDelta = new Vector2(84f, 84f);
            var knobImage = knobGo.GetComponent<Image>();
            knobImage.sprite = _circle;
            knobImage.color = new Color(0.86f, 0.9f, 0.95f, 0.55f);
            knobImage.raycastTarget = false;

            var stick = rootGo.GetComponent<VirtualJoystick>();
            stick.knob = knob;
            stick.radiusPixels = 90f;
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

        static Sprite MakeSquare(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
