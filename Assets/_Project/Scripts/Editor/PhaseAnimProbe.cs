using System.Collections.Generic;
using System.Text;
using Lingjing.AI;
using Lingjing.Core;
using Lingjing.Fairy;
using Lingjing.Sim;
using Lingjing.View;
using UnityEditor;
using UnityEngine;
using Grid = Lingjing.Nav.Grid;

namespace Lingjing.Editor
{
    /// <summary>
    /// 阶段→动画映射无头检查：
    /// 1) 逐条核对每个 (实体, 阶段, 期望状态)，并确认映射结果确实在该实体的素材状态集合里；
    /// 2) 用真实 BattleWorld 跑 1200 tick（40×24 空地图、玩家静止、三种以骸），
    ///    确认阶段真的把动画驱动到 telegraph/charge/fire/impact 里至少两个。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.PhaseAnimProbe.Run
    /// </summary>
    public static class PhaseAnimProbe
    {
        const int SimTicks = 1200;

        readonly struct Case
        {
            public readonly EnemyKind kind;
            public readonly int state;
            public readonly string expect;

            public Case(EnemyKind kind, int state, string expect)
            {
                this.kind = kind;
                this.state = state;
                this.expect = expect;
            }
        }

        // 与 EnemyAnimMap 的映射表一一对应；顺序固定，输出可复现。
        static readonly Case[] Cases =
        {
            new Case(EnemyKind.TailLight, (int)TailLight.TailLightState.Approach, "move"),
            new Case(EnemyKind.TailLight, (int)TailLight.TailLightState.Death, "death"),

            new Case(EnemyKind.Striker, (int)Striker.StrikerState.Approach, "move"),
            new Case(EnemyKind.Striker, (int)Striker.StrikerState.Windup, "telegraph"),
            new Case(EnemyKind.Striker, (int)Striker.StrikerState.Dash, "charge"),
            new Case(EnemyKind.Striker, (int)Striker.StrikerState.Recover, "idle"),
            new Case(EnemyKind.Striker, (int)Striker.StrikerState.Death, "death"),

            new Case(EnemyKind.NailGun, (int)NailGun.NailState.Reposition, "move"),
            new Case(EnemyKind.NailGun, (int)NailGun.NailState.Aim, "idle"),
            new Case(EnemyKind.NailGun, (int)NailGun.NailState.Fire, "fire"),
            new Case(EnemyKind.NailGun, (int)NailGun.NailState.Recover, "idle"),
            new Case(EnemyKind.NailGun, (int)NailGun.NailState.Retreat, "move"),
            new Case(EnemyKind.NailGun, (int)NailGun.NailState.Death, "death"),

            new Case(EnemyKind.Dropper, (int)Dropper.DropperState.Hover, "idle"),
            new Case(EnemyKind.Dropper, (int)Dropper.DropperState.Mark, "telegraph"),
            new Case(EnemyKind.Dropper, (int)Dropper.DropperState.Drop, "impact"),
            new Case(EnemyKind.Dropper, (int)Dropper.DropperState.Death, "death"),

            new Case(EnemyKind.ChainBoss, (int)ChainBoss.BossState.Approach, "idle"),
            new Case(EnemyKind.ChainBoss, (int)ChainBoss.BossState.Death, "death"),
        };

        static readonly string[] ObservedStates = { "idle", "move", "telegraph", "charge", "fire", "impact", "death" };

        public static void Run()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/_Project/Art/ArtLibrary.asset");
            if (library == null)
            {
                Debug.Log("[phase] 没有 ArtLibrary 资产：Assets/_Project/Art/ArtLibrary.asset");
                Debug.Log("[phase] PROBE FAIL");
                EditorApplication.Exit(1);
                return;
            }

