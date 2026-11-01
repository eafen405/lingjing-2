using Lingjing.Core;
using System;
using System.Collections.Generic;

namespace Lingjing.Combat
{

/// <summary>
/// 飞刀环。外圈：初始 4 把、半径 2.2、均匀分布、有相位；ω 的符号由正反切翻转。
/// 刀数、半径、角速度都是**每局可变状态**（关内成长会改），基础值来自 <see cref="Tuning"/>。
/// 内圈本阶段不实现，只留接口（见 <see cref="InnerRing"/>）。
/// </summary>
public sealed class BladeRing
{
    /// <summary>刀尖视为点；命中判定用点 vs 敌人圆。</summary>
    public const double TipRadius = 0.0;

    /// <summary>
    /// 刀刃的半宽（单位，≈6px）。刀不是一根数学线：判定按「圆心→刀尖」的线段向外扩这么宽，
    /// 所以以骸贴着刀身就算命中，不必正好落在某个半径上。
    /// </summary>
    public const double BladeHalfWidth = 0.18;

    public BladeRing()
    {
        Inner = new InnerRing();
    }

    /// <summary>外圈刀数。上限由调用方约束（成长时加，见 game-design §7.2）。</summary>
    public int BladeCount { get; set; } = Tuning.BladeCount;

    public double OuterRadius { get; set; } = Tuning.RingRadius;

    public double Omega { get; set; } = Tuning.RingOmega;

    /// <summary>相位（弧度），只随 ω·dt 前进；不动时刀是静止的。</summary>
    public double Phase { get; private set; }

    /// <summary>内圈占位：半径天赋第 3 级开启，本阶段恒为关闭。</summary>
    public InnerRing Inner { get; }

    /// <summary>相邻两刀之间的扇区宽度（弧度）。</summary>
    public double BladeGap => Math.PI * 2.0 / (BladeCount > 0 ? BladeCount : 1);

    /// <summary>一把刀走过一格扇区的时间；正反切的冷却不能比这更短（game-design §4.3）。</summary>
    public double GapSeconds => BladeGap / (Omega > 0.0 ? Omega : Tuning.RingOmega);

    /// <summary>正反切冷却（tick），向上取整，保证绝不提前解锁。</summary>
    public int ReverseCooldownTicks => (int)Math.Ceiling(GapSeconds / SimConstants.Dt);

    public double AngleOf(int index) => Phase + index * BladeGap;

    public Vec2 TipAt(Vec2 center, int index) => center + Vec2.FromAngle(AngleOf(index)) * OuterRadius;

    /// <summary>
    /// 这一刀是否扫到以骸：判定体是「圆心→刀尖」的线段，外扩刀半宽。
    /// 旧版只判刀尖一个点，结果贴身的以骸永远落在半径之内、打不到；改成线段后，
    /// 半径以内的任何位置都会被扫到，走位只影响伤害高低，不再影响「能不能打到」。
    /// </summary>
    public bool Covers(Vec2 center, int index, Vec2 target, double targetRadius)
        => target.DistanceToSegment(center, TipAt(center, index)) <= targetRadius + BladeHalfWidth;

    public void Advance(int spinSign, double dt)
    {
        Phase = SimMath.WrapAngle(Phase + spinSign * Omega * dt);
    }

    /// <summary>外圈当前切线方向（用于伤害模型与规划器评分）。</summary>
    public Vec2 TangentAt(int index) => Vec2.FromAngle(AngleOf(index) + Math.PI * 0.5);
}

/// <summary>
/// 内圈占位。规格：半径 = 开启瞬间外圈的 0.6 倍并锁死；刀数 = 外圈数量的一半；方向相反；不磨损。
/// 本阶段不参与模拟，只保证后续接入时不动外圈代码。
/// </summary>
public sealed class InnerRing
{
    public bool Enabled => false;
    public int BladeCount => 0;
    public double Radius => 0.0;

    public IReadOnlyList<Vec2> SampleTips(Vec2 center, double outerRadius) => Array.Empty<Vec2>();
}
}
