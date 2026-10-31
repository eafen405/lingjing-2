using BattleSim.Core;
using BattleSim.Nav;

namespace BattleSim.Fairy;

/// <summary>
/// 基线规划器（占位，**不是**规格 §3 的 P0–P3 最终规划器）。
///
/// 它只做三件事：
///   1. 16 方向采样，剔除会撞墙的方向；
///   2. 简单评分：躲开敌人 1.0 单位内的方向、尽量把最近敌人保持在刀刃环半径 r 附近；
///   3. 在两个旋转方向里选 |ω_rel| 更大者（5% 门限 + 0.15s 最小间隔，避免抖）。
/// 没有 1 秒前瞻、没有候选点集合、没有字典序筛除。后续接最终规划器时整体替换本文件即可。
/// </summary>
public sealed class BaselinePlanner : IPlanner
{
    private const int DirectionCount = 16;
    private const double AvoidRadius = 1.0;
    private const double LookAheadTicks = 15.0;     // 0.25s
    private const double DangerWeight = 1.0;
    private const double RingWeight = 1.5;
    private const double OmegaWeight = 0.6;
    private const double SwitchRatio = 0.05;        // 相对更优 5% 才翻
    private const int SwitchMinIntervalTicks = 9;   // 0.15s

    private static readonly Vec2[] Dirs = BuildDirs();

    private int _lastSwitchTick = -1000;
    private Vec2 _lastDir;

    public Command Decide(BattleSnapshot s)
    {
        Vec2 bestDir = Vec2.Zero;
        int bestSign;
        double bestScore = double.NegativeInfinity;
        bool anyCandidate = false;

        for (int k = 0; k < DirectionCount; k++)
        {
            Vec2 dir = Dirs[k];
            // 可走性过滤：探针长度 0.5 → 0.8 覆盖下一次决策的位移。
            if (!s.Grid.CircleFits(s.PlayerPos + dir * 0.5, s.PlayerRadius) ||
                !s.Grid.CircleFits(s.PlayerPos + dir * 0.8, s.PlayerRadius))
            {
                continue;
            }

            anyCandidate = true;
            Vec2 next = s.PlayerPos + dir * (s.PlayerSpeed * LookAheadTicks * SimConstants.Dt);

            double danger = 0.0;
            double nearest = double.PositiveInfinity;
            for (int i = 0; i < s.Enemies.Length; i++)
            {
                SnapshotEnemy e = s.Enemies[i];
                if (!e.Alive)
                {
                    continue;
                }

                double d = next.DistanceTo(e.Pos);
                if (d < nearest)
                {
                    nearest = d;
                }

                if (d < AvoidRadius)
                {
                    danger += (AvoidRadius - d) * 40.0;
                }
                else
                {
                    danger += 0.4 / (d - AvoidRadius + 0.3);
                }
            }

            double ring = double.IsPositiveInfinity(nearest) ? 0.0 : Math.Abs(nearest - s.BladeRadius) * RingWeight;
            double omegaBest = BestRelativeOmega(s, dir, s.SpinSign);
            // 连续性项：没有它，两个几乎等分的候选会 10Hz 来回翻，看起来像抖动。
            double continuity = _lastDir == Vec2.Zero ? 0.0 : 0.35 * dir.Dot(_lastDir);
            double score = omegaBest * OmegaWeight - danger * DangerWeight - ring + continuity;

            if (score > bestScore)
            {
                bestScore = score;
                bestDir = dir;
            }
        }

        if (!anyCandidate)
        {
            // 候选全部不可达：保持当前朝向小幅移动（防抖）。
            return new Command(s.PlayerMoveDir.Normalized() * 0.25, s.SpinSign, false);
        }

        _lastDir = bestDir;

        // 在两个旋转方向里选 |ω_rel| 更大者，带 5% 门限与 0.15s 最小间隔。
        double vPlus = SumRelativeOmega(s, bestDir, 1);
        double vMinus = SumRelativeOmega(s, bestDir, -1);
        int wanted = vPlus >= vMinus ? 1 : -1;
        double vWanted = Math.Max(vPlus, vMinus);
        double vOther = Math.Min(vPlus, vMinus);

        bool worthSwitching = wanted != s.SpinSign &&
                              vWanted > vOther * (1.0 + SwitchRatio) &&
                              s.Tick - _lastSwitchTick >= SwitchMinIntervalTicks &&
                              s.Tick > 0;

        if (worthSwitching)
        {
            _lastSwitchTick = s.Tick;
            bestSign = wanted;
        }
        else
        {
            bestSign = s.SpinSign;
        }

        return new Command(bestDir, bestSign, worthSwitching);
    }

    /// <summary>单方向下，两个旋转方向里 |ω_rel| 更大的那个值（仅用于候选评分）。</summary>
    private static double BestRelativeOmega(BattleSnapshot s, Vec2 dir, int fallbackSign)
    {
        double a = SumRelativeOmega(s, dir, 1);
        double b = SumRelativeOmega(s, dir, -1);
        double best = Math.Max(a, b);
        return best > 0.0 ? best : Math.Abs(s.Omega);
    }

    /// <summary>Σ w_i·|ω + s·(m·t̂)/r|，权重按距离衰减。</summary>
    private static double SumRelativeOmega(BattleSnapshot s, Vec2 dir, int sign)
    {
        double sum = 0.0;
        for (int i = 0; i < s.Enemies.Length; i++)
        {
            SnapshotEnemy e = s.Enemies[i];
            if (!e.Alive)
            {
                continue;
            }

            Vec2 toEnemy = e.Pos - s.PlayerPos;
            double d = toEnemy.Length;
            if (d > s.BladeRadius + 2.5 || d < 1e-6)
            {
                continue;
            }

            Vec2 tangent = toEnemy.Perpendicular().Normalized();
            double m = dir.Dot(tangent) * s.PlayerSpeed;
            double omegaRel = s.Omega + sign * m / s.BladeRadius;
            sum += Math.Abs(omegaRel) / (1.0 + 0.25 * d);
        }

        return sum;
    }

    private static Vec2[] BuildDirs()
    {
        var dirs = new Vec2[DirectionCount];
        for (int i = 0; i < DirectionCount; i++)
        {
            dirs[i] = Vec2.FromAngle(i * (Math.PI * 2.0 / DirectionCount));
        }

        return dirs;
    }
}
