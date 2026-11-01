using Lingjing.Core;
using System;
using System.Collections.Generic;

namespace Lingjing.Combat
{

/// <summary>
/// 飞刀环。外圈：初始 4 把、半径 2.0、均匀分布、有相位；ω 的符号由正反切翻转。
/// 刀数、半径、角速度都是**每局可变状态**（关内成长会改），默认值来自 game-design §4.1。
/// 内圈本阶段不实现，只留接口（见 <see cref="InnerRing"/>）。
/// </summary>
public sealed class BladeRing
{
    public const int DefaultBladeCount = 4;
    public const double DefaultOuterRadius = 2.0;
    public const double DefaultOmega = 2.5;

    /// <summary>外圈刀数硬上限（game-design §7.2：初始 4，最多加到 10）。</summary>
    public const int MaxBladeCount = 10;

    /// <summary>刀尖视为点；命中判定用点 vs 敌人圆。</summary>
    public const double TipRadius = 0.0;

    public BladeRing()
    {
        Inner = new InnerRing();
    }

    /// <summary>外圈刀数。上限由调用方约束（成长时加，见 game-design §7.2）。</summary>
    public int BladeCount { get; set; } = DefaultBladeCount;

    public double OuterRadius { get; set; } = DefaultOuterRadius;

    public double Omega { get; set; } = DefaultOmega;

    /// <summary>相位（弧度），只随 ω·dt 前进；不动时刀是静止的。</summary>
    public double Phase { get; private set; }

    /// <summary>内圈占位：半径天赋第 3 级开启，本阶段恒为关闭。</summary>
    public InnerRing Inner { get; }

    /// <summary>相邻两刀之间的扇区宽度（弧度）。</summary>
    public double BladeGap => Math.PI * 2.0 / (BladeCount > 0 ? BladeCount : 1);

    /// <summary>一把刀走过一格扇区的时间；正反切的冷却不能比这更短（game-design §4.3）。</summary>
    public double GapSeconds => BladeGap / (Omega > 0.0 ? Omega : DefaultOmega);

    /// <summary>正反切冷却（tick），向上取整，保证绝不提前解锁。</summary>
    public int ReverseCooldownTicks => (int)Math.Ceiling(GapSeconds / SimConstants.Dt);

    public double AngleOf(int index) => Phase + index * BladeGap;

    public Vec2 TipAt(Vec2 center, int index) => center + Vec2.FromAngle(AngleOf(index)) * OuterRadius;

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
