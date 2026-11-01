using Lingjing.Core;
using System;

namespace Lingjing.Combat
{

/// <summary>
/// 伤害模型的规则（不再是数值表）。数值在 <see cref="Tuning"/> 与 <c>Lingjing.AI.EnemyTuning</c>。
/// 公式来源：game-design.md §4.2。
/// </summary>
public static class DamageRules
{
    /// <summary>ω_rel 的下限（弧度/秒）。低于它视为刀刃相对该以骸停滞，这把刀在它身上不再命中。</summary>
    public const double MinOmegaRel = 1e-3;

    /// <summary>
    /// 同一把刀对同一只以骸的再次命中间隔（tick）= 2π / |ω_rel|（game-design §4.2）。
    /// 静止时正好是一整圈；顺着刃口切向走变短、逆着走变长；ω_rel → 0 时趋于无穷。
    /// </summary>
    public static int BladeHitIntervalTicks(double omegaRel)
    {
        double w = Math.Abs(omegaRel);
        if (w < MinOmegaRel)
        {
            w = MinOmegaRel;
        }

        return (int)Math.Ceiling(Math.PI * 2.0 / (w * SimConstants.Dt));
    }

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
