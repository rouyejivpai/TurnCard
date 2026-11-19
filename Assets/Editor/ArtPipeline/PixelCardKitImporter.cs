using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// Applies the project's pixel-art import rules to the third-party PixelCardKit and
    /// restores the 9-slice borders recorded in PixelCardKit.json.
    ///
    /// The kit lives in Assets/Game/Art/UI/PixelCardKit and is a curated subset of
    /// CC0 packs (see LICENSE.md next to it). Borders are stored in the json instead of
    /// only in the .meta files so the layout survives re-imports and fresh clones.
    /// </summary>
    public static class PixelCardKitImporter
    {
        private const string KitRoot = "Assets/Game/Art/UI/PixelCardKit";
        private const string ConfigPath = KitRoot + "/PixelCardKit.json";

        [Serializable]
        private class Config
        {
            public int pixelsPerUnit = 100;
            public int maxTextureSize = 2048;
            public List<Entry> entries = new List<Entry>();
        }

        [Serializable]
        private class Entry
        {
            public string path;
            public int[] border;
        }

        [MenuItem("Tools/TurnCard/像素卡牌/应用 PixelCardKit 导入预设", false, 2021)]
        public static void ApplyAll()
        {
            if (!File.Exists(ConfigPath))
            {
                Debug.LogError($"[PixelCardKit] 找不到配置 {ConfigPath}");
                return;
            }

            if (!AssetDatabase.IsValidFolder(KitRoot))
            {
                Debug.LogError($"[PixelCardKit] 找不到目录 {KitRoot}");
                return;
            }

            var config = JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath));
            var borders = config.entries
                .Where(e => e.border != null && e.border.Length == 4)
                .ToDictionary(e => Normalize(e.path), e => e.border, StringComparer.OrdinalIgnoreCase);

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { KitRoot });
            var applied = 0;
            var sliced = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                ApplyPixelSettings(importer, config);

                var relative = Normalize(path.Substring(KitRoot.Length).TrimStart('/'));
                if (borders.TryGetValue(relative, out var border))
                {
                    var vector = new Vector4(border[0], border[1], border[2], border[3]);
                    if (importer.spriteBorder != vector)
                    {
                        importer.spriteBorder = vector;
                        sliced++;
                    }
                }

                importer.SaveAndReimport();
                applied++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PixelCardKit] 已应用 {applied} 张图的导入设置，其中 {sliced} 张写入了九宫格边界");
        }

        [MenuItem("Tools/TurnCard/像素卡牌/选中卡框目录", false, 2022)]
        private static void PingCardsFolder()
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(KitRoot + "/Cards");
            if (folder == null)
            {
                Debug.LogWarning($"[PixelCardKit] 没有 {KitRoot}/Cards");
                return;
            }

            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        private static void ApplyPixelSettings(TextureImporter importer, Config config)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = config.pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = config.maxTextureSize;
            importer.isReadable = false;

            // FullRect keeps a 9-sliced sprite's quad stable; Tight would re-mesh per size.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/').TrimStart('/');
        }
    }
}
