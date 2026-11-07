using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingjing.View
{
    /// <summary>
    /// 触屏按钮：抬起前一直保持按下状态；PressedThisFrame 给「按一次触发一次」的动作
    /// （正反切）用，由读取方在同一帧内消费。
    /// 表现全部手写插值，不引 DOTween：按下缩放+提亮、技能就绪呼吸、充能不足偏红；
    /// 三条时间线都走 unscaled 时间，暂停与顿帧里玩家也看得见按钮的反应。
    /// </summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>按下到按到底（以及回弹）的手写插值时长。</summary>
        public const float PressSeconds = 0.06f;

        /// <summary>按到底时相对常态的缩放。</summary>
        public const float PressedScale = 0.92f;

        /// <summary>技能就绪呼吸的周期（秒）与幅度：缩放与透明度都在 1.0±0.06 里走。</summary>
        public const float ReadyPeriod = 1.2f;
        public const float ReadyAmplitude = 0.06f;

        /// <summary>就绪时整体提亮的透明度增量：45% 的绿圆单靠呼吸不容易被看见，先垫一层亮度。</summary>
        public const float ReadyAlphaBoost = 0.22f;

        /// <summary>拒绝反馈时长：充能不足时偏红一下，把「按到了但开不了」说清楚。</summary>
        public const float RejectSeconds = 0.25f;

        static readonly Color RejectTint = new Color(1f, 0.3f, 0.26f, 1f);

        public bool IsPressed { get; private set; }

        public bool PressedThisFrame { get; private set; }

        /// <summary>按钮上的短名标签（UiText.SkillButton / UiText.ReverseButton），由 TouchHud 用 UiKit.CreateLabel 挂上。</summary>
        public Text label;

        /// <summary>物理按下时发一次；控制器据此请求点击音效，探针也在这条路径上模拟按下。</summary>
        public event System.Action Pressed;

        Image _image;
        Color _baseColor = Color.white;
        Vector3 _baseScale = Vector3.one;
        float _press;
        bool _ready;
        float _rejectUntil;

        void Awake()
        {
            Bind();
        }

        /// <summary>
        /// 缓存常态外观。按钮是代码搭的，且批处理探针在编辑器里跑（Awake 不执行），
        /// 所以由 TouchHud 在配好色之后显式调一次；重复调用以当前外观为准。
        /// </summary>
        public void Bind()
        {
            _image = GetComponent<Image>();
            if (_image != null)
            {
                _baseColor = _image.color;
            }

            _baseScale = transform.localScale;
            ApplyVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
            PressedThisFrame = true;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
        }

        public void ConsumeFrame()
        {
            PressedThisFrame = false;
        }

        /// <summary>技能满槽且未在代行时打开呼吸提示；关掉时立即回到常态。</summary>
        public void SetReady(bool ready)
        {
            _ready = ready;
            EnsureBound();
            ApplyVisual();
        }

        /// <summary>充能不足的拒绝反馈：技能键短暂偏红。</summary>
        public void FlashRejected()
        {
            EnsureBound();
            _rejectUntil = Time.unscaledTime + RejectSeconds;
            ApplyVisual();
        }

        void Update()
        {
            if (_image == null)
            {
                return;
            }

            // 按下/回弹只在 0.06s 内跑完，读数取 unscaled，顿帧里也会走完。
            float target = IsPressed ? 1f : 0f;
            if (!Mathf.Approximately(_press, target))
            {
                _press = Mathf.MoveTowards(_press, target, Time.unscaledDeltaTime / PressSeconds);
            }

            ApplyVisual();
        }

        void EnsureBound()
        {
            if (_image == null)
            {
                Bind();
            }
        }

        void ApplyVisual()
        {
            if (_image == null)
            {
                return;
            }

            // 呼吸用 unscaled 时间：暂停/顿帧里「技能好了」也必须一眼看见。
            float breathe = _ready
                ? Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / ReadyPeriod)) * ReadyAmplitude
                : 0f;

            Color color = Color.Lerp(_baseColor, PressedColor(), _press);
            if (_ready)
            {
                color.a = Mathf.Clamp01((_baseColor.a + ReadyAlphaBoost) * (1f + breathe));
            }

            if (Time.unscaledTime < _rejectUntil)
            {
                // 拒绝反馈压过其它状态：偏红 0.25s，提示「这个键刚才确实被按到了」。
                color = Color.Lerp(color, RejectTint, 0.7f);
                color.a = Mathf.Max(color.a, _baseColor.a + 0.2f);
            }

            transform.localScale = _baseScale * ((1f + breathe) * Mathf.Lerp(1f, PressedScale, _press));
            _image.color = color;
        }

        Color PressedColor()
        {
            Color pressed = Color.Lerp(_baseColor, Color.white, 0.28f);
            pressed.a = Mathf.Clamp01(_baseColor.a + 0.18f);
            return pressed;
        }
    }
}
