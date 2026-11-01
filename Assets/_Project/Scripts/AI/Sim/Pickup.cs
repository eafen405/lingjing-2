using Lingjing.Core;

namespace Lingjing.Sim
{
    /// <summary>地面掉落。飞刀掉落要等磨损系统一起做，见 game-design §7.2–§7.3。</summary>
    public enum PickupKind
    {
        Health,
        Energy
    }

    /// <summary>一件掉落物。走进 0.6 单位自动拾取，不占按键（§7.3）。</summary>
    public sealed class Pickup
    {
        public int Id;
        public PickupKind Kind;
        public Vec2 Pos;
        public bool Taken;
        public int SpawnTick;
    }
}
