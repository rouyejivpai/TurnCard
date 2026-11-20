using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 从方舟像素字体（Ark Pixel 12px）生成 TextMeshPro 字体资源。
    ///
    /// 为什么不用默认的 SDF：SDF 会把 1px 笔画模糊成灰边，像素字必须用
    /// Raster（位图）渲染模式 + Point 采样，字号也必须严格等于 12，否则字形会重采样变形。
    /// 图集用 Dynamic + 多图集，中文需要多少字烘焙多少字，不会一上来就爆 8K 图集。
    /// </summary>
    public static class PixelFontSetup
    {
        private const string TtfPath = "Assets/Game/Art/Fonts/Pixel/ark-pixel-12px-proportional-zh_hans.ttf";
        private const string OutputPath = "Assets/Game/Resources/Fonts/ArkPixel12 SDF.asset";
        private const int PointSize = 12;
        private const int AtlasSize = 1024;

        [MenuItem("Tools/TurnCard/字体/生成像素字体 TMP 资源", false, 2030)]
        public static void Build()
        {
            var importer = AssetImporter.GetAtPath(TtfPath) as TrueTypeFontImporter;
            if (importer == null)
            {
                Debug.LogError($"[PixelFont] 找不到字体文件 {TtfPath}");
                return;
            }

            // 作为旧版 Font 使用时也需要动态度量，否则中文只会烘焙 ASCII。
            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.fontRenderingMode = FontRenderingMode.HintedRaster;
            importer.fontSize = PointSize;
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(TtfPath, ImportAssetOptions.ForceUpdate);

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"[PixelFont] 已存在 {OutputPath}，直接定位。要重建请先删除它。");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (font == null)
            {
                Debug.LogError($"[PixelFont] 字体资源加载失败 {TtfPath}");
                return;
            }

            var asset = TMP_FontAsset.CreateFontAsset(
                font, PointSize, 0, GlyphRenderMode.RASTER, AtlasSize, AtlasSize,
                AtlasPopulationMode.Dynamic, true);

            if (asset == null)
            {
                Debug.LogError("[PixelFont] TMP_FontAsset.CreateFontAsset 返回空，检查 TMP 版本是否 >= 2.1");
                return;
            }

            asset.name = "ArkPixel12 SDF";
            var bitmapShader = Shader.Find("TextMeshPro/Bitmap");
            if (bitmapShader != null && asset.material != null)
            {
                asset.material.shader = bitmapShader;
            }

            if (asset.material != null)
            {
                asset.material.name = "ArkPixel12 Material";
            }

            foreach (var atlas in asset.atlasTextures)
            {
                atlas.name = "ArkPixel12 Atlas";
                atlas.filterMode = FilterMode.Point;   // 位图字体必须 Point，否则边缘发灰
                atlas.wrapMode = TextureWrapMode.Clamp;
            }

            AssetDatabase.CreateAsset(asset, OutputPath);
            if (asset.material != null)
            {
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            foreach (var atlas in asset.atlasTextures)
            {
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            Debug.Log($"[PixelFont] 已生成 {OutputPath}（{PointSize}px Raster，动态图集，Point 采样）");
        }
    }
}
