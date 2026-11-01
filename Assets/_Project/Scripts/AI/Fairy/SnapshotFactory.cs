using Lingjing.Combat;
using Lingjing.Fairy;
using Lingjing.Sim;
using System;
using Lingjing.AI;

namespace Lingjing.Fairy
{

/// <summary>把可变世界复制成只读快照。规划器每 0.1s 拿一次；不在快照里做任何随机。</summary>
public static class SnapshotFactory
{
    public static BattleSnapshot Create(WorldState w, bool fairyActive)
    {
        Player p = w.Player;
        var enemies = new SnapshotEnemy[w.Enemies.Count];
        for (int i = 0; i < w.Enemies.Count; i++)
        {
            Enemy e = w.Enemies[i];
            enemies[i] = new SnapshotEnemy(e.Id, e.Kind, e.Pos, e.Radius, e.Hp, e.State, e.Alive, e.Flying, e.MoveSpeed);
        }

        var projectiles = new SnapshotProjectile[w.Projectiles.Count];
        for (int i = 0; i < w.Projectiles.Count; i++)
        {
            Projectile pr = w.Projectiles[i];
            projectiles[i] = new SnapshotProjectile(pr.Pos, pr.Vel);
        }

        var zones = new SnapshotZone[w.Zones.Count];
        for (int i = 0; i < w.Zones.Count; i++)
        {
            HazardZone z = w.Zones[i];
            zones[i] = new SnapshotZone(z.X, z.Y, z.Width, z.Height, z.WarnTicksLeft);
        }

        return new BattleSnapshot
        {
            Tick = w.Tick,
            PlayerPos = p.Pos,
            PlayerVel = p.Vel,
            PlayerMoveDir = p.MoveDir,
            PlayerRadius = p.Radius,
            PlayerSpeed = p.Speed,
            PlayerHp = p.Hp,
            PlayerMaxHp = p.MaxHp,
            PlayerEnergy = p.Energy,
            SpinSign = p.SpinSign,
            Omega = p.Ring.Omega,
            BladeRadius = p.Ring.OuterRadius,
            BladeCount = p.Ring.BladeCount,
            BladePhase = p.Ring.Phase,
            FairyActive = fairyActive,
            Grid = w.Grid,
            Enemies = enemies,
            Projectiles = projectiles,
            Zones = zones
        };
    }
}
}
