namespace Lingjing.Core
{
    /// <summary>
    /// 战斗数值的唯一来源：玩家、飞刀、充能、技能、掉落。改手感只改这里。
    /// 为什么不是一张表全包：Core 不能反向依赖 AI / Level（程序集分层），
    /// 所以分种以骸的数值在 <c>Lingjing.AI.EnemyTuning</c>，成长曲线的级数与倍率在
    /// <c>Lingjing.Level.GrowthRules</c>。行为类里不再留任何可调数字。
    /// </summary>
    public static class Tuning
    {
        // ── 玩家 ───────────────────────────────────────────────
        /// <summary>碰撞半径（单位）。</summary>
        public const double PlayerRadius = 0.4;

        /// <summary>移动速度（单位/秒）。</summary>
        public const double PlayerSpeed = 4.8;

        /// <summary>加速度（单位/秒²）：越大转向越硬，越小越滑。</summary>
        public const double PlayerAcceleration = 60.0;

        public const double PlayerMaxHp = 100.0;

        // ── 飞刀环 ─────────────────────────────────────────────
        /// <summary>外圈初始刀数。</summary>
        public const int BladeCount = 4;

        /// <summary>外圈刀数硬上限（成长最多加到它）。</summary>
        public const int MaxBladeCount = 10;

        /// <summary>外圈半径（单位）。</summary>
        public const double RingRadius = 2.2;

        /// <summary>角速度（弧度/秒）。一圈 = 2π/ω。</summary>
        public const double RingOmega = 2.5;

        /// <summary>每把刀的单次伤害。</summary>
        public const double BladeDamage = 12.0;

        // ── 充能 ───────────────────────────────────────────────
        public const double EnergyMax = 100.0;

        /// <summary>命中一次的回能。</summary>
        public const double EnergyPerHit = 1.0;

        /// <summary>击杀一只的回能。</summary>
        public const double EnergyPerKill = 8.0;

        // ── 掉落 ───────────────────────────────────────────────
        /// <summary>走进 0.6 单位自动拾取，不占按键（game-design §7.3）。</summary>
        public const double PickupRadius = 0.6;

        public const double HealthPickupAmount = 30.0;
        public const double EnergyPickupAmount = 30.0;

        /// <summary>击杀掉落概率：血包 12%、能量球 18%；恶名以骸必掉血包。</summary>
        public const double HealthDropChance = 0.12;
        public const double EnergyDropChance = 0.18;

        // ── Fairy 代行 ─────────────────────────────────────────
        /// <summary>初始代行时长（秒）。</summary>
        public const double FairySeconds = 10.0;

        /// <summary>初始代行时长（tick）。10 秒 × 60Hz。</summary>
        public const int FairyTicks = (int)(FairySeconds * 60.0);

        /// <summary>「代行时长」每级加多少秒。</summary>
        public const double FairySecondsPerLevel = 2.0;

        /// <summary>「代行时长」最多几级（10s → 18s）。</summary>
        public const int FairyMaxLevels = 4;
    }
}
