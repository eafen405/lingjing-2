using Lingjing.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingjing.Editor
{
    /// <summary>
    /// 触屏 HUD 的无头检查：全屏可触层、浮动底座、两个按钮与它们的排除热区。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.TouchProbe.Run
    /// </summary>
    public static class TouchProbe
    {
        public static void Run()
        {
            TouchInputController controller = TouchHud.Build();

            bool canvas = Object.FindObjectOfType<Canvas>() != null;
            bool eventSystem = Object.FindObjectOfType<EventSystem>() != null;
            VirtualJoystick stick = controller != null ? controller.moveStick : null;

            bool stickBuilt = stick != null && stick.ring != null && stick.knob != null;
            bool reverse = controller != null && controller.reverseButton != null;
            bool skill = controller != null && controller.skillButton != null;
            // 底座平时是隐藏的（浮动摇杆只在按下时出现），所以这里要连未激活的一起数。
            int images = Object.FindObjectsOfType<Image>(true).Length;

            // 可触层必须铺满屏幕，否则「按哪儿都能起摇杆」就不成立。
            var area = stick != null ? (RectTransform)stick.transform : null;
            bool fullScreen = area != null
                && area.anchorMin == Vector2.zero
                && area.anchorMax == Vector2.one
                && area.offsetMin == Vector2.zero
                && area.offsetMax == Vector2.zero;

            // 两个按钮都必须在排除列表里，且外扩余量是正数。
            bool blocked = stick != null
                && stick.blockers != null
                && stick.blockers.Length == 2
                && stick.blockers[0] != null
                && stick.blockers[1] != null
                && stick.blockerMargin > 0f;

            Debug.Log($"[touch] canvas={canvas} eventSystem={eventSystem} stick={stickBuilt} fullScreen={fullScreen} " +
                      $"blockers={blocked} reverse={reverse} skill={skill} images={images}");

            bool pass = canvas && eventSystem && stickBuilt && fullScreen && blocked && reverse && skill && images >= 5;
            Debug.Log(pass ? "[touch] PROBE PASS" : "[touch] PROBE FAIL");

            if (controller != null)
            {
                Object.DestroyImmediate(controller.gameObject);
            }

            var es = Object.FindObjectOfType<EventSystem>();
            if (es != null)
            {
                Object.DestroyImmediate(es.gameObject);
            }

            if (!pass)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
