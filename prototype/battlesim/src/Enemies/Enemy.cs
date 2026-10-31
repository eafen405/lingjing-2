using BattleSim.Combat;
using BattleSim.Core;
using BattleSim.Sim;

namespace BattleSim.Enemies;

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
/// 死亡后不再造成任何伤害（碰撞伤害由模拟层检查 Alive）。
/// </summary>
public abstract class Enemy
{
    public int Id;
    public EnemyKind Kind;
    public Vec2 Pos;
    public Vec2 Vel;
    public double Radius = DamageRules.EnemyRadius;
    public double MaxHp = DamageRules.EnemyMaxHp;
    public double Hp = DamageRules.EnemyMaxHp;
    public double MoveSpeed;
    public int State;
    public int StateTick;
    public int SpawnTick;

    /// <summary>每把刀一个独立冷却（tick），防止一帧多判。</summary>
    public readonly int[] BladeCooldown = new int[BladeRing.BladeCount];

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
