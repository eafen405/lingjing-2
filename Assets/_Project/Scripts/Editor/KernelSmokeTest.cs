using Lingjing.AI;
using Lingjing.Core;
using Lingjing.Data;
using Lingjing.Fairy;
using Lingjing.Sim;
using UnityEditor;
using UnityEngine;
using Grid = Lingjing.Nav.Grid;

namespace Lingjing.Editor
{
    /// <summary>
    /// 无头冒烟：不打开编辑器也能确认「搬进来的内核真的会跑」。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.KernelSmokeTest.Run
    /// </summary>
    public static class KernelSmokeTest
    {
        public static void Run()
        {
            (int hits, int kills, double hp, ulong hash) a = Simulate();
            (int hits, int kills, double hp, ulong hash) b = Simulate();

            Debug.Log($"[smoke] run1 hits={a.hits} kills={a.kills} hp={a.hp:0.0} hash={a.hash:X16}");
            Debug.Log($"[smoke] run2 hits={b.hits} kills={b.kills} hp={b.hp:0.0} hash={b.hash:X16}");
            Debug.Log(a.hash == b.hash
                ? "[smoke] DETERMINISM PASS: 两次逐 tick 哈希一致"
                : "[smoke] DETERMINISM FAIL: 两次哈希不一致");

            if (a.hash != b.hash || a.hits <= 0)
            {
                EditorApplication.Exit(1);
            }
        }

        static (int, int, double, ulong) Simulate()
        {
            var grid = new Grid(40, 24);
            grid.FillRect(18, 0, 1, 6);

            var world = new BattleWorld(grid, new Vec2(20.5, 8.5), 7);
            world.Spawn(EnemyKind.TailLight, new Vec2(6.5, 4.5));

            int hits = 0;
            int kills = 0;
            world.BladeHit += (_, __) => hits++;
            world.EnemyKilled += _ => kills++;

            var hasher = new TickHasher();
            ulong tail = 0;
            // 绕环风筝（与原型 single_tail_light 同一份脚本策略）：用来验证「命中 → 扣血 → 击杀」整条链路。
            var policy = new ScriptedPlayerPolicy();

            for (int i = 0; i < 600; i++)
            {
                Command command = policy.Decide(world.World, PlayerScriptKind.Orbit);
                world.Step(command);
                tail = hasher.HashTick(world.Tick, 7UL, command, Command.None, false, world.World);
            }

            return (hits, kills, world.World.Player.Hp, tail);
        }
    }
}
