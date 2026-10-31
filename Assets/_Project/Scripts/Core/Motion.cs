using Lingjing.Core;
using System;

namespace Lingjing.Core
{

/// <summary>
/// 运动学工具。移动是**连续**的（浮点世界坐标 + 60Hz 积分），不做像素吸附：
/// 32px 网格只用于寻路，像素只用于落区矩形的对齐与美术换算。
///
/// 手感参照球球大作战这类俯视移动：任意 360° 方向、速度平滑加速/减速，
/// 方向变化是弧线而不是瞬间转向，也没有"车头朝向"这个概念。
/// 实现方式就是「速度插值」——每 tick 把当前速度朝目标速度逼近固定增量。
/// </summary>
public static class Motion
{
    /// <summary>把 current 朝 target 逼近最多 maxDelta（标量速度插值，保留方向变化的连续性）。</summary>
    public static Vec2 MoveTowards(Vec2 current, Vec2 target, double maxDelta)
    {
        Vec2 delta = target - current;
        double length = delta.Length;
        if (length <= maxDelta || length < 1e-12)
        {
            return target;
        }

        return current + delta / length * maxDelta;
    }
}
}
