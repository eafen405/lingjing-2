using Lingjing.AI;

namespace Lingjing.Level
{
    /// <summary>levels.json 里 kind 字符串与 <see cref="EnemyKind"/> 的唯一映射。</summary>
    public static class EnemyKindNames
    {
        public static bool TryParse(string name, out EnemyKind kind)
        {
            switch (name)
            {
                case "TailLight":
                    kind = EnemyKind.TailLight;
                    return true;
                case "Striker":
                    kind = EnemyKind.Striker;
                    return true;
                case "NailGun":
                    kind = EnemyKind.NailGun;
                    return true;
                case "Dropper":
                    kind = EnemyKind.Dropper;
                    return true;
                case "ChainBoss":
                    kind = EnemyKind.ChainBoss;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }
    }
}
