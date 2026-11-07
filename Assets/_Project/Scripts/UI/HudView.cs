using System;
using Lingjing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.UI
{
    /// <summary>
    /// 战斗内 HUD：只读战斗数据，暂停键只向外发回调，不直接引用 BattleRunner。
    /// 左上内容贴 40 安全边距，右上放暂停；左下摇杆与右下按钮的位置全部留空。
    /// 文本只在数值真的变了才重建——每帧拼字符串会让 UGUI 每帧重建画布。
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
        Text _subtitleText;
        GameObject _subtitleObject;
        GameObject _bannerObject;
        GameObject _emblemObject;
        UiBar _hpBar;
        UiBar _energyBar;

        RectTransform _statsCard;

        // 受伤反馈：全屏红脉冲 + 面板抖动
        Image _hurtOverlay;
        float _hurtUntil;
        float _hurtShakeUntil;
        Vector2 _lastStatsOffset;

        // 恶名以骸的血条（Fairy 层，顶部）
        GameObject _bossPanel;
        Text _bossName;
        Text _bossValue;
        UiBar _bossBar;

        // 文本节流用的上一帧值
        int _lastHp = int.MinValue;
        int _lastMaxHp = int.MinValue;
        int _lastEnergy = int.MinValue;
        int _lastEnergyMax = int.MinValue;
        int _lastFairyTenths = int.MinValue;
        bool _lastFairyActive;
        int _lastSection = int.MinValue;
        bool _lastIntermission;

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

            // 设备上的刘海/圆角/挖孔：把 HUD 内容收进安全区。桌面整屏时锚点回到 0..1，等于没改。
            view.Root.gameObject.AddComponent<UiSafeArea>();

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
            _statsCard = card;

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

            BuildBossPanel(root);

            var center = new Vector2(0.5f, 0.5f);
            RectTransform banner = UiKit.CreatePanel(root, "SectionBanner", center, center,
                new Vector2(0f, 60f), new Vector2(420f, 96f), UiPalette.Panel);
            banner.GetComponent<Image>().raycastTarget = false;

            _bannerText = UiKit.CreateTitle(banner, string.Empty, 48, TextAnchor.MiddleCenter, center, center,
                Vector2.zero, new Vector2(400f, 80f), UiPalette.Signal);

            _bannerObject = banner.gameObject;
            _bannerObject.SetActive(false);

            // 字幕贴底居中：左右下角分别留给摇杆与技能键。
            // 抬到 y=140 是为了避开技能键上沿（技能键占 32..128），不然字幕会压住按钮。
            RectTransform subtitle = UiKit.CreatePanel(root, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 140f), new Vector2(760f, 62f), UiPalette.Panel);
            subtitle.GetComponent<Image>().raycastTarget = false;

            _subtitleText = UiKit.CreateLabel(subtitle, string.Empty, 24, TextAnchor.MiddleCenter, center, center,
                Vector2.zero, new Vector2(724f, 52f), UiPalette.Text);

            _subtitleObject = subtitle.gameObject;
            _subtitleObject.SetActive(false);

            // 受伤红脉冲放最后建，保证盖在所有 HUD 元素之上；不拦截触控。
            _hurtOverlay = UiKit.CreateImage(root, "HurtOverlay", UiKit.White, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero).GetComponent<Image>();
            RectTransform overlayRect = (RectTransform)_hurtOverlay.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            _hurtOverlay.color = new Color(1f, 0.18f, 0.22f, 0f);
            _hurtOverlay.raycastTarget = false;
        }

        void BuildBossPanel(Transform root)
        {
            RectTransform panel = UiKit.CreatePanel(root, "BossBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -52f), new Vector2(560f, 46f), UiPalette.HudBackdrop);
            panel.GetComponent<Image>().raycastTarget = false;
            _bossPanel = panel.gameObject;

            _bossName = UiKit.CreateLabel(panel, string.Empty, 20, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -6f), new Vector2(360f, 24f),
                UiPalette.Danger);

            _bossValue = UiKit.CreateLabel(panel, string.Empty, 18, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -7f), new Vector2(170f, 24f),
                UiPalette.Muted);

            _bossBar = UiKit.CreateBar(panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -32f), new Vector2(532f, 8f), BarBackground, UiPalette.Danger);

            _bossPanel.SetActive(false);
        }

        /// <summary>受伤：红脉冲 + 数值面板抖一下。只在真挨打时调。</summary>
        public void PulseHurt()
        {
            _hurtUntil = Time.unscaledTime + 0.28f;
            _hurtShakeUntil = Time.unscaledTime + 0.18f;

            if (_hurtOverlay != null)
            {
                _hurtOverlay.color = new Color(1f, 0.18f, 0.22f, 0.28f);
            }
        }

        void Update()
        {
            AnimateHurt();
        }

        void AnimateHurt()
        {
            if (_hurtOverlay == null)
            {
                return;
            }

            float left = _hurtUntil - Time.unscaledTime;
            if (left <= 0f && _hurtOverlay.color.a > 0f)
            {
                _hurtOverlay.color = new Color(1f, 0.18f, 0.22f, 0f);
            }
            else if (left > 0f)
            {
                Color c = _hurtOverlay.color;
                c.a = Mathf.Clamp01(left / 0.28f) * 0.28f;
                _hurtOverlay.color = c;
            }

            if (_statsCard != null)
            {
                float shake = _hurtShakeUntil - Time.unscaledTime;
                Vector2 offset = Vector2.zero;
                if (shake > 0f)
                {
                    int amp = Mathf.Max(1, Mathf.RoundToInt(6f * (shake / 0.18f)));
                    offset = new Vector2(
                        UnityEngine.Random.Range(-amp, amp + 1),
                        UnityEngine.Random.Range(-amp, amp + 1));
                }

                // 只在偏移真的变了才写：每帧回写同一个 anchoredPosition 会白脏一次 RectTransform。
                if (offset != _lastStatsOffset)
                {
                    _lastStatsOffset = offset;
                    _statsCard.anchoredPosition = new Vector2(SafeMargin, -SafeMargin) + offset;
                }
            }
        }

        /// <summary>字幕：Fairy 说话时才有内容。传 null/空串即收起。</summary>
        public void SetSubtitle(string text)
        {
            bool show = !string.IsNullOrEmpty(text);

            if (_subtitleText != null)
            {
                _subtitleText.text = show ? text : string.Empty;
            }

            if (_subtitleObject != null && _subtitleObject.activeSelf != show)
            {
                _subtitleObject.SetActive(show);
            }
        }

        /// <summary>节进度：数字常驻，换节停顿期间把中间的大提示亮出来。</summary>
        public void SetSection(int current, int total, bool intermission)
        {
            if (_lastSection != current)
            {
                _lastSection = current;
                _sectionText.text = UiText.SectionProgress(current, total);
                _bannerText.text = UiText.SectionBanner(current);
            }

            if (_lastIntermission != intermission && _bannerObject != null)
            {
                _lastIntermission = intermission;
                _bannerObject.SetActive(intermission);
            }
        }

        /// <summary>恶名以骸血条。传 null 名字即隐藏。</summary>
        public void SetBoss(string name, float hp, float maxHp)
        {
            if (_bossPanel == null)
            {
                return;
            }

            bool show = !string.IsNullOrEmpty(name);
            if (_bossPanel.activeSelf != show)
            {
                _bossPanel.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            _bossName.text = name;
            _bossValue.text = $"{Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(maxHp)}";
            _bossBar.SetValue(maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f);
        }

        /// <summary>只做显示；数值与倒计时由 BattleRunner 每帧灌进来。</summary>
        public void SetStats(float hp, float maxHp, float energy, float energyMax, bool fairyActive, float fairySeconds)
        {
            maxHp = Mathf.Max(1f, maxHp);
            energyMax = Mathf.Max(1f, energyMax);

            int hpValue = Mathf.CeilToInt(hp);
            int maxHpValue = Mathf.CeilToInt(maxHp);
            if (hpValue != _lastHp || maxHpValue != _lastMaxHp)
            {
                _lastHp = hpValue;
                _lastMaxHp = maxHpValue;
                _hpText.text = $"{UiText.Hp} {hpValue}/{maxHpValue}";
                // 低血把数值本身变红：这是唯一一直可见的低血提示，不靠额外特效。
                _hpText.color = hpValue * 100 <= maxHpValue * 35 ? UiPalette.Danger : TextColor;
            }

            _hpBar.SetValue(hp / maxHp);

            int energyValue = Mathf.FloorToInt(energy);
            int energyMaxValue = Mathf.CeilToInt(energyMax);
            if (energyValue != _lastEnergy || energyMaxValue != _lastEnergyMax)
            {
                _lastEnergy = energyValue;
                _lastEnergyMax = energyMaxValue;
                _energyText.text = $"{UiText.Energy} {energyValue}/{energyMaxValue}";
            }

            _energyBar.SetValue(energy / energyMax);

            if (fairyActive != _lastFairyActive)
            {
                _lastFairyActive = fairyActive;
                _fairyText.gameObject.SetActive(fairyActive);
                if (_emblemObject != null)
                {
                    _emblemObject.SetActive(fairyActive);
                }
            }

            if (fairyActive)
            {
                int tenths = Mathf.Max(0, Mathf.CeilToInt(fairySeconds * 10f));
                if (tenths != _lastFairyTenths)
                {
                    _lastFairyTenths = tenths;
                    _fairyText.text = $"{UiText.FairyTakeover} {tenths / 10f:0.0}s";
                }
            }
        }
    }
}
