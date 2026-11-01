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

        /// <summary>跟随硬度：越大越贴手，10 约等于 0.1 秒收敛。硬锁会让世界像被从脚下抽走。</summary>
        public float sharpness = 10f;

        /// <summary>前瞻时长（秒）：按玩家速度往前看一点，跑动时看得见前面而不是一直盯着自己。</summary>
        public float lookAheadSeconds = 0.18f;

        /// <summary>前瞻的最大位移（单位），避免高速时把玩家推到屏幕边缘。</summary>
        public float maxLookAhead = 1.2f;

        float _shakeUntil;
        float _shakeSeconds;
        int _amplitude;
        Vector3 _smoothTarget;
        bool _hasTarget;

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

                Vector3 lead = Vector3.zero;
                if (lookAheadSeconds > 0f)
                {
                    Vector2 velocity = runner.PlayerVelocity;
                    lead = new Vector3(velocity.x * lookAheadSeconds, velocity.y * lookAheadSeconds, 0f);
                    float magnitude = lead.magnitude;
                    if (magnitude > maxLookAhead)
                    {
                        lead *= maxLookAhead / Mathf.Max(1e-4f, magnitude);
                    }
                }

                Vector3 desired = p + lead;
                if (!_hasTarget)
                {
                    _smoothTarget = desired;
                    _hasTarget = true;
                }

                // 指数收敛：帧率无关，顿帧期间（timeScale=0）也继续追，回来时不会有一下猛的位移。
                float k = 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime);
                _smoothTarget = Vector3.Lerp(_smoothTarget, desired, k);
                position = new Vector3(Snap(_smoothTarget.x), Snap(_smoothTarget.y), zOffset);
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
