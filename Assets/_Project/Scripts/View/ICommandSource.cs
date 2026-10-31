using Lingjing.Fairy;

namespace Lingjing.View
{
    /// <summary>指令来源：键盘、触屏、或将来的自动演示，都只实现这一个接口。</summary>
    public interface ICommandSource
    {
        Command CurrentCommand { get; }

        /// <summary>本帧是否按下了技能键（Fairy 代行）。读到即消耗，同一帧只触发一次。</summary>
        bool TakeSkillPress();
    }
}
