using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Nav;
using Lingjing.Sim;
using System;

namespace Lingjing.AI
{

/// <summary>
/// 坠物：飞行（无视地面墙，但不出地图边界）。
/// Hover（空中跟随，受空域分离）→ Mark（在玩家附近画 2×2 矩形落区）→ Drop（1.2s 预警）→ 回到 Hover。
/// 落区伤害由模拟层在预警结束时结算。
/// </summary>
public sealed class Dropper : Enemy
{
    public const double SpeedConst = 2.4;
    public const double MarkRange = 4.0;
    public const int MarkDelayTicks = 72;   // 1.2s 预警
    public const int HoverCooldownTicks = 90;

    public enum DropperState
    {
        Hover = 0,
        Mark = 1,
        Drop = 2,
        Death = 3
    }

    private int _cooldown;

    public Dropper()
    {
        Kind = EnemyKind.Dropper;
        MoveSpeed = SpeedConst;
    }

    public override string StateName => ((DropperState)State).ToString();

    public override bool Flying => true;

    public override void Update(WorldState w)
    {
        StateTick++;
        if (!Alive)
        {
            State = (int)DropperState.Death;
            Hold();
            return;
        }

        if (_cooldown > 0)
        {
            _cooldown--;
        }

        Vec2 playerPos = w.Player.Pos;
        double distance = Pos.DistanceTo(playerPos);

        switch ((DropperState)State)
        {
            case DropperState.Hover:
                {
                    // 空中跟随：靠近到玩家 2 格以内就悬停。
                    if (distance > 2.0)
                    {
                        MoveAlong(w, (playerPos - Pos).Normalized(), MoveSpeed);
                    }
                    else
                    {
                        Hold();
                    }

                    if (_cooldown == 0 && distance <= MarkRange)
                    {
                        Enter(DropperState.Mark);
                    }

                    break;
                }

            case DropperState.Mark:
                {
                    MarkZone(w);
                    Enter(DropperState.Drop);
                    break;
                }

            case DropperState.Drop:
                Hold();
                if (StateTick >= MarkDelayTicks - 1)
                {
                    _cooldown = HoverCooldownTicks;
                    Enter(DropperState.Hover);
                }

                break;
        }
    }

    private void MarkZone(WorldState w)
    {
        // 2×2 格、像素对齐：左下角落在整数格上，中心在玩家附近 ±2 格。
        int baseX = Grid.CellOf(w.Player.Pos.X);
        int baseY = Grid.CellOf(w.Player.Pos.Y);
        int x = baseX + w.Rng.RangeInt(-2, 3);
        int y = baseY + w.Rng.RangeInt(-2, 3);
        x = SimMath.ClampInt(x, 0, w.Grid.Width - 2);
        y = SimMath.ClampInt(y, 0, w.Grid.Height - 2);
        w.SpawnZone(x, y, MarkDelayTicks);
    }

    private void Enter(DropperState state)
    {
        State = (int)state;
        StateTick = 0;
    }
}
}
