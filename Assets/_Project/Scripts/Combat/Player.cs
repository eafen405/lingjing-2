using Lingjing.Core;
using System;

namespace Lingjing.Combat
{

/// <summary>
/// 玩家。基础数值全部来自 <see cref="Tuning"/>；关内成长改的是这里的实例字段，不改变量表。
/// 圆 vs 网格碰撞由 Grid.ResolveCircle 负责。
/// </summary>
public sealed class Player
{
    public Vec2 Pos;
    public Vec2 Vel;
    public double Radius = Tuning.PlayerRadius;
    public double Speed = Tuning.PlayerSpeed;
    public double MaxHp = Tuning.PlayerMaxHp;
    public double Hp = Tuning.PlayerMaxHp;

    /// <summary>当前移动方向（单位向量或零）。</summary>
    public Vec2 MoveDir;

    /// <summary>加速度（单位/秒²）：速度朝目标速度平滑逼近，方向变化走弧线。</summary>
    public double Acceleration = Tuning.PlayerAcceleration;

    /// <summary>充能 0..100。命中 +1、击杀 +8，代行期间不回能。</summary>
    public double Energy;

    /// <summary>每把刀的伤害。基础值来自 Tuning，关内成长（game-design §7.1）会改它。</summary>
    public double BladeDamage = Tuning.BladeDamage;

    /// <summary>一次代行能接管多久（tick）。基础值来自 Tuning，关内成长会把它拉长。</summary>
    public int FairyDurationTicks = SimConstants.FairyDurationTicks;

    /// <summary>旋转方向：+1 / −1。</summary>
    public int SpinSign = 1;

    public int ReverseCooldown;

    /// <summary>受击无敌剩余 tick。一次性伤害会点亮它，贴身持续伤害不看它（见 BattleWorld.Hurt）。</summary>
    public int InvulnerableTicks;

    public double DamageTakenTotal;

    public readonly BladeRing Ring = new();

    public bool Dead => Hp <= 0.0;

    public double EnergyRatio => Energy / Tuning.EnergyMax;

    public void TakeDamage(double amount)
    {
        if (amount <= 0.0 || Hp <= 0.0)
        {
            return;
        }

        double applied = Math.Min(amount, Hp);
        Hp -= applied;
        DamageTakenTotal += applied;
    }

    /// <summary>拾取回血。死了不能靠拾取复活，所以 Hp 已经是 0 时直接忽略。</summary>
    public void Heal(double amount)
    {
        if (amount <= 0.0 || Hp <= 0.0)
        {
            return;
        }

        Hp = Math.Min(MaxHp, Hp + amount);
    }
}
}
