using Lingjing.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingjing.Editor
{
    /// <summary>
    /// 触屏 HUD 的无头检查：能不能在没有任何外部 UI 图的情况下把 Canvas、摇杆、两个按钮搭出来。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.TouchProbe.Run
    /// </summary>
    public static class TouchProbe
    {
        public static void Run()
        {
            TouchInputController controller = TouchHud.Build();

            bool canvas = Object.FindObjectOfType<Canvas>() != null;
            bool eventSystem = Object.FindObjectOfType<EventSystem>() != null;
            bool stick = controller != null && controller.moveStick != null;
            bool knob = stick && controller.moveStick.knob != null;
            bool reverse = controller != null && controller.reverseButton != null;
            bool skill = controller != null && controller.skillButton != null;
            int images = Object.FindObjectsOfType<Image>().Length;

            Debug.Log($"[touch] canvas={canvas} eventSystem={eventSystem} stick={stick} knob={knob} reverse={reverse} skill={skill} images={images}");

            bool pass = canvas && eventSystem && stick && knob && reverse && skill && images >= 4;
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
