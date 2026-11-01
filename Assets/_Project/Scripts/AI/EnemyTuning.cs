namespace Lingjing.AI
{
    /// <summary>
    /// 每种以骸的数值表：生命、移速、碰撞半径、接触与技能伤害。
    /// 行为参数（预警时长、射程、弹速）仍留在各自的类里——它们是行为的一部分，不是平衡旋钮。
    /// 玩家的数值在 <c>Lingjing.Core.Tuning</c>。
    /// </summary>
    public static class EnemyTuning
    {
        /// <summary>普通以骸的生命；恶名以骸单独一档。</summary>
        public static double MaxHp(EnemyKind kind) => kind switch
        {
            EnemyKind.TailLight => 40.0,
            EnemyKind.Striker => 55.0,
            EnemyKind.NailGun => 45.0,
            EnemyKind.Dropper => 60.0,
            EnemyKind.ChainBoss => 600.0,
            _ => 40.0
        };

        /// <summary>追击移速。必须全部低于玩家的移速，否则「不掉血」与「保持射程」无法同时成立。</summary>
        public static double MoveSpeed(EnemyKind kind) => kind switch
        {
            EnemyKind.TailLight => 2.8,
            EnemyKind.Striker => 2.4,
            EnemyKind.NailGun => 2.2,
            EnemyKind.Dropper => 2.6,
            EnemyKind.ChainBoss => 2.2,
            _ => 2.4
        };

        /// <summary>碰撞半径：恶名以骸块头更大。</summary>
        public static double Radius(EnemyKind kind) => kind == EnemyKind.ChainBoss ? 0.6 : 0.35;

        // ── 以骸造成的伤害 ─────────────────────────────────────
        /// <summary>尾灯贴身持续伤害：每秒 8（按 dt 摊到每 tick）。</summary>
        public const double TailLightContactDps = 8.0;

        /// <summary>撞针冲刺撞击：一次性 15。</summary>
        public const double StrikerDashImpact = 15.0;

        /// <summary>钉枪弹丸：8。</summary>
        public const double NailDamage = 8.0;

        /// <summary>坠物落区：12。</summary>
        public const double FallingZoneDamage = 12.0;
    }
}
