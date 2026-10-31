using BattleSim.Core;
using BattleSim.Nav;

namespace BattleSim.Fairy;

/// <summary>
/// Fairy 的决策接口：输入世界快照，输出一条玩家也能执行的指令。
/// 约束（规格 §Further Notes）：只能读玩家能看到的信息、只能做玩家能做的动作（移动 + 正反切）。
/// </summary>
public interface IPlanner
{
    Command Decide(BattleSnapshot s);
}

/// <summary>与玩家输入完全同构的指令。SpinSign 是「期望的旋转方向」，RequestReverse 是「就地翻转」。</summary>
public readonly struct Command
{
    public readonly Vec2 Move;
    public readonly int SpinSign;
    public readonly bool RequestReverse;

    public Command(Vec2 move, int spinSign, bool requestReverse)
    {
        Move = move;
        SpinSign = spinSign;
        RequestReverse = requestReverse;
    }

    public static readonly Command None = new(Vec2.Zero, 0, false);
}

/// <summary>世界快照。规划器只读它，不得反向修改世界（保证重放一致）。</summary>
public sealed class BattleSnapshot
{
    public int Tick;
    public Vec2 PlayerPos;
    public Vec2 PlayerVel;
    public Vec2 PlayerMoveDir;
    public double PlayerRadius;
    public double PlayerSpeed;
    public double PlayerHp;
    public double PlayerMaxHp;
    public double PlayerEnergy;
    public int SpinSign;
    public double Omega;
    public double BladeRadius;
    public int BladeCount;
    public double BladePhase;
    public bool FairyActive;
    public Grid Grid = null!;
    public SnapshotEnemy[] Enemies = Array.Empty<SnapshotEnemy>();
    public SnapshotProjectile[] Projectiles = Array.Empty<SnapshotProjectile>();
    public SnapshotZone[] Zones = Array.Empty<SnapshotZone>();
}

public readonly struct SnapshotEnemy
{
    public readonly int Id;
    public readonly Enemies.EnemyKind Kind;
    public readonly Vec2 Pos;
    public readonly double Radius;
    public readonly double Hp;
    public readonly int State;
    public readonly bool Alive;
    public readonly bool Flying;
    public readonly double MoveSpeed;

    public SnapshotEnemy(int id, Enemies.EnemyKind kind, Vec2 pos, double radius, double hp, int state, bool alive, bool flying, double moveSpeed)
    {
        Id = id;
        Kind = kind;
        Pos = pos;
        Radius = radius;
        Hp = hp;
        State = state;
        Alive = alive;
        Flying = flying;
        MoveSpeed = moveSpeed;
    }
}

public readonly struct SnapshotProjectile
{
    public readonly Vec2 Pos;
    public readonly Vec2 Vel;

    public SnapshotProjectile(Vec2 pos, Vec2 vel)
    {
        Pos = pos;
        Vel = vel;
    }
}

public readonly struct SnapshotZone
{
    public readonly double X;
    public readonly double Y;
    public readonly double Width;
    public readonly double Height;
    public readonly int WarnTicksLeft;

    public SnapshotZone(double x, double y, double width, double height, int warnTicksLeft)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        WarnTicksLeft = warnTicksLeft;
    }
}
