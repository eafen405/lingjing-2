using BattleSim.Combat;
using BattleSim.Core;
using BattleSim.Sim;

namespace BattleSim.Enemies;

/// <summary>
/// 尾灯：流场追玩家，移速 2.6。状态机只有 Approach 与 Death（死亡由 Hp 表达，保留枚举位以对齐规格）。
/// 贴身伤害由模拟层的接触判定施加（每秒 8）。
/// </summary>
public sealed class TailLight : Enemy
{
    public const double SpeedConst = 2.6;

    public enum TailLightState
    {
        Approach = 0,
        Death = 1
    }

    public TailLight()
    {
        Kind = EnemyKind.TailLight;
        MoveSpeed = SpeedConst;
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
