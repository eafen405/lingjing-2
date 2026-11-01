using System;
namespace Lingjing.Core
{

/// <summary>
/// 复刻协议里的固定节拍。所有与时间有关的逻辑只能从这里取，禁止 DateTime / Stopwatch 参与模拟。
/// </summary>
public static class SimConstants
{
    public const int TicksPerSecond = 60;

    /// <summary>模拟固定步长 1/60s。</summary>
    public const double Dt = 1.0 / 60.0;

    /// <summary>AI 决策固定 0.1s（每 6 tick 一次）。</summary>
    public const int DecisionTicks = 6;

    /// <summary>流场重算 0.25s。</summary>
    public const int NavRecalcTicks = 15;

    /// <summary>1 单位 = 32px。</summary>
    public const double UnitPixels = 32.0;

    /// <summary>Fairy 代行 4s。</summary>
    public const int FairyDurationTicks = 240;

    /// <summary>每 tick 最多发起 4 条 A*，超出排队（本原型里丢弃，下个决策周期再试）。</summary>
    public const int AstarBudgetPerTick = 4;

    public static int FromSeconds(double seconds) => (int)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero);

    public static double ToSeconds(int ticks) => ticks / (double)TicksPerSecond;
}

public static class SimMath
{
    public static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    public static int ClampInt(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    /// <summary>哈希用：位置量化为 1e-3 后取整。round-half-up，确定性优先于数学美观。</summary>
    public static long QuantizeLong(double v) => (long)Math.Floor(v * 1000.0 + 0.5);

    public static double WrapAngle(double radians)
    {
        const double twoPi = Math.PI * 2.0;
        double r = radians - twoPi * Math.Floor(radians / twoPi);
        return r;
    }
}
}
