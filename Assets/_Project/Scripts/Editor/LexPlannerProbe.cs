using Lingjing.AI;
using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Fairy;
using Lingjing.Sim;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Grid = Lingjing.Nav.Grid;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Lingjing.Editor
{
    /// <summary>
    /// LexPlanner 的无头检查：同一场景跑两遍 240 tick（正好一个代行期），
    /// 比较最终玩家位置、命中数与掉血是否完全一致，并确认决策中位耗时低于 2 ms、结束时玩家仍存活。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.LexPlannerProbe.Run
    /// </summary>
    public static class LexPlannerProbe
    {
        const int MapWidth = 40;
        const int MapHeight = 24;
        const ulong Seed = 7UL;
        const double MedianBudgetMs = 2.0;

        static readonly Vec2 PlayerStart = new Vec2(20.5, 8.5);

        /// <summary>两只尾灯、一只撞针、一只钉枪。都离地图边界 ≥3 格，不贴墙。</summary>
        static readonly SpawnSpec[] Spawns =
        {
            new SpawnSpec(EnemyKind.TailLight, new Vec2(14.5, 8.5)),
            new SpawnSpec(EnemyKind.TailLight, new Vec2(26.5, 8.5)),
            new SpawnSpec(EnemyKind.Striker, new Vec2(20.5, 3.5)),
            new SpawnSpec(EnemyKind.NailGun, new Vec2(28.5, 12.5))
        };

        public static void Run()
        {
            RunResult first = Simulate();
            RunResult second = Simulate();

            bool deterministic = first.PlayerPos.Equals(second.PlayerPos) &&
                                 first.Hits == second.Hits &&
                                 first.HpLost.Equals(second.HpLost);
            bool activated = first.Activated && second.Activated;
            bool ended = first.FairyEnded && second.FairyEnded;
            bool alive = first.Alive && second.Alive;
            bool budgetOk = first.MedianDecisionMs < MedianBudgetMs;

            bool pass = activated && ended && alive && deterministic && budgetOk;

            Debug.Log(
                $"[lex] fairyActivated={activated} fairyEnded={ended} playerAlive={alive} " +
                $"moved={first.Displacement:0.000} hits={first.Hits} kills={first.Kills} hpLost={first.HpLost:0.###} " +
                $"decisions={first.Decisions} medianMs={first.MedianDecisionMs:0.###} slowestMs={first.SlowestDecisionMs:0.###} " +
                $"firstMs={first.FirstDecisionMs:0.###} overBudgetFirstRun={first.OverBudgetDecisions} " +
                $"overBudgetSecondRun={second.OverBudgetDecisions} " +
                $"pos1=({first.PlayerPos.X:0.######},{first.PlayerPos.Y:0.######}) " +
                $"pos2=({second.PlayerPos.X:0.######},{second.PlayerPos.Y:0.######}) " +
                $"hits2={second.Hits} hpLost2={second.HpLost:0.###} deterministic={deterministic} " +
                $"medianBudgetMs={MedianBudgetMs:0.###}");
            Debug.Log(pass ? "[lex] PROBE PASS" : "[lex] PROBE FAIL");

            if (!pass)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>一次完整代行：建世界 → 刷 4 只以骸 → 能量拉满 → 240 tick → 收集指标。</summary>
        static RunResult Simulate()
        {
            var grid = new Grid(MapWidth, MapHeight);
            var world = new BattleWorld(grid, PlayerStart, Seed);

            int hits = 0;
            int kills = 0;
            world.BladeHit += (_, __) => hits++;
            world.EnemyKilled += _ => kills++;

            for (int i = 0; i < Spawns.Length; i++)
            {
                world.Spawn(Spawns[i].Kind, Spawns[i].Pos);
            }

            // 正式规划器只包一层探针侧计时装饰器；决策本身仍全部由 LexPlanner 给出。
            var planner = new LexPlanner();
            var timing = new TimingPlanner(planner);
            world.SetFairyPlanner(timing);

            Player player = world.World.Player;
            player.Energy = DamageRules.EnergyMax;
            bool activated = world.TryActivateFairy();
            Vec2 start = player.Pos;

            for (int i = 0; i < SimConstants.FairyDurationTicks; i++)
            {
                world.Step(Command.None);
            }

            return new RunResult
            {
                Activated = activated,
                FairyEnded = !world.FairyActive,
                PlayerPos = player.Pos,
                Displacement = (player.Pos - start).Length,
                Hits = hits,
                Kills = kills,
                HpLost = player.MaxHp - player.Hp,
                Alive = !player.Dead,
                Decisions = timing.Count,
                FirstDecisionMs = timing.FirstMs(),
                OverBudgetDecisions = timing.OverBudgetCount(MedianBudgetMs),
                MedianDecisionMs = timing.MedianMs(),
                SlowestDecisionMs = timing.SlowestMs()
            };
        }

        readonly struct SpawnSpec
        {
            public readonly EnemyKind Kind;
            public readonly Vec2 Pos;

            public SpawnSpec(EnemyKind kind, Vec2 pos)
            {
                Kind = kind;
                Pos = pos;
            }
        }

        sealed class RunResult
        {
            public bool Activated;
            public bool FairyEnded;
            public Vec2 PlayerPos;
            public double Displacement;
            public int Hits;
            public int Kills;
            public double HpLost;
            public bool Alive;
            public int Decisions;
            public double FirstDecisionMs;
            public int OverBudgetDecisions;
            public double MedianDecisionMs;
            public double SlowestDecisionMs;
        }

        /// <summary>探针侧计时装饰器：只测 Decide 的墙钟耗时，不改指令、不参与世界。</summary>
        sealed class TimingPlanner : IPlanner
        {
            readonly LexPlanner _inner;
            readonly List<double> _milliseconds;

            public TimingPlanner(LexPlanner inner)
            {
                _inner = inner;
                _milliseconds = new List<double>(SimConstants.FairyDurationTicks / SimConstants.DecisionTicks + 1);
            }

            public int Count => _milliseconds.Count;

            public Command Decide(BattleSnapshot s)
            {
                long started = Stopwatch.GetTimestamp();
                Command command = _inner.Decide(s);
                long elapsed = Stopwatch.GetTimestamp() - started;
                _milliseconds.Add(elapsed * 1000.0 / Stopwatch.Frequency);
                return command;
            }

            public double MedianMs()
            {
                if (_milliseconds.Count == 0)
                {
                    return double.PositiveInfinity;
                }

                var sorted = new double[_milliseconds.Count];
                _milliseconds.CopyTo(sorted);
                Array.Sort(sorted);

                int middle = sorted.Length / 2;
                return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) * 0.5 : sorted[middle];
            }

            public double FirstMs() => _milliseconds.Count == 0 ? double.PositiveInfinity : _milliseconds[0];

            public int OverBudgetCount(double budgetMs)
            {
                int count = 0;
                for (int i = 0; i < _milliseconds.Count; i++)
                {
                    if (_milliseconds[i] > budgetMs)
                    {
                        count++;
                    }
                }

                return count;
            }

            public double SlowestMs()
            {
                double slowest = 0.0;
                for (int i = 0; i < _milliseconds.Count; i++)
                {
                    if (_milliseconds[i] > slowest)
                    {
                        slowest = _milliseconds[i];
                    }
                }

                return _milliseconds.Count == 0 ? double.PositiveInfinity : slowest;
            }
        }
    }
}
