using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.UI
{
    /// <summary>
    /// 关卡选择：每关一个节点，锁住或没有场景的节点灰显并标「未解锁」。
    /// 面板挂在主菜单 Canvas 下，默认隐藏，返回按钮只关面板不切场景。
    /// </summary>
    public sealed class LevelSelectPanel : MonoBehaviour
    {
        static readonly Color TextColor = UiPalette.Text;
        static readonly Color LockedTextColor = UiPalette.Disabled;
        static readonly Color NodeColor = UiPalette.Capsule;
        static readonly Color LockedNodeColor = UiPalette.Window;

        sealed class Node
        {
            public int Level;
            public Button Button;
            public Image Background;
            public Text Label;
            public Text Status;
        }

        Node[] _nodes;

        public RectTransform Root { get; private set; }

        /// <summary>关卡面板自己的 Doozy 视图，挂在主菜单视图下面，默认隐藏。</summary>
        public DoozyViewHost View { get; private set; }

        public bool Visible => View != null && View.IsVisible;

        public static LevelSelectPanel Build(Transform parent)
        {
            DoozyViewHost view = DoozyViewHost.Create(
                parent, DoozyViewHost.LevelSelectViewName, DoozyViewHost.LevelSelectSortingOrder);

            var panel = view.Root.AddComponent<LevelSelectPanel>();
            panel.View = view;
            panel.Root = (RectTransform)view.Root.transform;
            UiKit.CreateFullScreen(panel.Root, "Dim", UiPalette.Dim);
            panel.BuildUi(panel.Root);
            return panel;
        }

        void BuildUi(Transform root)
        {
            var center = new Vector2(0.5f, 0.5f);
            RectTransform card = UiKit.CreatePanel(root, "Card", center, center, Vector2.zero,
                new Vector2(600f, 400f), UiPalette.Panel);

            UiKit.CreateTitle(card, UiText.LevelSelect, 38, TextAnchor.MiddleCenter, center, center,
                new Vector2(0f, 160f), new Vector2(520f, 40f), TextColor);

            _nodes = new Node[GameFlow.LevelCount];
            for (int i = 0; i < GameFlow.LevelCount; i++)
            {
                int level = i + 1;
                var node = new Node { Level = level };
                node.Button = UiKit.CreateButton(card, GameFlow.LevelName(level), center, center,
                    new Vector2(0f, 80f - i * 80f), new Vector2(480f, 70f), () => UiNav.LoadLevel(level), 26);
                node.Background = node.Button.GetComponent<Image>();
                node.Button.targetGraphic = node.Background;

                Transform labelTx = node.Button.transform.Find("Label");
                node.Label = labelTx != null
                    ? labelTx.GetComponent<Text>()
                    : node.Button.GetComponentInChildren<Text>(true);

                node.Status = UiKit.CreateLabel(node.Button.transform, string.Empty, 18, TextAnchor.MiddleRight,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(160f, 28f),
                    UiPalette.Warning);

                _nodes[i] = node;
            }

            UiKit.CreateButton(card, UiText.Back, center, center,
                new Vector2(0f, -148f), new Vector2(240f, 52f), Hide, 24);

            Refresh();
        }

        public void Show()
        {
            Refresh();
            View.Show(true);
            Root.SetAsLastSibling();
        }

        public void Hide()
        {
            View.Hide(true);
        }

        /// <summary>解锁状态以 GameFlow/GameSave 为准，每次打开面板重新算一遍。</summary>
        void Refresh()
        {
            if (_nodes == null)
            {
                return;
            }

            for (int i = 0; i < _nodes.Length; i++)
            {
                Node node = _nodes[i];
                bool playable = GameFlow.IsUnlocked(node.Level) && GameFlow.HasScene(node.Level);

                node.Button.interactable = playable;
                node.Background.color = playable ? NodeColor : LockedNodeColor;
                if (node.Label != null)
                {
                    node.Label.color = playable ? TextColor : LockedTextColor;
                }

                // 未解锁与「已解锁但没做场景」都算不可进，统一显示同一个锁定标记。
                node.Status.text = UiText.Locked;
                node.Status.gameObject.SetActive(!playable);
            }
        }
    }
}
