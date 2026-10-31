using BattleSim.Core;

namespace BattleSim.Combat;

/// <summary>
/// 飞刀环。外圈：N=4、半径 2.0、均匀分布、有相位；ω 的符号由正反切翻转。
/// 内圈本阶段不实现，只留接口（见 <see cref="InnerRing"/>）。
/// </summary>
public sealed class BladeRing
{
    public const int BladeCount = 4;
    public const double OuterRadius = 2.0;
    public const double Omega = 2.5;

    /// <summary>刀尖视为点；命中判定用点 vs 敌人圆。</summary>
    public const double TipRadius = 0.0;

    public BladeRing()
    {
        Inner = new InnerRing();
    }

    /// <summary>相位（弧度），只随 ω·dt 前进；不动时刀是静止的。</summary>
    public double Phase { get; private set; }

    /// <summary>内圈占位：半径天赋第 3 级开启，本阶段恒为关闭。</summary>
    public InnerRing Inner { get; }

    public double AngleOf(int index) => Phase + index * (Math.PI * 2.0 / BladeCount);

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
