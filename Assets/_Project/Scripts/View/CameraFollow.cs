using UnityEngine;

namespace Lingjing.View
{
    /// <summary>像素对齐的跟随相机：位置吸附到 1/32 单位，避免半像素抖动。</summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        public BattleRunner runner;
        public float pixelsPerUnit = 32f;
        public float zOffset = -10f;

        void LateUpdate()
        {
            if (runner == null || runner.PlayerTransform == null)
            {
                return;
            }

            Vector3 p = runner.PlayerTransform.position;
            transform.position = new Vector3(Snap(p.x), Snap(p.y), zOffset);
        }

        float Snap(float v) => Mathf.Round(v * pixelsPerUnit) / pixelsPerUnit;
    }
}
