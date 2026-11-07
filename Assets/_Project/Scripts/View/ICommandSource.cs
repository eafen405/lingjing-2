using Lingjing.Fairy;

namespace Lingjing.View
{
    /// <summary>指令来源：键盘、触屏、或将来的自动演示，都只实现这一个接口。</summary>
    public interface ICommandSource
    {
        Command CurrentCommand { get; }

        /// <summary>本帧是否按下了技能键（Fairy 代行）。读到即消耗，同一帧只触发一次。</summary>
        bool TakeSkillPress();

        /// <summary>
        /// 把内部的旋转方向直接对齐到世界（0 表示「不知道」、保持原值）。
        /// Fairy 代行期间规划器会自己翻转刀环，交还控制权时不同步的话，世界会把方向不一致当成一次正反切。
        /// </summary>
        void SyncSpinSign(int sign);
    }
}
