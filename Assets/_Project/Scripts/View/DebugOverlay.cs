using System.Text;
using Lingjing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.View
{
    /// <summary>
    /// 调试叠层：FPS / 帧时间 / 同屏以骸数。默认在编辑器里常显，真机上默认关闭。
    /// 真机开关是「三指按住 1.5 秒」——不占 HUD，也不会被误触；编辑器里 F1 也能切。
    /// 读的都是不缩放时间，顿帧与暂停期间数字不会被 Time.timeScale 骗。
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        public const float RefreshSeconds = 0.25f;
        public const float ToggleHoldSeconds = 1.5f;

        public BattleRunner runner;

        Text _text;
        bool _visible;
        float _elapsed;
        int _frames;
        float _worstMs;
        float _touchHeld;
        readonly StringBuilder _builder = new(64);

        public static DebugOverlay Build(BattleRunner runner)
        {
            Canvas canvas = UiKit.CreateCanvas("DebugOverlay", 210);
            var overlay = canvas.gameObject.AddComponent<DebugOverlay>();
            overlay.runner = runner;
            overlay.Init(canvas.transform);
            return overlay;
        }

        void Init(Transform root)
        {
            _text = UiKit.CreateLabel(root, string.Empty, 18, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-HudView.SafeMargin, -96f),
                new Vector2(320f, 60f), new Color(0.79f, 0.95f, 0.3f, 0.9f));

#if UNITY_EDITOR
            _visible = true;
#else
            _visible = false;
#endif
            _text.gameObject.SetActive(_visible);
        }

        void Update()
        {
            HandleToggle();

            _elapsed += Time.unscaledDeltaTime;
            _frames++;
            _worstMs = Mathf.Max(_worstMs, Time.unscaledDeltaTime * 1000f);

            if (_elapsed < RefreshSeconds)
            {
                return;
            }

            if (_visible)
            {
                float fps = _frames / Mathf.Max(1e-4f, _elapsed);
                int enemies = 0;
                if (runner != null && runner.World != null)
                {
                    enemies = runner.World.World.Enemies.Count;
                }

                _builder.Clear();
                _builder.Append("FPS ").Append(Mathf.RoundToInt(fps))
                    .Append("  ·  ").Append((_elapsed * 1000f / Mathf.Max(1, _frames)).ToString("0.0")).Append("ms")
                    .Append("  ·  峰值 ").Append(_worstMs.ToString("0.0")).Append("ms")
                    .Append('\n').Append("以骸 ").Append(enemies);
                _text.text = _builder.ToString();
            }

            _elapsed = 0f;
            _frames = 0;
            _worstMs = 0f;
        }

        void HandleToggle()
        {
            bool toggle = Input.GetKeyDown(KeyCode.F1);

            if (Input.touchCount >= 3)
            {
                _touchHeld += Time.unscaledDeltaTime;
                if (_touchHeld >= ToggleHoldSeconds)
                {
                    _touchHeld = -ToggleHoldSeconds; // 松手前不重复触发
                    toggle = true;
                }
            }
            else
            {
                _touchHeld = Mathf.Min(0f, _touchHeld + Time.unscaledDeltaTime);
            }

            if (!toggle)
            {
                return;
            }

            _visible = !_visible;
            if (_text != null)
            {
                _text.gameObject.SetActive(_visible);
            }
        }
    }
}
