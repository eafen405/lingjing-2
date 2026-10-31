using UnityEngine;
using UnityEngine.EventSystems;

namespace Lingjing.View
{
    /// <summary>
    /// 触屏按钮：抬起前一直保持按下状态；PressedThisFrame 给「按一次触发一次」的动作
    /// （正反切）用，由读取方在同一帧内消费。
    /// </summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool IsPressed { get; private set; }

        public bool PressedThisFrame { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
            PressedThisFrame = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
        }

        public void ConsumeFrame()
        {
            PressedThisFrame = false;
        }
    }
}
