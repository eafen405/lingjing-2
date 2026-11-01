using Lingjing.Sim;

namespace Lingjing.Level
{
    /// <summary>levels.json 里补给点的 kind 字符串与 <see cref="PickupKind"/> 的唯一映射。</summary>
    public static class SupplyKindNames
    {
        public static bool TryParse(string name, out PickupKind kind)
        {
            switch (name)
            {
                case "Health":
                    kind = PickupKind.Health;
                    return true;
                case "Energy":
                    kind = PickupKind.Energy;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }
    }
}
