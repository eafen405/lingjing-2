using System;
using Lingjing.AI;
using Lingjing.Combat;
using Lingjing.Core;

namespace Lingjing.Level
{
    /// <summary>关内成长的选项（game-design §7.1 / §4.5）。</summary>
    public enum GrowthKind
    {
        Damage,
        BladeCount,
        Omega,
        Speed,

        /// <summary>代行时长。她是加分项也是有故事的机制，时间越长越接近"整局交给她"。</summary>
        FairyDuration
    }

    /// <summary>成长数值的唯一权威：经验权重、升级阈值、每类上限、效果强度都在这里。</summary>
    public static class GrowthRules
    {
        /// <summary>一次给几张牌。</summary>
        public const int OptionCount = 3;

        /// <summary>类别总数，数组一律按它开。</summary>
        public const int KindCount = 5;

        /// <summary>每点一次代行时长加多少秒。</summary>
        public const double FairyDurationPerLevelSeconds = 2.0;

        /// <summary>击杀经验按以骸强度加权，不数人头（§7.1）。</summary>
        public static double ExperienceOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.TailLight: return 12.0;
                case EnemyKind.Striker: return 16.0;
                case EnemyKind.NailGun: return 14.0;
                case EnemyKind.Dropper: return 20.0;
                case EnemyKind.ChainBoss: return 300.0;
                default: return 12.0;
            }
        }

        /// <summary>从第 level 级升到下一级所需经验（level 从 0 算）。</summary>
        public static double Threshold(int level) => 100.0 + 40.0 * level;

        /// <summary>每类最多点几次；刀数 6 次正好对应 4 → 10 把（§7.2）。</summary>
        public static int CapOf(GrowthKind kind)
        {
            switch (kind)
            {
                case GrowthKind.Damage: return 8;
                case GrowthKind.BladeCount: return 6;
                case GrowthKind.Omega: return 6;
                case GrowthKind.Speed: return 5;
                case GrowthKind.FairyDuration: return 4;
                default: return 0;
            }
        }

        /// <summary>每级的增量倍率。刀数不用倍率（见 <see cref="Apply"/>），返回 1。</summary>
        public static double MultiplierOf(GrowthKind kind)
        {
            switch (kind)
            {
                case GrowthKind.Damage: return 1.20;
                case GrowthKind.Omega: return 1.10;
                case GrowthKind.Speed: return 1.08;
                default: return 1.0;
            }
        }

        public static void Apply(Player player, GrowthKind kind)
        {
            switch (kind)
            {
                case GrowthKind.Damage:
                    player.BladeDamage *= MultiplierOf(kind);
                    break;
                case GrowthKind.BladeCount:
                    player.Ring.BladeCount = Math.Min(BladeRing.MaxBladeCount, player.Ring.BladeCount + 1);
                    break;
                case GrowthKind.Omega:
                    player.Ring.Omega *= MultiplierOf(kind);
                    break;
                case GrowthKind.Speed:
                    player.Speed *= MultiplierOf(kind);
                    break;
                case GrowthKind.FairyDuration:
                    player.FairyDurationTicks += SimConstants.FromSeconds(FairyDurationPerLevelSeconds);
                    break;
            }
        }
    }

    /// <summary>
    /// 一局内的经验与三选一。它只改 Player（伤害、刀数、角速度、移速），
    /// 不碰世界推进；世界在待选期间由 LevelRun 停住。
    /// </summary>
    public sealed class GrowthState
    {
        readonly int[] _taken = new int[GrowthRules.KindCount];
        readonly GrowthKind[] _options = new GrowthKind[GrowthRules.OptionCount];
        readonly Prng _rng;

        public GrowthState(ulong seed)
        {
            _rng = new Prng(seed);
        }

        public double Experience { get; private set; }

        /// <summary>已完成的升级次数（从 0 起）。</summary>
        public int Level { get; private set; }

        public double ExperienceToNext => GrowthRules.Threshold(Level);

        /// <summary>有未处理的升级：此时 LevelRun 不投波次、不推进节。</summary>
        public bool HasPendingChoice { get; private set; }

        /// <summary>本次给出的牌数（池子不够三张时会少）。</summary>
        public int OptionCount { get; private set; }

        public GrowthKind OptionAt(int index) => _options[index];

        /// <summary>某类已经点了几次（UI 用来显示 +N）。</summary>
        public int TakenCount(GrowthKind kind) => _taken[(int)kind];

        /// <summary>击杀给经验；够阈值就开一次三选一。待选期间继续累积，不重复开。</summary>
        public void Award(Player player, EnemyKind kind)
        {
            Experience += GrowthRules.ExperienceOf(kind);

            if (!HasPendingChoice && Experience >= ExperienceToNext)
            {
                SpendLevel();
            }
        }

        /// <summary>选一张牌。只接受本次给出的那几张；选完若又够阈值，立刻再开一组。</summary>
        public bool Choose(Player player, GrowthKind kind)
        {
            if (!HasPendingChoice)
            {
                return false;
            }

            bool offered = false;
            for (int i = 0; i < OptionCount; i++)
            {
                if (_options[i] == kind)
                {
                    offered = true;
                    break;
                }
            }

            if (!offered)
            {
                return false;
            }

            GrowthRules.Apply(player, kind);
            _taken[(int)kind]++;

            HasPendingChoice = false;
            OptionCount = 0;

            if (Experience >= ExperienceToNext)
            {
                SpendLevel();
            }

            return true;
        }

        void SpendLevel()
        {
            Experience -= ExperienceToNext;
            Level++;
            RollOptions();
        }

        /// <summary>从没满级的类别里随机取三张（不足三张就全给）。</summary>
        void RollOptions()
        {
            var pool = new GrowthKind[GrowthRules.KindCount];
            int count = 0;
            for (int k = 0; k < pool.Length; k++)
            {
                var kind = (GrowthKind)k;
                if (_taken[k] < GrowthRules.CapOf(kind))
                {
                    pool[count++] = kind;
                }
            }

            if (count == 0)
            {
                // 全部满级：没有牌可发，等级照算，但不再打断。
                HasPendingChoice = false;
                OptionCount = 0;
                return;
            }

            OptionCount = Math.Min(GrowthRules.OptionCount, count);
            for (int i = 0; i < OptionCount; i++)
            {
                int j = i + _rng.RangeInt(0, count - i);
                GrowthKind swap = pool[i];
                pool[i] = pool[j];
                pool[j] = swap;
                _options[i] = pool[i];
            }

            HasPendingChoice = true;
        }
    }
}
