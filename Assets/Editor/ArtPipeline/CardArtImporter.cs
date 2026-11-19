using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// Applies pixel-art import settings to the card art exported from Aseprite and
    /// generates one looping AnimationClip per animated sheet.
    ///
    /// The list of sheets/frames comes from CardArtManifest.json, which is written by
    /// Tools/ArtPipeline/export_cards.py (the Aseprite side of the pipeline).
    /// </summary>
    public static class CardArtImporter
    {
        private const string CardsRoot = "Assets/Game/Art/Cards";
        private const string ManifestPath = CardsRoot + "/CardArtManifest.json";
        private const float PixelsPerUnit = 100f;
        private const int MaxTextureSize = 2048;

        [Serializable]
        private class Manifest
        {
            public string generatedAt;
            public int scale;
            public List<ArtAsset> assets;
        }

        [Serializable]
        private class ArtAsset
        {
            public string name;
            public string entity;
            public string source;
            public string sheet;
            public string staticImage;
            public int frameCount;
            public int frameWidth;
            public int frameHeight;
            public List<FrameRect> frames;
        }

        [Serializable]
        private class FrameRect
        {
            public int index;
            public int x;
            public int y;
            public int w;
            public int h;
            public float duration;
        }

        [MenuItem("Tools/TurnCard/卡牌美术/应用导入设置并生成动画", false, 2010)]
        public static void ApplyAll()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"[CardArt] 找不到清单 {ManifestPath}，请先运行 Tools/ArtPipeline/export_cards.py");
                return;
            }

            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest?.assets == null || manifest.assets.Count == 0)
            {
                Debug.LogError("[CardArt] 清单为空");
                return;
            }

            var applied = 0;
            var clips = 0;
            var missing = new List<string>();

            foreach (var asset in manifest.assets)
            {
                if (!ConfigureStatic(asset))
                {
                    missing.Add(asset.staticImage);
                }

                if (!ConfigureSheet(asset))
                {
                    missing.Add(asset.sheet);
                    continue;
                }

                applied++;
                if (asset.frameCount > 1 && CreateLoopClip(asset))
                {
                    clips++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var summary = $"[CardArt] 已设置 {applied} 张图集，生成 {clips} 个循环动画";
            if (missing.Count > 0)
            {
                summary += $"，缺少 {missing.Count} 个文件：{string.Join(", ", missing.Take(5))}";
                Debug.LogWarning(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }

        [MenuItem("Tools/TurnCard/卡牌美术/选中卡牌图并定位", false, 2011)]
        private static void PingFirstSheet()
        {
            var sheet = AssetDatabase.FindAssets("t:Texture2D", new[] { CardsRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith("_sheet.png", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(sheet))
            {
                Debug.LogWarning($"[CardArt] {CardsRoot} 下没有 *_sheet.png");
                return;
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(sheet);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private static bool ConfigureStatic(ArtAsset asset)
        {
            if (!File.Exists(asset.staticImage))
            {
                return false;
            }

            var importer = AssetImporter.GetAtPath(asset.staticImage) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            ApplyPixelArtSettings(importer, SpriteImportMode.Single);
            importer.SaveAndReimport();
            return true;
        }

        private static bool ConfigureSheet(ArtAsset asset)
        {
            if (!File.Exists(asset.sheet) || asset.frames == null || asset.frames.Count == 0)
            {
                return false;
            }

            var importer = AssetImporter.GetAtPath(asset.sheet) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            ApplyPixelArtSettings(importer, SpriteImportMode.Multiple);

            importer.spritesheet = asset.frames
                .OrderBy(f => f.index)
                .Select(f => new SpriteMetaData
                {
                    name = $"{asset.name}_{f.index}",
                    rect = new Rect(f.x, f.y, f.w, f.h),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                })
                .ToArray();

            importer.SaveAndReimport();
            return true;
        }

        private static void ApplyPixelArtSettings(TextureImporter importer, SpriteImportMode mode)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = MaxTextureSize;
            importer.isReadable = false;

            // Pixel art must not be re-meshed per frame; FullRect keeps the quad stable.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }

        private static bool CreateLoopClip(ArtAsset asset)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(asset.sheet)
                .OfType<Sprite>()
                .ToDictionary(s => s.name, s => s);

            var keys = new List<ObjectReferenceKeyframe>();
            var time = 0f;
            Sprite firstSprite = null;

            foreach (var frame in asset.frames.OrderBy(f => f.index))
            {
                if (!sprites.TryGetValue($"{asset.name}_{frame.index}", out var sprite))
                {
                    Debug.LogWarning($"[CardArt] {asset.name} 缺少切片 {asset.name}_{frame.index}");
                    return false;
                }

                if (firstSprite == null)
                {
                    firstSprite = sprite;
                }

                keys.Add(new ObjectReferenceKeyframe { time = time, value = sprite });
                time += frame.duration > 0f ? frame.duration : 0.2f;
            }

            // Closing key on frame 0 at the end of the cycle: without it the last frame would only
            // flash for one frameRate tick before the clip wraps, instead of holding its duration.
            keys.Add(new ObjectReferenceKeyframe { time = time, value = firstSprite });

            var clip = new AnimationClip { name = asset.name + "_Loop", frameRate = 30f };
            var array = keys.ToArray();

            // Same curve name works for SpriteRenderer and for UGUI Image, so one clip serves both.
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), array);
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(Image), "m_Sprite"), array);

            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            clipSettings.startTime = 0f;
            clipSettings.stopTime = time; // exact cycle length, so the loop doesn't pad a frame tick
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            var folder = Path.GetDirectoryName(asset.sheet)?.Replace('\\', '/');
            var clipPath = $"{folder}/{asset.name}_Loop.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            if (existing != null)
            {
                // Keep the asset guid so anything already referencing the clip stays connected.
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            return true;
        }
    }
}
