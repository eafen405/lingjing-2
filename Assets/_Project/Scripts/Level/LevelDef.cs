using System;

namespace Lingjing.Level
{
    /// <summary>
    /// 关卡数据。字段名就是 levels.json 的键名（JsonUtility 只认公有字段，且不认字段初始化器，
    /// 所以取值校验放在 <see cref="LevelField"/> 里做，不依赖这里的默认值）。
    /// 中文文案不放在数据里：文案的唯一来源是 UiText。
    /// </summary>
    [Serializable]
    public sealed class LevelDef
    {
        /// <summary>关卡序号，与 GameFlow 的顺序一致，从 1 开始。</summary>
        public int id;

        /// <summary>场地宽（格）。设计口径 96。</summary>
        public int mapWidth;

        /// <summary>场地高（格）。设计口径 54。</summary>
        public int mapHeight;

        /// <summary>边界墙厚度（格）。</summary>
        public int wallThickness;

        /// <summary>场内障碍矩形（墙角掩体、挡路物）。</summary>
        public RectDef[] obstacles;

        /// <summary>每节开始时刷新到这些固定点的补给（§7.3「每节 2–3 个固定补给点」）。</summary>
        public SupplyDef[] supplies;

        /// <summary>节，按顺序推进；最后一节的最后一波应含恶名以骸。</summary>
        public SectionDef[] sections;
    }

    [Serializable]
    public sealed class SectionDef
    {
        /// <summary>本节的波次，清空当前波才投下一波。</summary>
        public WaveDef[] waves;
    }

    [Serializable]
    public sealed class WaveDef
    {
        public UnitDef[] units;
    }

    [Serializable]
    public sealed class UnitDef
    {
        /// <summary>EnemyKind 的名字，映射见 <see cref="EnemyKindNames"/>。</summary>
        public string kind;

        public int count;
    }

    [Serializable]
    public sealed class RectDef
    {
        public int x;
        public int y;
        public int w;
        public int h;
    }

    [Serializable]
    public sealed class SupplyDef
    {
        /// <summary>格坐标；实际落点是格中心。</summary>
        public int x;
        public int y;

        /// <summary>"Health" 或 "Energy"，映射见 <see cref="SupplyKindNames"/>。</summary>
        public string kind;
    }

    /// <summary>levels.json 的根对象。JsonUtility 不支持顶层数组，所以包一层。</summary>
    [Serializable]
    public sealed class LevelCatalog
    {
        public LevelDef[] levels;

        /// <summary>按序号取关卡；找不到返回 null。</summary>
        public LevelDef Find(int id)
        {
            if (levels == null)
            {
                return null;
            }

            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] != null && levels[i].id == id)
                {
                    return levels[i];
                }
            }

            return null;
        }
    }
}
