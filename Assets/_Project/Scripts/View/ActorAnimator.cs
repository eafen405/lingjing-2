using System;
using Lingjing.Core;
using UnityEngine;

namespace Lingjing.View
{
    /// <summary>
    /// 精灵动画播放器：吃一个 ArtLibrary + 实体名，按「状态 + 8 向」选帧。
    /// 缺朝向时走镜像（manifest 的 mirror 表），最后退回第 0 行并翻转。
    /// </summary>
    public sealed class ActorAnimator : MonoBehaviour
    {
        public SpriteRenderer target;

        ArtLibrary.EntityClips _entity;
        ArtLibrary.StateClips _state;
        string _stateName = "";
        int _row;
        bool _flip;
        float _time;
        int _frame;
        bool _finished;

        public bool Finished => _finished;

        public void Init(ArtLibrary library, string entityName, string initialState)
        {
            if (target == null)
            {
                target = GetComponent<SpriteRenderer>();
            }

            _entity = library != null ? library.EntityOf(entityName) : null;
            if (_entity == null)
            {
                Debug.LogWarning($"[anim] 找不到实体动画：{entityName}");
                return;
            }

            SetState(initialState);
        }

        public void SetState(string state)
        {
            if (_entity == null || _stateName == state)
            {
                return;
            }

            _stateName = state;
            _state = _entity.StateOf(state);
            _time = 0f;
            _frame = 0;
            _finished = false;
            Apply();
        }

        /// <summary>按移动方向选朝向：只有 8 向素材的实体才真正用斜向。</summary>
        public void Face(Vec2 velocity)
        {
            if (_entity == null || _state == null)
            {
                return;
            }

            string dir = DirectionName(velocity.X, velocity.Y);
            int row = _state.RowOf(dir);
            bool flip = false;

            if (row < 0)
            {
                string mirror = _entity.MirrorOf(dir);
                if (!string.IsNullOrEmpty(mirror))
                {
                    row = _state.RowOf(mirror);
                    flip = row >= 0;
                }
            }

            if (row < 0)
            {
                row = 0;
                flip = velocity.X < 0;
            }

            if (row != _row || flip != _flip)
            {
                _row = row;
                _flip = flip;
                Apply();
            }
        }

        void Update()
        {
            if (_state == null || _state.fps <= 0f || _finished)
            {
                return;
            }

            ArtLibrary.Clip clip = ClipAt(_row);
            int count = clip.frames.Length;
            if (count <= 1)
            {
                return;
            }

            _time += Time.deltaTime;
            float frameTime = 1f / _state.fps;
            while (_time >= frameTime)
            {
                _time -= frameTime;
                _frame++;
                if (_frame >= count)
                {
                    if (_state.loop)
                    {
                        _frame = 0;
                    }
                    else
                    {
                        _frame = count - 1;
                        _finished = true;
                    }
                }
            }

            Apply();
        }

        void Apply()
        {
            ArtLibrary.Clip clip = ClipAt(_row);
            if (clip == null || clip.frames.Length == 0 || target == null)
            {
                return;
            }

            int index = Mathf.Clamp(_frame, 0, clip.frames.Length - 1);
            target.sprite = clip.frames[index];
            target.flipX = _flip;
        }

        /// <summary>某个状态播完一轮的时长，给尸体这类「播完自毁」用。</summary>
        public float Duration(string state)
        {
            if (_entity == null)
            {
                return 0.5f;
            }

            ArtLibrary.StateClips clips = _entity.StateOf(state);
            if (clips == null || clips.rows.Length == 0 || clips.rows[0].frames.Length == 0 || clips.fps <= 0f)
            {
                return 0.5f;
            }

            return clips.rows[0].frames.Length / clips.fps;
        }

        ArtLibrary.Clip ClipAt(int row)        {
            if (_state == null || _state.rows.Length == 0)
            {
                return null;
            }

            return _state.rows[Mathf.Clamp(row, 0, _state.rows.Length - 1)];
        }

        /// <summary>8 向量化：22.5° 分界，速度低于阈值时保持上一次的朝向由调用方控制。</summary>
        public static string DirectionName(double x, double y)
        {
            if (Math.Abs(x) < 1e-6 && Math.Abs(y) < 1e-6)
            {
                return "s";
            }

            double angle = Math.Atan2(y, x) * 180.0 / Math.PI;
            if (angle < 0)
            {
                angle += 360.0;
            }

            // 0° = 正东，逆时针增加
            if (angle >= 337.5 || angle < 22.5) return "e";
            if (angle < 67.5) return "ne";
            if (angle < 112.5) return "n";
            if (angle < 157.5) return "nw";
            if (angle < 202.5) return "w";
            if (angle < 247.5) return "sw";
            if (angle < 292.5) return "s";
            return "se";
        }
    }
}
