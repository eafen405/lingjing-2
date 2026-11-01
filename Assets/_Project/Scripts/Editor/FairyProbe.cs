using Lingjing.AI;
using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Fairy;
using Lingjing.Sim;
using UnityEditor;
using UnityEngine;
using Grid = Lingjing.Nav.Grid;

namespace Lingjing.Editor
{
    /// <summary>
    /// 代行无头检查：满槽才能触发、期间玩家输入被屏蔽且不回能、4 秒后自动归还。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.FairyProbe.Run
    /// </summary>
    public static class FairyProbe
    {
        public static void Run()
        {
            var grid = new Grid(40, 24);
            var world = new BattleWorld(grid, new Vec2(20.5, 8.5), 7);
            world.SetFairyPlanner(new BaselinePlanner());
            world.Spawn(EnemyKind.TailLight, new Vec2(14.5, 8.5));

            Player player = world.World.Player;
            bool rejectedWhenEmpty = !world.TryActivateFairy();

            player.Energy = Tuning.EnergyMax;
            bool activated = world.TryActivateFairy();
            Vec2 start = player.Pos;

            bool sawActive = false;
            for (int i = 0; i < SimConstants.FairyDurationTicks; i++)
            {
                world.Step(Command.None);
                sawActive |= world.FairyActive;
            }

            double moved = (player.Pos - start).Length;
            bool ended = !world.FairyActive;
            bool noEnergy = player.Energy < 1e-9;

            Debug.Log($"[fairy] rejectedWhenEmpty={rejectedWhenEmpty} activated={activated} moved={moved:0.00} " +
                      $"endedAfter{Tuning.FairySeconds:0}s={ended} energyStayedZero={noEnergy} sawActive={sawActive}");

            bool pass = rejectedWhenEmpty && activated && moved > 0.2 && ended && noEnergy && sawActive;
            Debug.Log(pass ? "[fairy] PROBE PASS" : "[fairy] PROBE FAIL");

            if (!pass)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
