using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.UI
{
    /// <summary>
    /// 运行期 UI 构件工厂。界面在代码里搭，不依赖 Prefab 与美术切片，
    /// 保证任何场景（含无头构建出来的）都能直接长出同样的界面。
    /// 尺寸一律按 960x540 参考分辨率给，CanvasScaler 负责缩放到真机。
    /// </summary>
    public static class UiKit
    {
        public const int RefWidth = 960;
        public const int RefHeight = 540;

        static Font _font;
        static Sprite _rounded;
        static Sprite _circle;
        static Sprite _white;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.Load<Font>("Fonts/NotoSansSC-UI");
                    if (_font == null)
                    {
                        Debug.LogWarning("[ui] 找不到字体 Resources/Fonts/NotoSansSC-UI，回退内置字体（中文会缺字）");
                    }
                }

                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        public static RectTransform CreatePanel(
            Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPos, Vector2 size, Color color, bool rounded = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = rounded ? Rounded : White;
            image.type = rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return rect;
        }

        public static RectTransform CreateFullScreen(Transform parent, string name, Color color, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = White;
            image.color = color;
            image.raycastTarget = true;
            return rect;
        }

        public static Text CreateLabel(
            Transform parent, string text, int fontSize, TextAnchor anchor,
            Vector2 anchorPoint, Vector2 pivot, Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorPoint;
            rect.anchorMax = anchorPoint;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var label = go.GetComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = anchor;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Text CreateStyledLabel(Transform parent, string text, int fontSize, TextAnchor anchor,
            Vector2 anchorPoint, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            return CreateLabel(parent, text, fontSize, anchor, anchorPoint, pivot, anchoredPos, size, new Color(0.92f, 0.94f, 0.96f, 1f));
        }

        public static Button CreateButton(
            Transform parent, string label, Vector2 anchorPoint, Vector2 pivot,
            Vector2 anchoredPos, Vector2 size, Action onClick, int fontSize = 26)
        {
            RectTransform rect = CreatePanel(parent, "Button_" + label, anchorPoint, pivot, anchoredPos, size,
                new Color(0.16f, 0.21f, 0.26f, 0.92f));
            rect.gameObject.name = "Button_" + label;

            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            CreateLabel(rect, label, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(0.94f, 0.96f, 0.98f, 1f));

            return button;
        }

        public static UiBar CreateBar(Transform parent, Vector2 anchorPoint, Vector2 pivot, Vector2 anchoredPos,
            Vector2 size, Color background, Color fillColor)
        {
            RectTransform bg = CreatePanel(parent, "Bar", anchorPoint, pivot, anchoredPos, size, background, rounded: false);
            bg.GetComponent<Image>().raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = (RectTransform)fillGo.transform;
            fill.SetParent(bg, false);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);

            var fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = White;
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;

            var bar = bg.gameObject.AddComponent<UiBar>();
            bar.fill = fill;
            return bar;
        }

        public static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    _white = MakeSolid(4, Color.white);
                }

                return _white;
            }
        }

        public static Sprite Rounded
        {
            get
            {
                if (_rounded == null)
                {
                    _rounded = MakeRounded(32, 8);
                }

                return _rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                {
                    _circle = MakeCircle(96);
                }

                return _circle;
            }
        }

        static Sprite MakeSolid(int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0f;
                    float dy = 0f;
                    if (x < radius) dx = radius - x - 0.5f;
                    else if (x >= size - radius) dx = x - (size - radius) + 0.5f;
                    if (y < radius) dy = radius - y - 0.5f;
                    else if (y >= size - radius) dy = y - (size - radius) + 0.5f;

                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
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
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>一条进度条：0..1 直接改 fill 的锚点宽度，不重建对象。</summary>
    public sealed class UiBar : MonoBehaviour
    {
        public RectTransform fill;

        public void SetValue(float t)
        {
            if (fill == null)
            {
                return;
            }

            fill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
        }
    }
}
