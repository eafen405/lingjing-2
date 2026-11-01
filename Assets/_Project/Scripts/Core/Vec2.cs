using System;
namespace Lingjing.Core
{

/// <summary>
/// 世界坐标（单位：格；1 格 = 32px）。纯值类型，无引用、无浮点隐藏状态。
/// </summary>
public readonly struct Vec2 : IEquatable<Vec2>
{
    public readonly double X;
    public readonly double Y;

    public Vec2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static readonly Vec2 Zero = new(0.0, 0.0);
    public static readonly Vec2 UnitX = new(1.0, 0.0);
    public static readonly Vec2 UnitY = new(0.0, 1.0);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);

    public double LengthSquared => X * X + Y * Y;
    public double Length => Math.Sqrt(X * X + Y * Y);

    public Vec2 Normalized()
    {
        double len = Length;
        return len > 1e-12 ? new Vec2(X / len, Y / len) : Zero;
    }

    /// <summary>逆时针旋转 90°。</summary>
    public Vec2 Perpendicular() => new(-Y, X);

    public double Dot(Vec2 o) => X * o.X + Y * o.Y;
    public double Cross(Vec2 o) => X * o.Y - Y * o.X;
    public double DistanceTo(Vec2 o) => (this - o).Length;
    public double DistanceSquaredTo(Vec2 o) => (this - o).LengthSquared;

    /// <summary>
    /// 点到线段 [a,b] 的最短距离。刀刃是「圆心→刀尖」的一条线段，命中判定用它：
    /// 只要以骸贴着这条线，无论它站在环上还是已经贴到身上，都会被扫到。
    /// </summary>
    public double DistanceToSegment(Vec2 a, Vec2 b)
    {
        Vec2 ab = b - a;
        double lengthSquared = ab.LengthSquared;
        if (lengthSquared <= 1e-12)
        {
            return DistanceTo(a);
        }

        double t = (this - a).Dot(ab) / lengthSquared;
        t = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
        return DistanceTo(a + ab * t);
    }

    public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    public static Vec2 FromAngle(double radians) => new(Math.Cos(radians), Math.Sin(radians));

    public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is Vec2 v && Equals(v);
    public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
    public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);
    // 不用 System.HashCode：它带随机化种子，不适合任何可能与复刻相关的路径。
    public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
    public override string ToString() => System.FormattableString.Invariant($"({X:0.###},{Y:0.###})");
}
}
