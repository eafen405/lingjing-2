using UnityEngine;
using UnityEngine.EventSystems;

namespace Lingjing.View
{
    /// <summary>
    /// 虚拟摇杆：按下 / 拖动 / 抬起，输出 -1..1 的方向。
    /// 用 RectTransformUtility 把屏幕坐标换算到自身局部坐标，松手即回中。
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        public float radiusPixels = 90f;

        RectTransform _area;

        public Vector2 Value { get; private set; }

        void Awake()
        {
            _area = (RectTransform)transform;
        }

        public void OnPointerDown(PointerEventData eventData) => UpdateValue(eventData);

        public void OnDrag(PointerEventData eventData) => UpdateValue(eventData);

        public void OnPointerUp(PointerEventData eventData)
        {
            Value = Vector2.zero;
            if (knob != null)
            {
                knob.anchoredPosition = Vector2.zero;
            }
        }

        void UpdateValue(PointerEventData eventData)
        {
            if (_area == null)
            {
                _area = (RectTransform)transform;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _area, eventData.position, eventData.pressEventCamera, out Vector2 local);

            Vector2 clamped = Vector2.ClampMagnitude(local, radiusPixels);
            if (knob != null)
            {
                knob.anchoredPosition = clamped;
            }

            Value = radiusPixels <= 0f ? Vector2.zero : clamped / radiusPixels;
        }
    }
}
