using Lingjing.Core;
using System;
using System.Collections.Generic;

namespace Lingjing.Nav
{

/// <summary>
/// A* 路径跟随器：缓存路径、按段推进、走到下一段时更新当前方向。
/// 撞针与钉枪共用；追击敌人不用它（走流场）。
/// </summary>
public sealed class PathFollower
{
    private readonly List<Vec2> _points = new();
    private int _index;
    private int _age;

    public bool HasPath => _index < _points.Count;
    public int Age => _age;
    public int Remaining => Math.Max(0, _points.Count - _index);
    public Vec2 Goal { get; private set; } = Vec2.Zero;

    public void Clear()
    {
        _points.Clear();
        _index = 0;
        _age = 0;
    }

    /// <summary>直接接收一条已经算好的路径（A* 有每 tick 配额，路径由世界层统一取）。</summary>
    public bool SetPath(List<Vec2>? path, Vec2 goal)
    {
        if (path == null || path.Count == 0)
        {
            return false;
        }

        _points.Clear();
        _points.AddRange(path);
        _index = 0;
        _age = 0;
        Goal = goal;
        return true;
    }

    public void TickAge() => _age++;

    /// <summary>朝当前路径点走；到达 0.25 格内就切下一段。没有路径时返回零向量。</summary>
    public Vec2 Steer(Vec2 pos)
    {
        while (_index < _points.Count && pos.DistanceTo(_points[_index]) <= 0.25)
        {
            _index++;
        }

        if (_index >= _points.Count)
        {
            return Vec2.Zero;
        }

        return (_points[_index] - pos).Normalized();
    }
}

/// <summary>16 方向贪心转向前馈：先按"想去哪"打分，再剔除会撞墙的方向。</summary>
public static class Steering
{
    private const int DirectionCount = 16;

    public static Vec2 PickDirection(Grid grid, Vec2 pos, Vec2 desired, double radius, double probeLength = 0.5, Vec2 previousDirection = default)
    {
        Vec2 dir = desired.Normalized();
        if (dir == Vec2.Zero)
        {
            return Vec2.Zero;
        }

        Vec2 previous = previousDirection.Normalized();
        double bestScore = double.NegativeInfinity;
        Vec2 best = Vec2.Zero;
        for (int i = 0; i < DirectionCount; i++)
        {
            var candidate = Vec2.FromAngle(i * (Math.PI * 2.0 / DirectionCount));
            bool blocked = !grid.CircleFits(pos + candidate * probeLength, radius);
            double score = candidate.Dot(dir);
            if (previous != Vec2.Zero)
            {
                // 动量项：贴墙时不要每 0.1s 在 ±y 之间来回抖。
                score += 0.2 * candidate.Dot(previous);
            }

            if (blocked)
            {
                score -= 10.0;
            }

            // 平局保留下标最小者：确定性。
            if (score > bestScore + 1e-12)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return bestScore <= -9.0 ? Vec2.Zero : best;
    }
}
}