            var failures = new List<string>();
            for (int i = 0; i < Cases.Length; i++)
            {
                Case c = Cases[i];
                Enemy enemy = New(c.kind);
                enemy.State = c.state;

                string mapped = EnemyAnimMap.StateFor(enemy);
                string entity = EntityName(c.kind);
                bool inLibrary = HasState(library.EntityOf(entity), mapped);
                if (mapped != c.expect || !inLibrary)
                {
                    failures.Add($"{entity}.{enemy.StateName}->{mapped}(期望 {c.expect}, 在库 {inLibrary})");
                }
            }

            HashSet<string> seen = Simulate(out int ticks);

            int phaseHits = 0;
            if (seen.Contains("telegraph")) phaseHits++;
            if (seen.Contains("charge")) phaseHits++;
            if (seen.Contains("fire")) phaseHits++;
            if (seen.Contains("impact")) phaseHits++;

            bool pass = failures.Count == 0 && phaseHits >= 2;

            var summary = new StringBuilder();
            summary.Append("[phase] map=").Append(Cases.Length - failures.Count).Append('/').Append(Cases.Length);
            summary.Append(" simTicks=").Append(ticks);
            summary.Append(" seen=").Append(JoinSeen(seen));
            summary.Append(" phaseHits=").Append(phaseHits).Append("/4");
            if (failures.Count > 0)
            {
                summary.Append(" firstFail=").Append(failures[0]);
            }

            Debug.Log(summary.ToString());
            Debug.Log(pass ? "[phase] PROBE PASS" : "[phase] PROBE FAIL");

            if (!pass)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>真实世界跑一遍：玩家 (20.5,8.5) 静止，三种以骸各一，Command.None 推进。</summary>
        static HashSet<string> Simulate(out int ticks)
        {
            var grid = new Grid(40, 24);
            var world = new BattleWorld(grid, new Vec2(20.5, 8.5), 7);
            world.Spawn(EnemyKind.Striker, new Vec2(16.5, 8.5));
            world.Spawn(EnemyKind.NailGun, new Vec2(24.5, 8.5));
            world.Spawn(EnemyKind.Dropper, new Vec2(20.5, 4.5));

            var seen = new HashSet<string>();
            var tickStates = new HashSet<string>();
            ticks = 0;

            for (int t = 0; t < SimTicks; t++)
            {
                world.Step(Command.None);
                ticks++;

                // 每 tick 重采一次：死亡被移除的以骸当 tick 就不再计入。
                tickStates.Clear();
                List<Enemy> enemies = world.World.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    tickStates.Add(EnemyAnimMap.StateFor(enemies[i]));
                }

                seen.UnionWith(tickStates);
            }

            return seen;
        }

        static Enemy New(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.TailLight: return new TailLight();
                case EnemyKind.Striker: return new Striker();
                case EnemyKind.NailGun: return new NailGun();
                case EnemyKind.Dropper: return new Dropper();
                case EnemyKind.ChainBoss: return new ChainBoss();
                default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "未知以骸类型");
            }
        }

        /// <summary>实体名与 BattleRunner.EntityOf 同源；探针不许改 View，故就地复制。</summary>
        static string EntityName(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.TailLight: return "taillight";
                case EnemyKind.Striker: return "striker";
                case EnemyKind.NailGun: return "nailgun";
                case EnemyKind.Dropper: return "dropper";
                case EnemyKind.ChainBoss: return "chain";
                default: return "taillight";
            }
        }

        static bool HasState(ArtLibrary.EntityClips clips, string state)
        {
            if (clips == null)
            {
                return false;
            }

            for (int i = 0; i < clips.states.Length; i++)
            {
                if (clips.states[i].state == state)
                {
                    return true;
                }
            }

            return false;
        }

        static string JoinSeen(HashSet<string> seen)
        {
            var text = new StringBuilder();
            for (int i = 0; i < ObservedStates.Length; i++)
            {
                if (!seen.Contains(ObservedStates[i]))
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.Append(',');
                }

                text.Append(ObservedStates[i]);
            }

            return text.Length > 0 ? text.ToString() : "none";
        }
    }
}
