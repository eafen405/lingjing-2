using Lingjing.AI;
using Lingjing.Core;
using Lingjing.Data;
using Lingjing.Fairy;
using Lingjing.Level;
using Lingjing.Sim;
using UnityEditor;
using UnityEngine;
using Grid = Lingjing.Nav.Grid;

namespace Lingjing.Editor
{
    /// <summary>
    /// 一局闭环的无头检查：把 levels.json 里的关卡真跑一遍。
    /// 断言「波次全部投出、四节全部推进、跑出结局、两次运行的击杀/耗时/落点完全一致」。
    /// 为了让探针只验流程、不验操作水平，跑的时候每 tick 把血量拉满（打印里标明 immortal）。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.LevelProbe.Run
    /// </summary>
    public static class LevelProbe
    {
        const ulong Seed = 7UL;
        const int MaxTicks = 120000;

        public static void Run()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(M1SceneBuilder.LevelDataPath);
            if (json == null)
            {
                Fail($"找不到关卡数据 {M1SceneBuilder.LevelDataPath}");
                return;
            }

            LevelCatalog catalog = JsonUtility.FromJson<LevelCatalog>(json.text);
            if (catalog?.levels == null || catalog.levels.Length == 0)
            {
                Fail("levels.json 解析成空");
                return;
            }

            LevelDef level1 = catalog.Find(1);
            LevelDef level2 = catalog.Find(2);
            if (level1 == null || level2 == null)
            {
                Fail("levels.json 里缺第一关或第二关");
                return;
            }

            int expected1 = CountUnits(level1);
            int expected2 = CountUnits(level2);

            Result a = Simulate(level1, true);
            Result b = Simulate(level1, true);
            Result c = Simulate(level2, true);

            // 不参与判定，只作平衡参考：真实血量下这一关能撑多久。
            Result m1 = Simulate(level1, false);
            Result m2 = Simulate(level2, false);

            Debug.Log($"[level] L1 run1 outcome={a.Outcome} kills={a.Kills}/{expected1} sections={a.LastSection + 1}/{a.SectionCount} " +
                      $"ticks={a.Ticks} hp={a.Hp:0.0} pos=({a.Pos.X:0.##},{a.Pos.Y:0.##})");
            Debug.Log($"[level] L1 run2 outcome={b.Outcome} kills={b.Kills}/{expected1} sections={b.LastSection + 1}/{b.SectionCount} ticks={b.Ticks}");
            Debug.Log($"[level] L2      outcome={c.Outcome} kills={c.Kills}/{expected2} sections={c.LastSection + 1}/{c.SectionCount} ticks={c.Ticks} " +
                      $"levels={c.Levels} blades={c.Blades} dmg={c.Damage:0.#}");
            Debug.Log($"[level] 真实血量（只作参考，不判定）：" +
                      $"L1 outcome={m1.Outcome} kills={m1.Kills}/{expected1} sections={m1.LastSection + 1} 存活{m1.Ticks / 60.0:0.0}s hp={m1.Hp:0.0}；" +
                      $"L2 outcome={m2.Outcome} kills={m2.Kills}/{expected2} sections={m2.LastSection + 1} 存活{m2.Ticks / 60.0:0.0}s hp={m2.Hp:0.0}");

            bool deterministic = a.Kills == b.Kills && a.Ticks == b.Ticks && a.Pos.Equals(b.Pos);
            bool l1Cleared = a.Outcome == RunOutcome.Cleared && a.Kills == expected1 && a.LastSection == a.SectionCount - 1;
            bool l2Cleared = c.Outcome == RunOutcome.Cleared && c.Kills == expected2;

            if (!l2Cleared)
            {
                Debug.Log($"[level] L2 卡住时 player=({c.Pos.X:0.#},{c.Pos.Y:0.#}) 场上剩余：{c.Remaining}");
                Debug.Log($"[level] L2 落点轨迹：{c.Trail}");
            }

            Debug.Log($"[level] deterministic={deterministic} l1Cleared={l1Cleared} l2Cleared={l2Cleared}");
            Debug.Log(deterministic && l1Cleared && l2Cleared ? "[level] PROBE PASS" : "[level] PROBE FAIL");

