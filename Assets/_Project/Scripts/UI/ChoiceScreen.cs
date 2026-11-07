using System;
using Lingjing.Level;
using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.UI
{
    /// <summary>
    /// 升级三选一：三张卡横排，点一张立刻生效并关闭。
    /// 只负责显示与回调；把哪几张牌发给它是 LevelRun 的事，加不加速度也是。
    /// </summary>
    public sealed class ChoiceScreen : MonoBehaviour
    {
        readonly Text[] _names = new Text[GrowthRules.OptionCount];
        readonly Text[] _effects = new Text[GrowthRules.OptionCount];
        readonly GameObject[] _cards = new GameObject[GrowthRules.OptionCount];

        Action<GrowthKind> _pick;

        public DoozyViewHost View { get; private set; }

        public bool Visible => View != null && View.IsVisible;

        /// <summary>最近一次显示出来的牌面，供探针核对。</summary>
        public GrowthKind ShownAt(int index) => _shown[index];

        readonly GrowthKind[] _shown = new GrowthKind[GrowthRules.OptionCount];

        public static ChoiceScreen Build(Action<GrowthKind> pick)
        {
            Canvas canvas = DoozyViewHost.EnsureCanvas();
            DoozyViewHost view = DoozyViewHost.Create(
                canvas.transform, DoozyViewHost.ChoiceViewName, DoozyViewHost.ChoiceSortingOrder);

            var screen = view.Root.AddComponent<ChoiceScreen>();
            screen.View = view;
            screen._pick = pick;
            screen.BuildUi(view.Root.transform);
            return screen;
        }

        void BuildUi(Transform root)
        {
            // Dim 留在根节点满铺；标题与三张牌收进安全区，横屏刘海机左右两张牌不被裁。
            UiKit.CreateFullScreen(root, "Dim", UiPalette.Dim);
            RectTransform safe = DoozyViewHost.CreateSafeArea(root);

            var center = new Vector2(0.5f, 0.5f);
            UiKit.CreateTitle(safe, UiText.PickUpgrade, 36, TextAnchor.MiddleCenter, center, center,
                new Vector2(0f, 190f), new Vector2(700f, 48f), UiPalette.Signal);

            // 三张卡：正中一张，左右各偏 ±280。
            const float spacing = 280f;
            for (int i = 0; i < GrowthRules.OptionCount; i++)
            {
                int index = i;
                float x = (i - (GrowthRules.OptionCount - 1) * 0.5f) * spacing;

                RectTransform card = UiKit.CreatePanel(safe, "Card" + i, center, center,
                    new Vector2(x, 10f), new Vector2(250f, 300f), UiPalette.Panel);

                _names[i] = UiKit.CreateTitle(card, string.Empty, 36, TextAnchor.MiddleCenter, center, center,
                    new Vector2(0f, 78f), new Vector2(230f, 48f), UiPalette.Warning);

                _effects[i] = UiKit.CreateLabel(card, string.Empty, 28, TextAnchor.MiddleCenter, center, center,
                    new Vector2(0f, 22f), new Vector2(230f, 40f), UiPalette.Success);

                UiKit.CreateButton(card, UiText.Choose, center, center,
                    new Vector2(0f, -78f), new Vector2(190f, 52f), () => OnPick(index), 24);

                _cards[i] = card.gameObject;
            }
        }

        /// <summary>显示本次的三张牌；不足三张时多余的卡隐藏。</summary>
        public void Show(GrowthState growth)
        {
            int count = growth.OptionCount;
            for (int i = 0; i < GrowthRules.OptionCount; i++)
            {
                bool has = i < count;
                _cards[i].SetActive(has);
                if (!has)
                {
                    continue;
                }

                GrowthKind kind = growth.OptionAt(i);
                _shown[i] = kind;
                _names[i].text = UiText.GrowthName(kind);
                _effects[i].text = UiText.GrowthEffect(kind);
            }

            View.Show(true);
            View.FadeIn();
        }

        public void Hide() => View.Hide(true);

        void OnPick(int index)
        {
            GrowthKind kind = _shown[index];
            Hide();
            _pick?.Invoke(kind);
        }
    }
}
