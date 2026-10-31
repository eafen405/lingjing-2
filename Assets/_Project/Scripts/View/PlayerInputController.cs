using UnityEngine;
using Lingjing.Fairy;

namespace Lingjing.View
{
    /// <summary>
    /// 玩家输入 → Command。竖切阶段先接键盘（WASD/方向键 + 空格正反切），
    /// 触屏摇杆与技能键在 M1 之后替换这里的取样，不改上层。
    /// </summary>
    public sealed class PlayerInputController : MonoBehaviour, ICommandSource
    {
        [Tooltip("技能键（Fairy 代行）暂未接入，先留出位")]
        public KeyCode skillKey = KeyCode.Space;

        [Tooltip("正反切键")]
        public KeyCode reverseKey = KeyCode.LeftShift;

        public Command CurrentCommand { get; private set; } = Command.None;

        int _spinSign = 1;
        bool _skillQueued;

        public bool TakeSkillPress()
        {
            bool queued = _skillQueued;
            _skillQueued = false;
            return queued;
        }

        void Update()
        {
            var move = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            bool reverse = Input.GetKeyDown(reverseKey);
            if (reverse)
            {
                _spinSign = -_spinSign;
            }

            if (Input.GetKeyDown(skillKey))
            {
                _skillQueued = true;
            }

            CurrentCommand = new Command(
                new Lingjing.Core.Vec2(move.x, move.y),
                _spinSign,
                reverse);
        }
    }
}
