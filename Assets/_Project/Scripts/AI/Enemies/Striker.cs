using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Nav;
using Lingjing.Sim;
using System;
using System.Collections.Generic;

namespace Lingjing.AI
{

/// <summary>
/// 撞针：Approach(A* 接近到 4 单位) → Windup 0.6s 静止 → Dash 0.25s 直线（预警结束瞬间锁定方向，
/// 不转向，撞墙停）→ Recover 0.8s → Approach。移速 2.2，冲刺 8。冲刺撞击 15 一次。
/// </summary>
public sealed class Striker : Enemy
{
    public const double SpeedConst = 2.2;
    public const double DashSpeedConst = 8.0;
    public const double TriggerRange = 4.0;
    public const int WindupTicks = 36;   // 0.6s
    public const int DashTicks = 15;     // 0.25s
    public const int RecoverTicks = 48;  // 0.8s
    private const int RepathInterval = 15;

    public enum StrikerState
    {
        Approach = 0,
        Windup = 1,
        Dash = 2,
        Recover = 3,
        Death = 4
    }

    private readonly PathFollower _follower = new();
    private Vec2 _dashDir = Vec2.UnitX;
    private bool _dashHitDone;

    public Striker()
    {
        Kind = EnemyKind.Striker;
        MoveSpeed = SpeedConst;
    }

    public override string StateName => ((StrikerState)State).ToString();

    /// <summary>本次冲刺是否还能撞到玩家（一次冲刺只结算一次）。</summary>
    public bool CanHitDuringDash => State == (int)StrikerState.Dash && !_dashHitDone;

    public void ConsumeDashHit() => _dashHitDone = true;

    public override void Update(WorldState w)
    {
        StateTick++;
        if (!Alive)
        {
            State = (int)StrikerState.Death;
            Hold();
            return;
        }

        Vec2 playerPos = w.Player.Pos;

        switch ((StrikerState)State)
        {
            case StrikerState.Approach:
                _follower.TickAge();
                if (Pos.DistanceTo(playerPos) <= TriggerRange)
                {
                    Enter(StrikerState.Windup);
                    break;
                }

                if (!_follower.HasPath || _follower.Age >= RepathInterval)
                {
                    if (w.TryPath(Pos, playerPos, out List<Vec2>? path))
                    {
                        _follower.SetPath(path, playerPos);
                    }
                }

                Vec2 steer = _follower.Steer(Pos);
                if (steer == Vec2.Zero)
                {
                    steer = (playerPos - Pos).Normalized();
                }

                MoveAlong(w, steer, MoveSpeed);
                break;

            case StrikerState.Windup:
                Hold();
                if (StateTick >= WindupTicks)
                {
                    // 预警结束的瞬间锁方向：之后无论玩家怎么走都不转向。
                    _dashDir = (playerPos - Pos).Normalized();
                    if (_dashDir == Vec2.Zero)
                    {
                        _dashDir = Vec2.UnitX;
                    }

                    _dashHitDone = false;
                    Enter(StrikerState.Dash);
                }

                break;

            case StrikerState.Dash:
                {
                    Vec2 before = Pos;
                    Pos = w.Grid.ResolveCircle(Pos + _dashDir * (DashSpeedConst * SimConstants.Dt), Radius);
                    Vel = _dashDir * DashSpeedConst;
                    double moved = Pos.DistanceTo(before);
                    bool blockedByWall = moved < DashSpeedConst * SimConstants.Dt * 0.5;
                    if (blockedByWall || StateTick >= DashTicks)
                    {
                        Enter(StrikerState.Recover);
                    }

                    break;
                }

            case StrikerState.Recover:
                Hold();
                if (StateTick >= RecoverTicks)
                {
                    _follower.Clear();
                    Enter(StrikerState.Approach);
                }

                break;
        }
    }

    private void Enter(StrikerState state)
    {
        State = (int)state;
        StateTick = 0;
    }
}
}