            if (!(deterministic && l1Cleared && l2Cleared))
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>整关的以骸总数：跑通时击杀数必须等于它，否则说明有波次没投出去。</summary>
        static int CountUnits(LevelDef level)
        {
            int total = 0;
            if (level.sections == null)
            {
                return total;
            }

            for (int s = 0; s < level.sections.Length; s++)
            {
                WaveDef[] waves = level.sections[s]?.waves;
                if (waves == null)
                {
                    continue;
                }

                for (int w = 0; w < waves.Length; w++)
                {
                    UnitDef[] units = waves[w]?.units;
                    if (units == null)
                    {
                        continue;
                    }

                    for (int u = 0; u < units.Length; u++)
                    {
                        if (units[u] != null && units[u].count > 0)
                        {
                            total += units[u].count;
                        }
                    }
                }
            }

            return total;
        }

        static Result Simulate(LevelDef level, bool immortal)
        {
            Grid grid = LevelField.Build(level);
            var world = new BattleWorld(grid, LevelField.PlayerStart(grid), Seed);
            var run = new LevelRun(level, world, Seed);
            var policy = new ScriptedPlayerPolicy();

            int kills = 0;
            world.EnemyKilled += _ => kills++;

            int lastSection = 0;
            int ticks = 0;
            var trail = new System.Text.StringBuilder();
            while (ticks < MaxTicks && run.Outcome == RunOutcome.Running)
            {
                Command command = policy.Decide(world.World, PlayerScriptKind.Kite);
                world.Step(command);

                // 探针专用：只验流程，所以不让死亡打断。必须在 run.Tick 之前回血，否则 Tick 会判失败。
                if (immortal)
                {
                    world.World.Player.Hp = world.World.Player.MaxHp;
                }

                run.Tick();

                // 探针代替玩家选牌：永远选第一张，只为让流程继续。真人由 ChoiceScreen 接手。
                while (run.Growth.HasPendingChoice)
                {
                    run.Growth.Choose(world.World.Player, run.Growth.OptionAt(0));
                }

                // 卡住时要知道玩家到底在不在动：每 3000 tick 记一个落点。
                if (ticks % 3000 == 0 && trail.Length < 400)
                {
                    Vec2 p = world.World.Player.Pos;
                    trail.Append($"({p.X:0.#},{p.Y:0.#})s{run.SectionIndex} ");
                }

                if (run.SectionIndex > lastSection)
                {
                    lastSection = run.SectionIndex;
                }

                ticks++;
            }

            return new Result
            {
                Outcome = run.Outcome,
                Kills = kills,
                Ticks = ticks,
                LastSection = lastSection,
                SectionCount = run.SectionCount,
                Pos = world.World.Player.Pos,
                Hp = world.World.Player.Hp,
                Remaining = Describe(world),
                Levels = run.Growth.Level,
                Blades = world.World.Player.Ring.BladeCount,
                Damage = world.World.Player.BladeDamage,
                Trail = trail.ToString()
            };
        }

        /// <summary>卡住时把场上剩下的以骸打出来：种类、血量、距玩家的距离。</summary>
        static string Describe(BattleWorld world)
        {
            var text = new System.Text.StringBuilder();
            Vec2 player = world.World.Player.Pos;
            for (int i = 0; i < world.World.Enemies.Count; i++)
            {
                Enemy e = world.World.Enemies[i];
                if (text.Length > 0)
                {
                    text.Append(' ');
                }

                text.Append($"{e.Kind}#{e.Id}(hp={e.Hp:0},d={e.Pos.DistanceTo(player):0.0})");
            }

            if (text.Length == 0)
            {
                text.Append("无");
            }

            return text.ToString();
        }

        static void Fail(string reason)
        {
            Debug.LogError($"[level] PROBE FAIL: {reason}");
            EditorApplication.Exit(1);
        }

        struct Result
        {
            public RunOutcome Outcome;
            public int Kills;
            public int Ticks;
            public int LastSection;
            public int SectionCount;
            public Vec2 Pos;
            public double Hp;
            public string Remaining;
            public int Levels;
            public int Blades;
            public double Damage;
            public string Trail;
        }
    }
}
