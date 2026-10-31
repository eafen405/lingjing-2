using Lingjing.Core;
using System;

namespace Lingjing.Combat
{

/// <summary>
/// 伤害与充能数值的唯一权威。所有常量来自 game-design.md §4.2 / §4.4 / §5。
/// </summary>
public static class DamageRules
{
    public const double BladeHitDamage = 10.0;

    /// <summary>同一把刀对同一敌人的命中冷却（0.1s = 6 tick）。</summary>
    public const int BladePerTargetCooldownTicks = 6;

    public const double EnemyMaxHp = 30.0;
    public const double EnemyRadius = 0.35;

    public const double EnergyPerHit = 2.0;
    public const double EnergyPerKill = 15.0;
    public const double EnergyMax = 100.0;

    /// <summary>尾灯贴身持续伤害：每秒 8（按 dt 摊到每 tick）。</summary>
    public const double TailLightContactDps = 8.0;

    /// <summary>撞针冲刺撞击：一次性 15。</summary>
    public const double StrikerDashImpact = 15.0;

    /// <summary>钉枪弹丸：8。</summary>
    public const double NailDamage = 8.0;

    /// <summary>坠物落区：12。</summary>
    public const double FallingZoneDamage = 12.0;

    /// <summary>
    /// 以"玩家移动方向 + 敌人方位"计算相对角速度：m 在敌人切向 t̂ 上的投影。
    /// 这是 P2 的目标量，也是「顺着刀刃切向走命中最多」的数学来源。
    /// </summary>
    public static double OmegaRel(double omega, int spinSign, Vec2 moveDir, double moveSpeed, Vec2 toEnemy, double ringRadius)
    {
        Vec2 dir = toEnemy.Normalized();
        if (dir == Vec2.Zero)
        {
            return omega;
        }

        Vec2 tangent = dir.Perpendicular();
        double m = moveDir.Dot(tangent) * moveSpeed;
        return omega + spinSign * m / ringRadius;
    }

    /// <summary>每秒命中刀数：N·|ω_rel| / 2π。</summary>
    public static double HitsPerSecond(int bladeCount, double omegaRel)
        => bladeCount * Math.Abs(omegaRel) / (Math.PI * 2.0);
}
}
