using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Sim;
using System;

namespace Lingjing.AI
{

public enum EnemyKind
{
    TailLight,
    Striker,
    NailGun,
    Dropper,
    ChainBoss
}

/// <summary>
/// 以骸基类。每种以骸一个显式状态机；状态编号写进每 tick 哈希。
/// 生命、移速、半径一律从 <see cref="EnemyTuning"/> 取，行为类里不再写死；死亡后不再造成任何伤害。
/// </summary>
public abstract class Enemy
{
    public int Id;
    public EnemyKind Kind;
    public Vec2 Pos;
    public Vec2 Vel;
    public double Radius;
    public double MaxHp;
    public double Hp;
    public double MoveSpeed;
    public int State;
    public int StateTick;
    public int SpawnTick;

    int[] _bladeCooldown = new int[Tuning.BladeCount];

    protected Enemy(EnemyKind kind)
    {
        Kind = kind;
        MaxHp = EnemyTuning.MaxHp(kind);
        Hp = MaxHp;
        MoveSpeed = EnemyTuning.MoveSpeed(kind);
        Radius = EnemyTuning.Radius(kind);
    }

    /// <summary>每把刀一个独立冷却（tick）。间隔由 DamageRules.BladeHitIntervalTicks 按 ω_rel 给出。</summary>
    public int[] BladeCooldown => _bladeCooldown;

    /// <summary>刀数随成长变化，冷却槽按需扩容；新槽从 0 开始，等于新刀落地即可命中。</summary>
    public void EnsureBladeSlots(int count)
    {
        if (count > _bladeCooldown.Length)
        {
            Array.Resize(ref _bladeCooldown, count);
        }
    }

    /// <summary>加速度（单位/秒²）：正常移动走速度插值，方向变化是弧线。</summary>
    public double Acceleration = 20.0;

    public bool Alive => Hp > 0.0;

    public abstract string StateName { get; }

    /// <summary>飞行单位（坠物）无视地面墙，只受地图边界约束。</summary>
    public virtual bool Flying => false;

    public abstract void Update(WorldState w);

    public void TakeDamage(double amount)
    {
        if (Hp <= 0.0)
        {
            return;
        }

        Hp = Math.Max(0.0, Hp - amount);
    }

    public void TickBladeCooldowns()
    {
        for (int i = 0; i < BladeCooldown.Length; i++)
        {
            if (BladeCooldown[i] > 0)
            {
                BladeCooldown[i]--;
            }
        }
    }

    /// <summary>按速度沿方向推进，圆 vs 网格自己解（不用物理引擎）。速度平滑逼近，不做像素吸附。</summary>
    protected void MoveAlong(WorldState w, Vec2 direction, double speed)
    {
        Vec2 target = direction.Normalized();
        if (target == Vec2.Zero)
        {
            Hold();
            return;
        }

        Vel = Motion.MoveTowards(Vel, target * speed, Acceleration * SimConstants.Dt);
        Vec2 next = Pos + Vel * SimConstants.Dt;
        Pos = Flying ? ClampToMap(w.Grid, next) : w.Grid.ResolveCircle(next, Radius);
    }

    /// <summary>原地不动（预警/硬直/瞄准）。</summary>
    protected void Hold()
    {
        Vel = Vec2.Zero;
    }

    protected static Vec2 ClampToMap(Nav.Grid grid, Vec2 p) =>
        new(SimMath.Clamp(p.X, 0.5, grid.Width - 0.5), SimMath.Clamp(p.Y, 0.5, grid.Height - 0.5));
}
}
