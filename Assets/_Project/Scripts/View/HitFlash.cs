using UnityEngine;

namespace Lingjing.View
{
    /// <summary>
    /// 受击闪白的共用材质与写入方式。
    /// 用 MaterialPropertyBlock 而不是给每个精灵换 material：一次 draw call 的合批不破，
    /// 也不用为每个以骸实例化材质。shader 在 Resources 下，运行时打包不会把它裁掉。
    /// </summary>
    public static class HitFlash
    {
        public const string ShaderPath = "Shaders/SpriteFlash";

        static Material s_material;
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly MaterialPropertyBlock s_block = new MaterialPropertyBlock();

        /// <summary>全工程共用的闪白材质；shader 丢了就退回默认精灵材质，画面上不闪但不会崩。</summary>
        public static Material Material
        {
            get
            {
                if (s_material != null)
                {
                    return s_material;
                }

                Shader shader = Resources.Load<Shader>(ShaderPath);
                if (shader == null)
                {
                    Debug.LogWarning("[fx] 找不到 Resources/" + ShaderPath + "，受击闪白关闭");
                    return null;
                }

                s_material = new Material(shader) { name = "SpriteFlash" };
                return s_material;
            }
        }

        /// <summary>0 = 原色，1 = 全闪。以骸闪白、伊埃斯受伤闪红走同一套。</summary>
        public static void Apply(SpriteRenderer renderer, float amount, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(s_block);
            s_block.SetFloat(FlashId, Mathf.Clamp01(amount));
            s_block.SetColor(FlashColorId, color);
            renderer.SetPropertyBlock(s_block);
        }
    }
}
