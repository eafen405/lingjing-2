using System;
namespace Lingjing.Core
{

/// <summary>
/// xorshift64* 自研伪随机数发生器。不用 System.Random：其内部实现随运行时版本变化，无法保证复刻。
/// 种子为 0 时映射到一个固定非零常量。
/// </summary>
public sealed class Prng
{
    private const ulong DefaultSeed = 0x9E3779B97F4A7C15UL;
    private const ulong Multiplier = 2685821657736338717UL;

    private ulong _state;

    public Prng(ulong seed)
    {
        _state = seed == 0UL ? DefaultSeed : seed;
    }

    /// <summary>当前内部状态，参与每 tick 哈希（能发现"逻辑相同但随机流走岔"）。</summary>
    public ulong State
    {
        get => _state;
    }

    public ulong NextUInt64()
    {
        ulong x = _state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _state = x;
        return unchecked(x * Multiplier);
    }

    /// <summary>[0,1) 均匀分布，53 位精度。</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    public double Range(double minInclusive, double maxExclusive) => minInclusive + (maxExclusive - minInclusive) * NextDouble();

    public int RangeInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        ulong span = (ulong)(maxExclusive - minInclusive);
        return (int)(minInclusive + (long)(NextUInt64() % span));
    }
}
}
