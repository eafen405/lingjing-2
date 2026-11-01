using DG.Tweening;
using UnityEngine;

namespace Lingjing.UI
{
    /// <summary>
    /// 表现层的一次性 DOTween 动效：拾取、通关、界面淡入。
    /// 放在 UI 程序集是因为 UI 与 View 都要用，而 View 已经依赖 UI（反向引用会成环）。
    /// 补间不碰模拟、不改世界状态；统一用未缩放时间，顿帧或暂停（timeScale = 0）时照样播完。
    /// </summary>
    public static class TweenFx
    {
        /// <summary>拾取时在原地弹一个放大淡出的图标，然后自毁。</summary>
        public static void PopSprite(Sprite sprite, Vector3 position, int sortingOrder, float seconds = 0.3f)
        {
            if (sprite == null)
            {
                return;
            }

            var go = new GameObject("Pop");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = Color.white;
            go.transform.localScale = Vector3.one * 0.7f;

            go.transform.DOScale(Vector3.one * 1.4f, seconds).SetEase(Ease.OutQuad).SetUpdate(true);
            DOTween.To(() => sr.color, c => sr.color = c, new Color(1f, 1f, 1f, 0f), seconds)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() => Object.Destroy(go));
        }

        /// <summary>轻轻鼓一下：拾取、升级、按键反馈用。</summary>
        public static void PunchScale(Transform target, float strength = 0.12f, float seconds = 0.18f)
        {
            if (target == null)
            {
                return;
            }

            target.DOKill();
            Vector3 baseScale = target.localScale == Vector3.zero ? Vector3.one : target.localScale;
            target.localScale = baseScale;
            target.DOScale(baseScale * (1f + strength), seconds * 0.4f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => target.DOScale(baseScale, seconds * 0.6f).SetEase(Ease.OutBack).SetUpdate(true));
        }

        /// <summary>界面淡入：DoozyUI 负责显隐与层级，DOTween 只补一层透明度过渡。</summary>
        public static void FadeIn(CanvasGroup group, float seconds = 0.18f)
        {
            if (group == null)
            {
                return;
            }

            group.DOKill();
            group.alpha = 0f;
            // 不用 CanvasGroup.DOFade：它在 DOTween 的 UI 模块里，本工程只引用了 DOTween 核心程序集。
            DOTween.To(() => group.alpha, a => group.alpha = a, 1f, seconds).SetUpdate(true).SetEase(Ease.OutQuad);
        }
    }
}
