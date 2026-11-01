using System;
using Lingjing.AI;
using Lingjing.Core;
using Lingjing.Nav;
using Lingjing.Sim;

namespace Lingjing.Level
{
    /// <summary>一局的结局。</summary>
    public enum RunOutcome
    {
        Running,
        Cleared,
        Failed
    }

    /// <summary>
    /// 一局的推进规则：按 LevelDef 把波次注入 BattleWorld，并在场上以骸清空时推进到下一节。
    /// 它只读世界、只调用 Spawn；世界的推进仍归 BattleWorld.Step 管。
    /// 用自己的 Prng（不复用世界的那条流），这样落点抽样不会打乱模拟的随机序列。
    /// </summary>
    public sealed class LevelRun
    {
        /// <summary>节间停顿 1.5s：给"第 N 节"提示与喘息留窗口。</summary>
        public const int SectionBreakTicks = 90;

        /// <summary>波次落点半径：8–14 单位（半屏到接近一屏）。</summary>
        public const double SpawnRadiusMin = 8.0;
        public const double SpawnRadiusMax = 14.0;

        /// <summary>落点净空：以骸半径 0.35 再留一点余量，避免落地就卡在墙角。</summary>
        public const double SpawnClearance = 0.5;

        /// <summary>落点采样次数；全落空就退回"夹进场地 + 推离墙"。</summary>
        const int SpawnSamples = 24;

        readonly LevelDef _level;
        readonly BattleWorld _world;
        readonly Prng _rng;

        int _sectionIndex;
        int _waveIndex;
        int _breakTicksLeft;
        RunOutcome _outcome;

        public LevelRun(LevelDef level, BattleWorld world, ulong seed)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _rng = new Prng(seed);
            Growth = new GrowthState(seed);

            // 经验只由击杀产生（§7.1）。挂在这里而不是表现层，是为了让无头探针也能跑出成长。
            _world.EnemyKilled += OnEnemyKilled;

            SpawnSupplies();
        }

        /// <summary>关内经验与三选一。待选期间整个局停住。</summary>
        public GrowthState Growth { get; }

        void OnEnemyKilled(Enemy enemy) => Growth.Award(_world.World.Player, enemy.Kind);

        public RunOutcome Outcome => _outcome;

        /// <summary>当前节下标（0 起）。</summary>
        public int SectionIndex => _sectionIndex;

        /// <summary>本节已投放到第几波；等于本节的波数表示全部投完。</summary>
        public int WaveIndex => _waveIndex;

        public int SectionCount => _level.sections?.Length ?? 0;

        public int WaveCount
        {
            get
            {
                SectionDef section = CurrentSection;
                return section?.waves?.Length ?? 0;
            }
        }

        /// <summary>节间停顿中（此时不投放，世界照常推进）。</summary>
        public bool Intermission => _breakTicksLeft > 0;

        public int BreakTicksLeft => _breakTicksLeft;

        SectionDef CurrentSection
        {
            get
            {
                SectionDef[] sections = _level.sections;
                if (sections == null || _sectionIndex < 0 || _sectionIndex >= sections.Length)
                {
                    return null;
                }

                return sections[_sectionIndex];
            }
        }

        /// <summary>每个固定步长之后调一次：补投波次、推进节、判定结局。</summary>
        public void Tick()
        {
            if (_outcome != RunOutcome.Running)
            {
                return;
            }

            if (_world.PlayerDead)
            {
                _outcome = RunOutcome.Failed;
                return;
            }

            // 升级待选：世界由调用方停住，这里也不推进波次与节。
            if (Growth.HasPendingChoice)
            {
                return;
            }

            if (_breakTicksLeft > 0)
            {
                _breakTicksLeft--;
                return;
            }

            SectionDef section = CurrentSection;
            if (section?.waves == null || section.waves.Length == 0)
            {
                AdvanceSection();
                return;
            }

            if (_world.World.Enemies.Count > 0)
            {
                return;
            }

            // 场上清空：还有没投的波就投下一波，投完了就换下一节。
            if (_waveIndex < section.waves.Length)
            {
                SpawnWave(section.waves[_waveIndex]);
                _waveIndex++;
                return;
            }

            AdvanceSection();
        }

        void AdvanceSection()
        {
            if (_sectionIndex + 1 >= SectionCount)
            {
                _outcome = RunOutcome.Cleared;
                return;
            }

            _sectionIndex++;
            _waveIndex = 0;
            _breakTicksLeft = SectionBreakTicks;
            SpawnSupplies();
        }

        /// <summary>每节开始时把固定补给点刷出来（§7.3）。</summary>
        void SpawnSupplies()
        {
            SupplyDef[] supplies = _level.supplies;
            if (supplies == null)
            {
                return;
            }

            for (int i = 0; i < supplies.Length; i++)
            {
                SupplyDef supply = supplies[i];
                if (supply == null || !SupplyKindNames.TryParse(supply.kind, out PickupKind kind))
                {
                    continue;
                }

                _world.World.SpawnPickup(kind, new Vec2(supply.x + 0.5, supply.y + 0.5));
            }
        }

        void SpawnWave(WaveDef wave)
        {
            if (wave?.units == null)
            {
                return;
            }

            for (int i = 0; i < wave.units.Length; i++)
            {
                UnitDef unit = wave.units[i];
                if (unit == null || unit.count <= 0)
                {
                    continue;
                }

                if (!EnemyKindNames.TryParse(unit.kind, out EnemyKind kind))
                {
                    // 未知种类跳过而不抛：数据是手写的，宁可少一只，也不要整局起不来。
                    continue;
                }

                for (int n = 0; n < unit.count; n++)
                {
                    _world.Spawn(kind, PickSpawnPoint());
                }
            }
        }

        Vec2 PickSpawnPoint()
        {
            Grid grid = _world.World.Grid;
            Vec2 center = _world.World.Player.Pos;

            for (int i = 0; i < SpawnSamples; i++)
            {
                double angle = _rng.Range(0.0, Math.PI * 2.0);
                double radius = _rng.Range(SpawnRadiusMin, SpawnRadiusMax);
                Vec2 candidate = center + Vec2.FromAngle(angle) * radius;
                if (Fits(grid, candidate))
                {
                    return candidate;
                }
            }

            // 兜底：固定往右上放一段，再让网格把圆推出墙外并夹进场地。
            return grid.ResolveCircle(center + new Vec2(SpawnRadiusMin, SpawnRadiusMin), SpawnClearance);
        }

        static bool Fits(Grid grid, Vec2 p)
        {
            if (p.X < SpawnClearance || p.Y < SpawnClearance)
            {
                return false;
            }

            if (p.X > grid.Width - SpawnClearance || p.Y > grid.Height - SpawnClearance)
            {
                return false;
            }

            return grid.CircleFits(p, SpawnClearance);
        }
    }
}
