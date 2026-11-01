using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Nav;
using System;
using System.Collections.Generic;
using Lingjing.AI;

namespace Lingjing.Sim
{

/// <summary>
/// 世界状态容器：唯一的可变权威。所有系统只改这里，不各自持有一份副本。
/// 集合一律用 List（保序），不使用依赖遍历顺序的哈希表。
/// </summary>
public sealed class WorldState
{
    public Grid Grid = null!;
    public FlowField Flow = null!;
    public AStar AStar = null!;
    public Player Player = null!;
    public Prng Rng = null!;

    public readonly List<Enemy> Enemies = new();
    public readonly List<Projectile> Projectiles = new();
    public readonly List<HazardZone> Zones = new();
    public readonly List<Pickup> Pickups = new();

    public int Tick;

    int _nextPickupId = 1;

    /// <summary>本 tick 剩余的 A* 配额（每 tick 最多 4 条）。</summary>
    public int AstarBudget;

    public bool TryPath(Vec2 from, Vec2 to, out List<Vec2>? path)
    {
        path = null;
        if (AstarBudget <= 0)
        {
            return false;
        }

        AstarBudget--;
        path = AStar.FindPath(from, to);
        return path != null && path.Count > 0;
    }

    public void SpawnProjectile(Vec2 pos, Vec2 velocity)
    {
        Projectiles.Add(new Projectile { Pos = pos, Vel = velocity });
    }

    public void SpawnZone(double x, double y, int warnTicks)
    {
        Zones.Add(new HazardZone { X = x, Y = y, WarnTicksLeft = warnTicks });
    }

    public void SpawnPickup(PickupKind kind, Vec2 pos)
    {
        Pickups.Add(new Pickup { Id = _nextPickupId++, Kind = kind, Pos = pos, SpawnTick = Tick });
    }
}
}
