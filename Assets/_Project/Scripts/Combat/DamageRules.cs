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
    /// 同一把刀对同一只以骸的再次命中间隔（tick）= 2π / ω，也就是这把刀转满一圈的时间。
    /// 与玩家移速、以骸方位都无关：命中机会恒定，走位只改「伤害倍率」（见 <see cref="RelativeDamageFactor"/>）。
    /// 旧口径按 |ω_rel| 算间隔，侧向移动时 ω_rel 会趋近 0，同一把刀对同一只以骸七十多秒不再命中、
    /// 而且玩家完全看不出来——"打着打着突然不掉血"就是它，已废弃。
    /// </summary>
    public static int BladeHitIntervalTicks(double omega)
    {
        double w = Math.Abs(omega);
        if (w < MinOmegaRel)
        {
            w = MinOmegaRel;
        }

        return (int)Math.Ceiling(Math.PI * 2.0 / (w * SimConstants.Dt));
    }

    /// <summary>相对速度对伤害的倍率上下限：顺扫最多 ×1.3，逆扫最低 ×0.8。</summary>
    public const double MaxDamageFactor = 1.3;
    public const double MinDamageFactor = 0.8;

    /// <summary>
    /// 顺着刃口切向走时 ω_rel 变大、伤害最多 ×1.3；逆着走最多 ×0.8。
    /// 这是"走位有收益"保留下来的方式——但它不再决定能不能命中，所以不会出现隐形死区。
    /// </summary>
    public static double RelativeDamageFactor(double omegaRel, double omega)
    {
        if (omega <= MinOmegaRel)
        {
            return 1.0;
        }

        double factor = omegaRel / omega;
        return factor < MinDamageFactor ? MinDamageFactor : (factor > MaxDamageFactor ? MaxDamageFactor : factor);
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
