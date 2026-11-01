using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Sim;
using System;

namespace Lingjing.AI
{

/// <summary>
/// 尾灯：流场追玩家。状态机只有 Approach 与 Death（死亡由 Hp 表达，保留枚举位以对齐规格）。
/// 贴身伤害由模拟层的接触判定施加。速度与生命见 <see cref="EnemyTuning"/>。
/// </summary>
public sealed class TailLight : Enemy
{
    public enum TailLightState
    {
        Approach = 0,
        Death = 1
    }

    public TailLight() : base(EnemyKind.TailLight)
    {
    }

    public override string StateName => ((TailLightState)State).ToString();

    public override void Update(WorldState w)
    {
        StateTick++;
        if (!Alive)
        {
            State = (int)TailLightState.Death;
            Hold();
            return;
        }

        // 追兵只读流场梯度；流场没覆盖到（例如玩家在墙里）时退化为直冲。
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
