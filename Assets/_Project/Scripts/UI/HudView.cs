using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.UI
{
    /// <summary>
    /// 战斗内 HUD：只读战斗数据，暂停键只向外发回调，不直接引用 BattleRunner。
    /// 左上内容贴 40 安全边距，右上放暂停；左下摇杆与右下按钮的位置全部留空。
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        /// <summary>左右安全边距（参考像素）。</summary>
        public const float SafeMargin = 40f;

        static readonly Color TextColor = new Color(0.92f, 0.94f, 0.96f, 1f);
        static readonly Color FairyColor = new Color(0.95f, 0.82f, 0.45f, 1f);
        static readonly Color BarBackground = new Color(0.055f, 0.07f, 0.09f, 0.92f);

        Canvas _canvas;
        Text _hpText;
        Text _energyText;
        Text _fairyText;
        UiBar _hpBar;
        UiBar _energyBar;

        public static HudView Build(Action onPause)
        {
            UiKit.EnsureEventSystem();

            Canvas canvas = UiKit.CreateCanvas("HudCanvas", 20);
            var view = canvas.gameObject.AddComponent<HudView>();
            view.Init(canvas, onPause);
            return view;
        }

        void Init(Canvas canvas, Action onPause)
        {
            _canvas = canvas;
            Transform root = canvas.transform;

            RectTransform card = UiKit.CreatePanel(root, "Stats", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(SafeMargin, -SafeMargin), new Vector2(300f, 158f),
                new Color(0.055f, 0.07f, 0.09f, 0.82f));
            card.GetComponent<Image>().raycastTarget = false;

            _hpText = UiKit.CreateLabel(card, string.Empty, 22, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(268f, 26f), TextColor);

            _hpBar = UiKit.CreateBar(card, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -42f), new Vector2(268f, 12f),
                BarBackground, new Color(0.86f, 0.33f, 0.36f, 1f));

            _energyText = UiKit.CreateLabel(card, string.Empty, 22, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -62f), new Vector2(268f, 26f), TextColor);

            _energyBar = UiKit.CreateBar(card, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -92f), new Vector2(268f, 12f),
                BarBackground, new Color(0.45f, 0.82f, 0.95f, 1f));

            _fairyText = UiKit.CreateLabel(card, string.Empty, 20, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -112f), new Vector2(268f, 26f), FairyColor);
            _fairyText.gameObject.SetActive(false);

            // 暂停键贴右上，与 HUD 数值一起远离左下摇杆和右下技能键。
            UiKit.CreateButton(root, UiText.Pause, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-SafeMargin, -SafeMargin), new Vector2(96f, 46f), onPause, 22);
        }

        /// <summary>只做显示；数值与倒计时由 BattleRunner 每帧灌进来。</summary>
        public void SetStats(float hp, float maxHp, float energy, float energyMax, bool fairyActive, float fairySeconds)
        {
            maxHp = Mathf.Max(1f, maxHp);
            energyMax = Mathf.Max(1f, energyMax);

            _hpText.text = $"{UiText.Hp} {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";
            _hpBar.SetValue(hp / maxHp);

            _energyText.text = $"{UiText.Energy} {Mathf.FloorToInt(energy)}/{Mathf.CeilToInt(energyMax)}";
            _energyBar.SetValue(energy / energyMax);

            _fairyText.gameObject.SetActive(fairyActive);
            if (fairyActive)
            {
                _fairyText.text = $"{UiText.FairyTakeover} {Mathf.Max(0f, fairySeconds):0.0}s";
            }
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(visible);
            }
        }
    }
}
