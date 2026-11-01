using System.Collections.Generic;
using UnityEngine;

namespace Lingjing.View
{
    /// <summary>
    /// 一次性特效的对象池：命中火花、击杀碎裂、拾取、代行进出都走这里。
    /// 一个 <see cref="Step"/> 驱动所有槽，不给每个特效挂 MonoBehaviour；
    /// 每帧最多放 <see cref="MaxPerFrame"/> 个，避免十几只以骸同时贴环时刷屏。
    /// </summary>
    public sealed class EffectPool
    {
        /// <summary>同屏上限。超过就复用最旧的槽（视觉上本来就叠在一起）。</summary>
        public const int Capacity = 24;

        /// <summary>每帧最多新放几个；命中频率高的时候靠它压住画面噪音。</summary>
        public const int MaxPerFrame = 2;

        readonly ArtLibrary _library;
        readonly Transform _root;
        readonly List<Slot> _slots = new(Capacity);
        int _next;
        int _frame = -1;
        int _playedThisFrame;

        sealed class Slot
        {
            public Transform tx;
            public SpriteRenderer sr;
            public ArtLibrary.EffectClips clip;
            public float time;
            public int frame;
            public bool playing;
        }

        public EffectPool(Transform parent, ArtLibrary library)
        {
            _library = library;
            var rootGo = new GameObject("Fx");
            _root = rootGo.transform;
            _root.SetParent(parent, false);
        }

        /// <summary>放一个特效。名字是 effects.json 里的组名（hit / break / pickup / proxy）。</summary>
        public bool Play(string name, Vector3 position, float scale = 1f)
        {
            ArtLibrary.EffectClips clip = _library != null ? _library.EffectOf(name) : null;
            if (clip == null || clip.frames.Length == 0)
            {
                return false;
            }

            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                _playedThisFrame = 0;
            }

            if (_playedThisFrame >= MaxPerFrame)
            {
                return false;
            }

            _playedThisFrame++;

            Slot slot = Take();
            slot.clip = clip;
            slot.time = 0f;
            slot.frame = 0;
            slot.playing = true;
            slot.tx.position = position;
            slot.tx.localScale = new Vector3(scale, scale, 1f);
            slot.tx.gameObject.SetActive(true);
            slot.sr.sprite = clip.frames[0];
            return true;
        }

        Slot Take()
        {
            if (_slots.Count < Capacity)
            {
                var go = new GameObject("Fx" + _slots.Count);
                go.transform.SetParent(_root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 13;
                go.SetActive(false);

                var slot = new Slot { tx = go.transform, sr = sr };
                _slots.Add(slot);
                return slot;
            }

            // 池满了：从游标开始找空闲槽，找不到就抢最旧的那个。
            for (int i = 0; i < Capacity; i++)
            {
                Slot candidate = _slots[(_next + i) % Capacity];
                if (!candidate.playing)
                {
                    _next = (_next + i + 1) % Capacity;
                    return candidate;
                }
            }

            Slot victim = _slots[_next];
            _next = (_next + 1) % Capacity;
            return victim;
        }

        /// <summary>按帧推进。传 Time.deltaTime 让特效跟着顿帧一起停。</summary>
        public void Step(float dt)
        {
            float scale = 1f;
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                if (!slot.playing)
                {
                    continue;
                }

                ArtLibrary.EffectClips clip = slot.clip;
                slot.time += dt;

                if (clip.fps > 0f)
                {
                    int frameCount = clip.frames.Length;
                    scale = 1f / clip.fps;
                    int frame = (int)(slot.time / scale);
                    if (frame >= frameCount)
                    {
                        if (clip.loop)
                        {
                            frame %= frameCount;
                        }
                        else
                        {
                            slot.playing = false;
                            slot.tx.gameObject.SetActive(false);
                            continue;
                        }
                    }

                    if (frame != slot.frame)
                    {
                        slot.frame = frame;
                        slot.sr.sprite = clip.frames[frame];
                    }
                }
                else if (slot.time > 0.08f)
                {
                    // fps = 0 的组（警示区）没有动画，播一个短闪就收。
                    slot.playing = false;
                    slot.tx.gameObject.SetActive(false);
                }
            }
        }
    }
}
