using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingjing.View
{
    /// <summary>
    /// 浮动摇杆：整块可触区铺满屏幕（右下按钮与右上暂停除外），按下点就是摇杆中心。
    /// 为什么不做固定底座：横屏双手握持时拇指每次落点都不一样，固定底座会把「想走」变成「找摇杆」。
    /// 松手即回中，底座淡出隐藏；同一时刻只认一根手指，其他手指不抢摇杆。
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>底座（含底圈与摇杆头），按下时移动到触点，松手后淡出。</summary>
        public RectTransform ring;

        /// <summary>底座内部跟手的圆点。</summary>
        public RectTransform knob;

        /// <summary>这些热区里按下不激活摇杆（右下两个按钮）。判定时会各自外扩 <see cref="blockerMargin"/>。</summary>
        public RectTransform[] blockers;

        /// <summary>摇杆头最大位移（参考像素）。</summary>
        public float radiusPixels = 90f;

        /// <summary>死区（占半径的比例）：手指动一点点不该让角色抖动。</summary>
        public float deadZone = 0.1f;

        /// <summary>松手后底座淡出时长。</summary>
        public float fadeSeconds = 0.15f;

        /// <summary>按钮热区外扩，避免贴着按钮边缘按就起摇杆。</summary>
        public float blockerMargin = 28f;

        const int NoPointer = int.MinValue;

        RectTransform _area;
        Image _ringImage;
        Image _knobImage;
        Color _ringColor;
        Color _knobColor;
        Vector2 _baseLocal;
        int _pointer = NoPointer;
        float _alpha;

        /// <summary>归一化移动方向，已扣死区，长按不出界。</summary>
        public Vector2 Value { get; private set; }

        public bool Held => _pointer != NoPointer;

        void Awake()
        {
            _area = (RectTransform)transform;
        }

        /// <summary>
        /// 组件是先于子节点建的（AddComponent 立刻跑 Awake），所以底座与摇杆头要在建好之后
        /// 显式绑一次：缓存两张 Image 的颜色，并先收成隐藏态。
        /// </summary>
        public void Bind()
        {
            if (ring != null)
            {
                _ringImage = ring.GetComponent<Image>();
                _ringColor = _ringImage != null ? _ringImage.color : Color.white;
            }

            if (knob != null)
            {
                _knobImage = knob.GetComponent<Image>();
                _knobColor = _knobImage != null ? _knobImage.color : Color.white;
            }

            SetAlpha(0f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointer != NoPointer || Blocked(eventData))
            {
                return;
            }

            _pointer = eventData.pointerId;
            _baseLocal = ToLocal(eventData);

            if (ring != null)
            {
                ring.localPosition = _baseLocal;
                ring.gameObject.SetActive(true);
            }

            if (knob != null)
            {
                knob.anchoredPosition = Vector2.zero;
            }

            Value = Vector2.zero;
            SetAlpha(1f);
            UpdateValue(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointer)
            {
                return;
            }

            UpdateValue(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointer)
            {
                return;
            }

            _pointer = NoPointer;
            Value = Vector2.zero;

            if (knob != null)
            {
                knob.anchoredPosition = Vector2.zero;
            }
        }

        void Update()
        {
            // 松手后底座要淡出：用不缩放时间，暂停/顿帧时也能收干净。
            float target = _pointer == NoPointer ? 0f : 1f;
            if (!Mathf.Approximately(_alpha, target))
            {
                _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime / Mathf.Max(1e-3f, fadeSeconds));
                SetAlpha(_alpha);
            }
        }

        void UpdateValue(PointerEventData eventData)
        {
            Vector2 local = ToLocal(eventData);
            Vector2 delta = Vector2.ClampMagnitude(local - _baseLocal, radiusPixels);

            if (knob != null)
            {
                knob.anchoredPosition = delta;
            }

            float magnitude = radiusPixels <= 0f ? 0f : delta.magnitude / radiusPixels;
            if (magnitude <= deadZone)
            {
                Value = Vector2.zero;
                return;
            }

            // 死区之外重新映射到 0..1，手感上没有跳变。
            Value = delta.normalized * ((magnitude - deadZone) / (1f - deadZone));
        }

        Vector2 ToLocal(PointerEventData eventData)
        {
            if (_area == null)
            {
                _area = (RectTransform)transform;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _area, eventData.position, eventData.pressEventCamera, out Vector2 local);
            return local;
        }

        /// <summary>触点是否落在排除热区里（按钮本体 + 外扩余量）。</summary>
        bool Blocked(PointerEventData eventData)
        {
            if (blockers == null)
            {
                return false;
            }

            for (int i = 0; i < blockers.Length; i++)
            {
                RectTransform blocker = blockers[i];
                if (blocker == null)
                {
                    continue;
                }

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        blocker, eventData.position, eventData.pressEventCamera, out Vector2 local))
                {
                    continue;
                }

                Rect rect = blocker.rect;
                rect.xMin -= blockerMargin;
                rect.xMax += blockerMargin;
                rect.yMin -= blockerMargin;
                rect.yMax += blockerMargin;
                if (rect.Contains(local))
                {
                    return true;
                }
            }

            return false;
        }

        void SetAlpha(float alpha)
        {
            _alpha = alpha;

            if (ring != null && _ringImage != null)
            {
                _ringImage.color = new Color(_ringColor.r, _ringColor.g, _ringColor.b, _ringColor.a * alpha);
            }

            if (knob != null && _knobImage != null)
            {
                _knobImage.color = new Color(_knobColor.r, _knobColor.g, _knobColor.b, _knobColor.a * alpha);
            }

            if (ring != null && ring.gameObject.activeSelf != alpha > 0.001f)
            {
                ring.gameObject.SetActive(alpha > 0.001f);
            }
        }
    }
}
