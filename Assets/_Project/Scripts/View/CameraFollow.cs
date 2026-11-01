using UnityEngine;
using Random = UnityEngine.Random;

namespace Lingjing.View
{
    /// <summary>
    /// 像素对齐的跟随相机：位置吸附到 1/32 单位，避免半像素抖动。
    /// 震屏只加整数像素并走不缩放时间——顿帧期间画面定住、震动继续，这一下才够「实」。
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        public BattleRunner runner;
        public float pixelsPerUnit = 32f;
        public float zOffset = -10f;

        float _shakeUntil;
        float _shakeSeconds;
        int _amplitude;

        /// <summary>震一下。<paramref name="pixels"/> 是最大偏移的像素数，按整数像素衰减。</summary>
        public void Shake(int pixels, float seconds)
        {
            if (pixels <= 0 || seconds <= 0f)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < _shakeUntil && pixels < _amplitude)
            {
                // 小震不打断大震，只延长一点点
                _shakeUntil = Mathf.Max(_shakeUntil, now + seconds * 0.5f);
                return;
            }

            _amplitude = pixels;
            _shakeSeconds = seconds;
            _shakeUntil = now + seconds;
        }

        void LateUpdate()
        {
            Vector3 position = transform.position;

            if (runner != null && runner.PlayerTransform != null)
            {
                Vector3 p = runner.PlayerTransform.position;
                position = new Vector3(Snap(p.x), Snap(p.y), zOffset);
            }

            int amplitude = CurrentAmplitude(out int ox, out int oy);
            if (amplitude > 0)
            {
                position.x += ox / pixelsPerUnit;
                position.y += oy / pixelsPerUnit;
            }

            transform.position = position;
        }

        /// <summary>当前的整像素偏移；返回振幅便于调试。</summary>
        int CurrentAmplitude(out int offsetX, out int offsetY)
        {
            float left = _shakeUntil - Time.unscaledTime;
            if (left <= 0f)
            {
                offsetX = 0;
                offsetY = 0;
                return 0;
            }

            int amplitude = Mathf.Max(1, Mathf.RoundToInt(_amplitude * (left / Mathf.Max(1e-3f, _shakeSeconds))));
            offsetX = Random.Range(-amplitude, amplitude + 1);
            offsetY = Random.Range(-amplitude, amplitude + 1);
            return amplitude;
        }

        float Snap(float v) => Mathf.Round(v * pixelsPerUnit) / pixelsPerUnit;
    }
}
