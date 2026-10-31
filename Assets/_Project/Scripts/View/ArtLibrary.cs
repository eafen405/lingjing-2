using System;
using UnityEngine;

namespace Lingjing.View
{
    /// <summary>
    /// 精灵库：按「实体 / 状态 / 朝向」索引到帧序列。由 ArtLibraryBuilder 从 Art 目录下的
    /// manifest 生成，运行时不读文件、不依赖 AssetDatabase。
    /// </summary>
    [CreateAssetMenu(menuName = "Lingjing/Art Library", fileName = "ArtLibrary")]
    public sealed class ArtLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Clip
        {
            public Sprite[] frames = Array.Empty<Sprite>();
        }

        [Serializable]
        public sealed class StateClips
        {
            public string state = "";
            public float fps = 12f;
            public bool loop = true;
            public string[] dirNames = Array.Empty<string>();
            public Clip[] rows = Array.Empty<Clip>();

            public int RowOf(string dir)
            {
                for (int i = 0; i < dirNames.Length; i++)
                {
                    if (dirNames[i] == dir)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        [Serializable]
        public sealed class EntityClips
        {
            public string entity = "";
            public StateClips[] states = Array.Empty<StateClips>();
            public string[] mirrorFrom = Array.Empty<string>();
            public string[] mirrorTo = Array.Empty<string>();

            public StateClips StateOf(string state)
            {
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i].state == state)
                    {
                        return states[i];
                    }
                }

                return states.Length > 0 ? states[0] : null;
            }

            public string MirrorOf(string dir)
            {
                for (int i = 0; i < mirrorFrom.Length; i++)
                {
                    if (mirrorFrom[i] == dir)
                    {
                        return mirrorTo[i];
                    }
                }

                return null;
            }
        }

        public EntityClips[] entities = Array.Empty<EntityClips>();

        public EntityClips EntityOf(string entity)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i].entity == entity)
                {
                    return entities[i];
                }
            }

            return null;
        }
    }
}
