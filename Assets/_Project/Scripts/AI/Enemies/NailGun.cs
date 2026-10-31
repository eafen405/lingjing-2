using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Nav;
using Lingjing.Sim;
using System;
using System.Collections.Generic;

namespace Lingjing.AI
{

/// <summary>
/// 钉枪：维持 5–7 单位环带，A* 只在被挡/越界时重新站位；
/// Aim 0.5s（需视线）→ 发射直线弹丸（速度 6）→ Recover 0.8s；玩家 &lt;3 单位时后退。
/// </summary>
public sealed class NailGun : Enemy
{
    public const double SpeedConst = 2.0;
    public const double StandoffMin = 5.0;
    public const double StandoffMax = 7.0;
    public const double RetreatDistance = 3.0;
    public const double RetreatUntil = 3.6;
    public const int AimTicks = 30;      // 0.5s
    public const int RecoverTicks = 48;  // 0.8s
    public const double ProjectileSpeed = 6.0;

    private const int RepathInterval = 15;
    private const int StandPointRethink = 30;

    public enum NailState
    {
        Reposition = 0,
        Aim = 1,
        Fire = 2,
        Recover = 3,
        Retreat = 4,
        Death = 5
    }

    private readonly PathFollower _follower = new();
    private Vec2 _standPoint;
    private bool _hasStandPoint;

    public NailGun()
    {
        Kind = EnemyKind.NailGun;
        MoveSpeed = SpeedConst;
    }

    public override string StateName => ((NailState)State).ToString();

    public override void Update(WorldState w)
    {
        StateTick++;
        if (!Alive)
        {
            State = (int)NailState.Death;
            Hold();
            return;
        }

        Vec2 playerPos = w.Player.Pos;
        double distance = Pos.DistanceTo(playerPos);
        bool lineOfSight = w.Grid.LineOfSight(Pos, playerPos, 0.15);

        // 被贴脸：打断一切，先撤。
        if (distance < RetreatDistance && State != (int)NailState.Retreat)
        {
            Enter(NailState.Retreat);
        }

        switch ((NailState)State)
        {
            case NailState.Retreat:
                {
                    Vec2 away = (Pos - playerPos).Normalized();
                    Vec2 dir = Steering.PickDirection(w.Grid, Pos, away, Radius);
                    if (dir == Vec2.Zero)
                    {
                        dir = away;
                    }

                    MoveAlong(w, dir, MoveSpeed);
                    if (distance >= RetreatUntil)
                    {
                        _hasStandPoint = false;
                        _follower.Clear();
                        Enter(NailState.Reposition);
                    }

                    break;
                }

            case NailState.Reposition:
                {
                    if (distance >= StandoffMin - 0.2 && distance <= StandoffMax + 0.2 && lineOfSight)
                    {
                        Enter(NailState.Aim);
                        break;
                    }

                    // 只在没有站位点、或站位点明显失效时重新决策（避免每帧找点）。
                    if (!_hasStandPoint || StateTick == 1 || StateTick % StandPointRethink == 0)
                    {
                        _hasStandPoint = TryChooseStandPoint(w, playerPos, out _standPoint);
                        _follower.Clear();
                    }

                    if (!_hasStandPoint)
                    {
                        Hold();
                        break;
                    }

                    _follower.TickAge();
                    if (!_follower.HasPath || _follower.Age >= RepathInterval)
                    {
                        if (w.TryPath(Pos, _standPoint, out List<Vec2>? path))
                        {
                            _follower.SetPath(path, _standPoint);
                        }
                    }

                    Vec2 steer = _follower.Steer(Pos);
                    if (steer == Vec2.Zero)
                    {
                        steer = (_standPoint - Pos).Normalized();
                    }

                    MoveAlong(w, steer, MoveSpeed);
                    break;
                }

            case NailState.Aim:
                Hold();
                if (!lineOfSight)
                {
                    _hasStandPoint = false;
                    Enter(NailState.Reposition);
                    break;
                }

                if (StateTick >= AimTicks)
                {
                    Enter(NailState.Fire);
                }

                break;

            case NailState.Fire:
                {
                    Vec2 dir = (playerPos - Pos).Normalized();
                    if (dir != Vec2.Zero)
                    {
                        w.SpawnProjectile(Pos + dir * (Radius + 0.1), dir * ProjectileSpeed);
                    }

                    Enter(NailState.Recover);
                    break;
                }

            case NailState.Recover:
                Hold();
                if (StateTick >= RecoverTicks)
                {
                    _hasStandPoint = false;
                    Enter(NailState.Reposition);
                }

                break;
        }
    }

    /// <summary>在 5–7 单位环带里找一格：有视线、可站、离自己最近。确定性：同分取下标小者。</summary>
    private bool TryChooseStandPoint(WorldState w, Vec2 playerPos, out Vec2 standPoint)
    {
        Grid grid = w.Grid;
        double bestScore = double.MaxValue;
        standPoint = Vec2.Zero;
        bool found = false;

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (!grid.IsWalkableCell(x, y))
                {
                    continue;
                }

                Vec2 center = Grid.CellCenter(x, y);
                double d = center.DistanceTo(playerPos);
                if (d < StandoffMin || d > StandoffMax)
                {
                    continue;
                }

                double travel = center.DistanceTo(Pos);
                if (travel >= bestScore)
                {
                    continue;
                }

                if (!grid.LineOfSight(center, playerPos, 0.15))
                {
                    continue;
                }

                bestScore = travel;
                standPoint = center;
                found = true;
            }
        }

        return found;
    }

    private void Enter(NailState state)
    {
        State = (int)state;
        StateTick = 0;
    }
}
}
