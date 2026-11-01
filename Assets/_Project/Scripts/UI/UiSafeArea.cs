using UnityEngine;

namespace Lingjing.UI
{
    /// <summary>
    /// 把界面内容收进屏幕安全区（刘海 / 挖孔 / 圆角）。只在设备上真正生效：
    /// 桌面上 <c>Screen.safeArea</c> 就是整屏，锚点回到 0..1，编辑器布局与探针断言不受影响。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiSafeArea : MonoBehaviour
    {
        RectTransform _rect;
        Rect _applied = new Rect(-1f, -1f, -1f, -1f);

        void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != _applied)
            {
                Apply();
            }
        }

        void Apply()
        {
            if (_rect == null)
            {
                return;
            }

            _applied = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            _rect.anchorMin = new Vector2(_applied.xMin / Screen.width, _applied.yMin / Screen.height);
            _rect.anchorMax = new Vector2(_applied.xMax / Screen.width, _applied.yMax / Screen.height);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
