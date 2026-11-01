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

        static readonly Color TextColor = UiPalette.Text;
        static readonly Color FairyColor = UiPalette.Glow;
        static readonly Color BarBackground = UiPalette.BarTrack;

        Text _hpText;
        Text _energyText;
        Text _fairyText;
        Text _sectionText;
        Text _bannerText;
        GameObject _bannerObject;
        GameObject _emblemObject;
        UiBar _hpBar;
        UiBar _energyBar;

        /// <summary>HUD 自己的 Doozy 视图，常驻显示。</summary>
        public DoozyViewHost View { get; private set; }

        public static HudView Build(Action onPause)
        {
            UiKit.EnsureEventSystem();

            Canvas canvas = DoozyViewHost.EnsureCanvas();
            DoozyViewHost view = DoozyViewHost.Create(
                canvas.transform, DoozyViewHost.HudViewName, DoozyViewHost.HudSortingOrder);

            var hud = view.Root.AddComponent<HudView>();
            hud.View = view;
            hud.Init(view.Root.transform, onPause);
            view.Show(true);
            return hud;
        }

        void Init(Transform root, Action onPause)
        {
            RectTransform card = UiKit.CreatePanel(root, "Stats", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(SafeMargin, -SafeMargin), new Vector2(300f, 158f),
                UiPalette.HudBackdrop);
            card.GetComponent<Image>().raycastTarget = false;

            _hpText = UiKit.CreateLabel(card, string.Empty, 22, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(268f, 26f), TextColor);

            _hpBar = UiKit.CreateBar(card, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -42f), new Vector2(268f, 12f),
                BarBackground, UiPalette.Danger);

            _energyText = UiKit.CreateLabel(card, string.Empty, 22, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -62f), new Vector2(268f, 26f), TextColor);

            _energyBar = UiKit.CreateBar(card, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -92f), new Vector2(268f, 12f),
                BarBackground, UiPalette.Signal);

            // 代行时把她那枚徽记一起亮出来：这是全作唯一一件非像素的实体图形。
            Image emblem = UiKit.CreateImage(card, "Emblem", UiKit.Emblem,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -104f), new Vector2(30f, 30f));
            _emblemObject = emblem.gameObject;
            _emblemObject.SetActive(false);

            _fairyText = UiKit.CreateLabel(card, string.Empty, 20, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -108f), new Vector2(226f, 26f), FairyColor);
            _fairyText.gameObject.SetActive(false);

            // 暂停键贴右上，与 HUD 数值一起远离左下摇杆和右下技能键。
            UiKit.CreateButton(root, UiText.Pause, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-SafeMargin, -SafeMargin), new Vector2(96f, 46f), onPause, 22);

            // 节进度常驻顶部中央；换节时中间弹一次大提示。
            _sectionText = UiKit.CreateLabel(root, string.Empty, 22, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f),
                new Vector2(300f, 30f), UiPalette.Muted);

            var center = new Vector2(0.5f, 0.5f);
            RectTransform banner = UiKit.CreatePanel(root, "SectionBanner", center, center,
                new Vector2(0f, 60f), new Vector2(420f, 96f), UiPalette.Panel);
            banner.GetComponent<Image>().raycastTarget = false;

            _bannerText = UiKit.CreateTitle(banner, string.Empty, 48, TextAnchor.MiddleCenter, center, center,
                Vector2.zero, new Vector2(400f, 80f), UiPalette.Signal);

            _bannerObject = banner.gameObject;
            _bannerObject.SetActive(false);
        }

        /// <summary>节进度：数字常驻，换节停顿期间把中间的大提示亮出来。</summary>
        public void SetSection(int current, int total, bool intermission)
        {
            if (_sectionText != null)
            {
                _sectionText.text = UiText.SectionProgress(current, total);
            }

            if (_bannerText != null)
            {
                _bannerText.text = UiText.SectionBanner(current);
            }

            if (_bannerObject != null)
            {
                _bannerObject.SetActive(intermission);
            }
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
            if (_emblemObject != null)
            {
                _emblemObject.SetActive(fairyActive);
            }

            if (fairyActive)
            {
                _fairyText.text = $"{UiText.FairyTakeover} {Mathf.Max(0f, fairySeconds):0.0}s";
            }
        }
    }
}
