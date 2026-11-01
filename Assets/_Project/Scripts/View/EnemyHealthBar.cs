using UnityEngine;

namespace Lingjing.View
{
    /// <summary>
    /// 以骸血条：世界层的 32×5 像素条，贴在头顶。
    /// 满血不显示；受过伤后显示，停手 <see cref="HoldSeconds"/> 秒淡出——45 只同屏时全部常显会盖过角色。
    /// 填充按整数像素裁、用 1×3 图块平铺，不做插值（像素风里半格宽的条比掉一格更难看）。
    /// 每帧由 BattleRunner 用同一次循环推进，不挂 MonoBehaviour；
    /// 尺寸、颜色、可见性都只在真的变了才写——平铺精灵每改一次 size 都会重建网格。
    /// </summary>
    public sealed class EnemyHealthBar
    {
        const float PixelsPerUnit = 32f;
        const int InnerPixels = 30;
        const float HoldSeconds = 2.5f;
        const float FadeSeconds = 0.35f;
        const float BarAlpha = 0.95f;

        readonly Transform _tx;
        readonly SpriteRenderer _track;
        readonly SpriteRenderer _fill;
        readonly float _offsetY;

        float _ratio = 1f;
        float _shownUntil;
        float _alpha;
        float _appliedWidth = -1f;
        float _appliedAlpha = -1f;
        bool _renderersOn = true;

        public EnemyHealthBar(Transform parent, Sprite trackSprite, Sprite fillSprite, float offsetY)
        {
            _offsetY = offsetY;

            var go = new GameObject("HpBar");
            _tx = go.transform;
            _tx.SetParent(parent, false);

            _track = go.AddComponent<SpriteRenderer>();
            _track.sprite = trackSprite;
            _track.sortingOrder = 9;

            var fillGo = new GameObject("Fill");
            _fill = fillGo.AddComponent<SpriteRenderer>();
            _fill.transform.SetParent(_tx, false);
            _fill.sprite = fillSprite;
            _fill.sortingOrder = 10;
            // 1×3 的图块按整数像素平铺，宽度 = size.x。
            _fill.drawMode = SpriteDrawMode.Tiled;
            _fill.tileMode = SpriteTileMode.Continuous;

            Apply(0f);
        }

        /// <summary>受伤时把显示窗口往后推；满血由 <see cref="Sync"/> 直接判掉。</summary>
        public void MarkDamaged()
        {
            _shownUntil = Time.time + HoldSeconds;
        }

        /// <summary>敌人离场时销毁自己的两个渲染对象。</summary>
        public void Destroy()
        {
            if (_tx != null)
            {
                Object.Destroy(_tx.gameObject);
            }
        }

        public void Sync(double hp, double maxHp, Vector3 worldPosition, float dt)
        {
            _ratio = maxHp > 0.0 ? Mathf.Clamp01((float)(hp / maxHp)) : 0f;

            bool wantsVisible = _ratio < 0.999f && Time.time < _shownUntil;
            float target = wantsVisible ? BarAlpha : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, dt / FadeSeconds);

            Apply(_alpha);
            _tx.position = new Vector3(worldPosition.x, worldPosition.y + _offsetY, 0f);
        }

        void Apply(float alpha)
        {
            bool visible = alpha > 0.001f;
            if (_renderersOn != visible)
            {
                _renderersOn = visible;
                _track.enabled = visible;
                _fill.enabled = visible;
            }

            if (!visible)
            {
                _appliedWidth = -1f;
                _appliedAlpha = -1f;
                return;
            }

            if (!Mathf.Approximately(alpha, _appliedAlpha))
            {
                _appliedAlpha = alpha;
                var color = new Color(1f, 1f, 1f, alpha);
                _track.color = color;
                _fill.color = color;
            }

            float width = Mathf.Round(_ratio * InnerPixels) / PixelsPerUnit;
            if (_ratio > 0.001f && width < 1f / PixelsPerUnit)
            {
                width = 1f / PixelsPerUnit;
            }

            if (Mathf.Approximately(width, _appliedWidth))
            {
                return;
            }

            _appliedWidth = width;
            _fill.size = new Vector2(width, 3f / PixelsPerUnit);
            // 填充是 1×3 的图块：左端钉死在轨道内侧，从右往左掉。
            _fill.transform.localPosition = new Vector3(-InnerPixels / (2f * PixelsPerUnit) + width * 0.5f, 0f, 0f);
        }
    }
}
