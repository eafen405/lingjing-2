using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Sim;
using System;

namespace Lingjing.AI
{

/// <summary>
/// Boss 占位。本阶段只做"会走会挨打"的空壳：沿流场接近玩家、可被击杀。
/// TODO(下一阶段)：连环 —— Approach → Windup(1s) → Dash(0.4s) → Recover(1.5s)；
///                  ≤50% HP 追加 Summon（每 8s，2 只钉枪，同屏 ≤4）；
///                  骤雨 —— 技能调度器 4s 冷却、Barrage（6 块矩形，1 块必含玩家）与 Summon 不重叠。
/// </summary>
public sealed class ChainBoss : Enemy
{
    public enum BossState
    {
        Approach = 0,
        Death = 1
    }

    public ChainBoss() : base(EnemyKind.ChainBoss)
    {
    }

    public override string StateName => ((BossState)State).ToString();

    public override void Update(WorldState w)
    {
        StateTick++;
        if (!Alive)
        {
            State = (int)BossState.Death;
            Hold();
            return;
        }

        Vec2 dir;
        if (w.Flow.TryWaypoint(Pos, out Vec2 waypoint))
        {
            dir = (waypoint - Pos).Normalized();
            if (dir == Vec2.Zero)
            {
                dir = (w.Player.Pos - Pos).Normalized();
            }
        }
        else
        {
            dir = (w.Player.Pos - Pos).Normalized();
        }

        MoveAlong(w, dir, MoveSpeed);
    }
}
}
