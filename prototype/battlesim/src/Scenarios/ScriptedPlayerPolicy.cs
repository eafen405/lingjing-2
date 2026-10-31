using BattleSim.Core;
using BattleSim.Enemies;
using BattleSim.Fairy;
using BattleSim.Nav;
using BattleSim.Sim;

namespace BattleSim.Scenarios;

/// <summary>
/// 场景驱动的"玩家脚本"。**不是** Fairy：它只按固定策略走位（直线逃跑 / 绕环风筝），
/// 用来给 baseline 规划器提供对照组。
/// </summary>
public sealed class ScriptedPlayerPolicy
{
    private int _orbitSign = 1;
    private int _stuckTicks;
    private Vec2 _lastPos = new(-999, -999);
    private Vec2 _lastDir;

    public Command Decide(WorldState w, PlayerScriptKind kind)
    {
        var player = w.Player;

        Enemy? nearest = null;
        double nearestDistance = double.PositiveInfinity;
        for (int i = 0; i < w.Enemies.Count; i++)
        {
            Enemy e = w.Enemies[i];
            if (!e.Alive)
            {
                continue;
            }

            double d = e.Pos.DistanceTo(player.Pos);
            if (d < nearestDistance)
            {
                nearestDistance = d;
                nearest = e;
            }
        }

        if (player.Pos.DistanceTo(_lastPos) < 0.02)
        {
            _stuckTicks++;
        }
        else
        {
            _stuckTicks = 0;
        }

        _lastPos = player.Pos;
        if (_stuckTicks > 20)
        {
            _orbitSign = -_orbitSign;
            _stuckTicks = 0;
        }

        Vec2 desired;
        if (nearest == null)
        {
            desired = Vec2.UnitX;
        }
        else
        {
            Vec2 toEnemy = (nearest.Pos - player.Pos).Normalized();
            if (kind == PlayerScriptKind.Flee)
            {
                desired = -toEnemy;
            }
            else
            {
                // 绕环风筝：目标是把最近的敌人稳定压在刀刃半径（2.0）上。
                if (nearestDistance > 3.0)
                {
                    desired = toEnemy;
                }
                else
                {
                    Vec2 tangential = toEnemy.Perpendicular() * _orbitSign;
                    Vec2 radial = toEnemy * ((nearestDistance - 2.15) * 3.0);
                    desired = (tangential + radial).Normalized();
                }
            }
        }

        Vec2 dir = Steering.PickDirection(w.Grid, player.Pos, desired, player.Radius, 0.5, _lastDir);
        if (dir == Vec2.Zero)
        {
            dir = _lastDir;
        }

        _lastDir = dir;
        return new Command(dir, player.SpinSign, false);
    }
}
