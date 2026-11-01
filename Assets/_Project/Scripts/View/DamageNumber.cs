using Lingjing.UI;
using System.Collections.Generic;
using UnityEngine;

namespace Lingjing.View
{
    /// <summary>
    /// 世界层伤害数字：池化的 TextMesh，命中时在敌人头顶弹出来、上浮并淡出。
    /// 为什么不用 UI Canvas：数字要贴着世界层一起缩放、跟着像素相机走，走 Canvas 反而要每帧做
    /// WorldToScreenPoint 转换，还会和 Fairy 层的界面抢层级。
    /// </summary>
    public sealed class DamageNumber : MonoBehaviour
    {
        /// <summary>整条动画时长（秒）。用 unscaledDeltaTime，顿帧期间照样播完。</summary>
        public const float LifeSeconds = 0.55f;

        /// <summary>上浮距离（单位）。</summary>
        public const float RiseUnits = 0.9f;

        /// <summary>字号缩放：世界层 1 单位 = 32px，字高约 0.5 单位。</summary>
        public const float TextScale = 0.5f;

        static readonly Stack<DamageNumber> Pool = new Stack<DamageNumber>();
        static Transform _root;

        TextMesh _mesh;
        MeshRenderer _meshRenderer;
        Vector3 _origin;
        float _age;
        Color _color = Color.white;

        public static void Spawn(Vector3 worldPosition, double amount)
        {
            if (amount < 0.5)
            {
                return;
            }

            if (_root == null)
            {
                var rootGo = new GameObject("DamageNumbers");
                _root = rootGo.transform;
            }

            DamageNumber number = Pool.Count > 0 ? Pool.Pop() : Create();
            number.transform.SetParent(_root, false);
            number.transform.position = worldPosition + new Vector3(Random.Range(-0.2f, 0.2f), 0.45f, 0f);
            number.transform.localScale = Vector3.one * TextScale;
            number._origin = number.transform.position;
            number._age = 0f;
            number._mesh.text = Mathf.Max(1, Mathf.RoundToInt((float)amount)).ToString();
            number._color = new Color(1f, 0.87f, 0.45f);
            number._mesh.color = number._color;
            number.gameObject.SetActive(true);
            number.enabled = true;
        }

        static DamageNumber Create()
        {
            var go = new GameObject("DamageNumber");
            var number = go.AddComponent<DamageNumber>();

            number._mesh = go.AddComponent<TextMesh>();
            Font font = UiKit.Font;
            if (font != null)
            {
                number._mesh.font = font;
                number._meshRenderer = go.GetComponent<MeshRenderer>();
                if (number._meshRenderer != null)
                {
                    number._meshRenderer.sharedMaterial = font.material;
                    number._meshRenderer.sortingOrder = 30;
                }
            }

            number._mesh.fontSize = 64;
            number._mesh.characterSize = 0.1f;
            number._mesh.anchor = TextAnchor.MiddleCenter;
            number._mesh.alignment = TextAlignment.Center;
            number._mesh.text = "0";
            go.SetActive(false);
            return number;
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = _age / LifeSeconds;
            if (t >= 1f)
            {
                Despawn();
                return;
            }

            // 先快后慢地上浮，尾巴几乎停住，读得清数字。
            transform.position = _origin + new Vector3(0f, RiseUnits * Mathf.Sqrt(t), 0f);
            float alpha = 1f - Mathf.Clamp01((t - 0.5f) / 0.5f);
            _mesh.color = new Color(_color.r, _color.g, _color.b, alpha);
        }

        void Despawn()
        {
            gameObject.SetActive(false);
            enabled = false;
            Pool.Push(this);
        }
    }
}
