using UnityEditor;
using UnityEngine;

namespace Lingjing.Editor
{
    /// <summary>
    /// 把 docs/specs/art-assets.md §6 的导入设置写死在导入时：PPU 32、Point、不压缩、
    /// 无 mipmap、Clamp、Alpha Is Transparency。锚点按 §2 / 各 manifest 的 pivotPx 折算。
    /// 改这里等于改规格，两边要一起改。
    /// </summary>
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        const string ArtRoot = "Assets/_Project/Art/";

       /// <summary>folder → 归一化锚点。顺序即优先级，先匹配更长的前缀。</summary>
        static readonly (string folder, Vector2 pivot)[] Pivots =
        {
            ("Characters/eous/", new Vector2(0.5f, 3f / 64f)),
            ("Enemies/dropper/", new Vector2(0.5f, 5f / 96f)),
            ("Enemies/", new Vector2(0.5f, 5f / 64f)),
            ("Bosses/", new Vector2(0.5f, 8f / 96f)),
            ("Items/knife/", new Vector2(0.5f, 0.5f)),
            ("Items/", new Vector2(0.5f, 6f / 32f)),
            ("Terrain/props/", new Vector2(0.5f, 0f)),
            ("Terrain/portal/", new Vector2(0.5f, 0f)),
            ("Fx/", new Vector2(0.5f, 0.5f)),
        };

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            if (!assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = PivotFor(assetPath);
            importer.SetTextureSettings(settings);
        }

        /// <summary>按目录取锚点；缩略图与审阅图不在此目录下，不受影响。</summary>
        internal static Vector2 PivotFor(string path)
        {
            var rel = path.Substring(ArtRoot.Length);
            foreach (var (folder, pivot) in Pivots)
                if (rel.StartsWith(folder, System.StringComparison.Ordinal))
                    return pivot;
            return new Vector2(0.5f, 0.5f);
        }
    }
}
