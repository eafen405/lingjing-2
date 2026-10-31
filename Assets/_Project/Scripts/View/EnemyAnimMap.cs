using Lingjing.AI;

namespace Lingjing.View
{
    /// <summary>
    /// 以骸阶段（内核状态机）→ 动画状态名的唯一映射。
    /// 纯静态、零分配：不碰 Enemy.StateName（枚举 ToString 每次都会分配字符串），
    /// 也不做字符串比较/包含判断；直接按 Kind + 该类型自己的阶段枚举编号一条 switch 落表。
    /// 返回的状态名必须存在于 ArtLibrary 中对应实体的状态集合里。
    /// </summary>
    public static class EnemyAnimMap
    {
        public static string StateFor(Enemy enemy)
        {
            switch ((enemy.Kind, enemy.State))
            {
                // 尾灯只有 Approach / Death，素材里也没有 idle 站桩需求，接近即 move。
                case (EnemyKind.TailLight, (int)TailLight.TailLightState.Approach):
                    return "move";
                case (EnemyKind.TailLight, (int)TailLight.TailLightState.Death):
                    return "death";

                // 撞针：预警播 telegraph、冲刺播 charge；硬直没有单独素材，回 idle。
                case (EnemyKind.Striker, (int)Striker.StrikerState.Approach):
                    return "move";
                case (EnemyKind.Striker, (int)Striker.StrikerState.Windup):
                    return "telegraph";
                case (EnemyKind.Striker, (int)Striker.StrikerState.Dash):
                    return "charge";
                case (EnemyKind.Striker, (int)Striker.StrikerState.Recover):
                    return "idle";
                case (EnemyKind.Striker, (int)Striker.StrikerState.Death):
                    return "death";

                // 钉枪：移动/后撤都播 move；瞄准没有 aim 素材，用 idle 代替；
                // 只有真正开火那一帧走 fire，硬直回 idle。
                case (EnemyKind.NailGun, (int)NailGun.NailState.Reposition):
                    return "move";
                case (EnemyKind.NailGun, (int)NailGun.NailState.Aim):
                    return "idle";
                case (EnemyKind.NailGun, (int)NailGun.NailState.Fire):
                    return "fire";
                case (EnemyKind.NailGun, (int)NailGun.NailState.Recover):
                    return "idle";
                case (EnemyKind.NailGun, (int)NailGun.NailState.Retreat):
                    return "move";
                case (EnemyKind.NailGun, (int)NailGun.NailState.Death):
                    return "death";

                // 坠物：悬停用 idle；标记落区播 telegraph；下坠/落区结算播 impact。
                case (EnemyKind.Dropper, (int)Dropper.DropperState.Hover):
                    return "idle";
                case (EnemyKind.Dropper, (int)Dropper.DropperState.Mark):
                    return "telegraph";
                case (EnemyKind.Dropper, (int)Dropper.DropperState.Drop):
                    return "impact";
                case (EnemyKind.Dropper, (int)Dropper.DropperState.Death):
                    return "death";

                // 链首占位：素材只有 idle/charge/summon/death，没有走路动画，
                // 接近阶段（流场移动）只能先用 idle；死亡照常播 death。
                case (EnemyKind.ChainBoss, (int)ChainBoss.BossState.Approach):
                    return "idle";
                case (EnemyKind.ChainBoss, (int)ChainBoss.BossState.Death):
                    return "death";

                // 未知阶段不猜：五个实体都有 idle，回 idle 保证不把不存在的状态名喂给 ArtLibrary。
                default:
                    return "idle";
            }
        }
    }
}
