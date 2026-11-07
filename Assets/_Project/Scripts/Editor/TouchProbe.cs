using Lingjing.UI;
using Lingjing.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lingjing.Editor
{
    /// <summary>
    /// 触屏 HUD 的无头检查：全屏可触层、浮动底座、两个按钮与它们的排除热区，
    /// 外加 P0 的三件事——按钮有没有短名、技能就绪/回收会不会改可见状态、按下会不会发点击音。
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

            TouchButton skillButton = skill ? controller.skillButton : null;
            TouchButton reverseButton = reverse ? controller.reverseButton : null;
            Image skillImage = skillButton != null ? skillButton.GetComponent<Image>() : null;

            // 1) 按钮身份：两个圆上都压着短名文本，文本不挡射线、父节点就是按钮。
            bool labeled = HasLabel(skillButton, UiText.SkillButton)
                && HasLabel(reverseButton, UiText.ReverseButton);

            // 2) 安全区：按钮收在 full-stretch 的 SafeArea 里；摇杆层不能跟着收，必须保持全屏。
            var safeArea = skillButton != null ? skillButton.transform.parent as RectTransform : null;
            bool safeOk = safeArea != null
                && safeArea.GetComponent<UiSafeArea>() != null
                && safeArea.anchorMin == Vector2.zero
                && safeArea.anchorMax == Vector2.one
                && safeArea.offsetMin == Vector2.zero
                && safeArea.offsetMax == Vector2.zero
                && reverseButton != null
                && reverseButton.transform.parent == safeArea
                && area != null
                && !area.IsChildOf(safeArea);

            // 3) 技能就绪提示：开关必须立刻体现在颜色或缩放上，且关掉后回到常态。
            bool readyVisible = false;
            bool readyRestored = false;
            if (skillButton != null && skillImage != null)
            {
                Color baseColor = skillImage.color;
                Vector3 baseScale = skillButton.transform.localScale;

                controller.SetSkillReady(true);
                Color readyColor = skillImage.color;
                Vector3 readyScale = skillButton.transform.localScale;

                controller.SetSkillReady(false);
                Color resetColor = skillImage.color;
                Vector3 resetScale = skillButton.transform.localScale;

                readyVisible = !SameColor(readyColor, baseColor) || !SameScale(readyScale, baseScale);
                readyRestored = SameColor(resetColor, baseColor) && SameScale(resetScale, baseScale);
            }

            // 4) 代行交还控制权：方向同步后，下一次指令必须已经是世界里的方向；0 视为不知道、不改。
            bool spinSynced = false;
            if (controller != null)
            {
                controller.SyncSpinSign(-1);
                bool synced = controller.CurrentCommand.SpinSign == -1;
                controller.SyncSpinSign(0);
                spinSynced = synced && controller.CurrentCommand.SpinSign == -1;
            }

            // 5) 按下必须发点击音：两个按钮各模拟一次按下，各触发一次 SfxRequested。
            int sfxCount = 0;
            int afterSkill = 0;
            bool sfxIdOk = true;
            if (controller != null)
            {
                System.Action<string> onSfx = id =>
                {
                    sfxCount++;
                    if (id != TouchInputController.ClickSfx)
                    {
                        sfxIdOk = false;
                    }
                };

                controller.SfxRequested += onSfx;
                var pointer = new PointerEventData(Object.FindObjectOfType<EventSystem>());
                PressAndRelease(skillButton, pointer);
                afterSkill = sfxCount;
                PressAndRelease(reverseButton, pointer);
                controller.SfxRequested -= onSfx;
            }

            bool sfx = afterSkill >= 1 && sfxCount >= 2 && sfxIdOk;

            Debug.Log($"[touch] canvas={canvas} eventSystem={eventSystem} stick={stickBuilt} fullScreen={fullScreen} " +
                      $"blockers={blocked} reverse={reverse} skill={skill} images={images}");
            Debug.Log($"[touch] labeled={labeled} safeArea={safeOk} ready={readyVisible}/{readyRestored} " +
                      $"spin={spinSynced} sfx={sfxCount}");

            bool pass = canvas && eventSystem && stickBuilt && fullScreen && blocked && reverse && skill && images >= 5
                && labeled && safeOk && readyVisible && readyRestored && spinSynced && sfx;
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

        static bool HasLabel(TouchButton button, string text)
        {
            Text label = button != null ? button.label : null;
            return label != null
                && label.text == text
                && !label.raycastTarget
                && label.transform.IsChildOf(button.transform)
                && label.fontSize >= 18;
        }

        static void PressAndRelease(TouchButton button, PointerEventData pointer)
        {
            if (button == null)
            {
                return;
            }

            // 直接走事件回调：不依赖 EventSystem 的点击判定，批处理里也能复现按下。
            button.OnPointerDown(pointer);
            button.OnPointerUp(pointer);
            button.ConsumeFrame();
        }

        static bool SameColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 1e-4f
                && Mathf.Abs(a.g - b.g) < 1e-4f
                && Mathf.Abs(a.b - b.b) < 1e-4f
                && Mathf.Abs(a.a - b.a) < 1e-4f;
        }

        static bool SameScale(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 1e-6f;
        }
    }
}
