using BattleSim.Core;
using BattleSim.Nav;

namespace BattleSim.Combat;

/// <summary>直线弹丸（钉枪的铆钉）。撞墙即消失，命中玩家造成一次伤害。</summary>
public sealed class Projectile
{
    public const double DefaultRadius = 0.12;
    public const int DefaultTtlTicks = 240;

    public Vec2 Pos;
    public Vec2 Vel;
    public double Radius = DefaultRadius;
    public double Damage = DamageRules.NailDamage;
    public int TtlTicks = DefaultTtlTicks;
    public bool Alive = true;

    public void Step(Grid grid)
    {
        Pos += Vel * SimConstants.Dt;
        TtlTicks--;
        if (TtlTicks <= 0)
        {
            Alive = false;
            return;
        }

        if (!grid.CircleFits(Pos, Radius))
        {
            Alive = false;
        }
    }
}

/// <summary>
/// 矩形落区（坠物）。像素对齐：左下角落在整数格上，尺寸 2×2 格。
/// 预警结束后判定一次，若玩家圆与矩形相交则造成 12 点伤害。
/// </summary>
public sealed class HazardZone
{
    public double X;
    public double Y;
    public double Width = 2.0;
    public double Height = 2.0;
    public int WarnTicksLeft;
    public double Damage = DamageRules.FallingZoneDamage;
    public bool Resolved;

    public bool ContainsCircle(Vec2 center, double radius)
    {
        double nearestX = SimMath.Clamp(center.X, X, X + Width);
        double nearestY = SimMath.Clamp(center.Y, Y, Y + Height);
        double dx = center.X - nearestX;
        double dy = center.Y - nearestY;
        return dx * dx + dy * dy <= radius * radius;
    }
}
