using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 把 UI 里的 TMP 字体从 SimHei SDF 批量换成 ArkPixel12 SDF。
    ///
    /// 只换 font 引用是不够的：TMP_Text 还单独序列化了 m_sharedMaterial，
    /// 而材质是字体资源的子资源。换字体不换材质，字会用 SDF 着色器去采样位图图集，
    /// 结果是发虚甚至全灰。所以这里同时处理材质，并把自定义材质（描边/阴影 preset）
    /// 单独列出来交给人工确认。
    /// </summary>
    public static class TmpFontSwitcher
    {
        private const string OldFontPath = "Assets/Game/Resources/Fonts/SimHei SDF.asset";
        private const string NewFontPath = "Assets/Game/Resources/Fonts/ArkPixel12 SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private static TMP_FontAsset OldFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OldFontPath);
        private static TMP_FontAsset NewFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NewFontPath);

        private struct Hit
        {
            public string AssetPath;
            public string ObjectPath;
            public string FontSize;
            public bool CustomMaterial;
        }

        // ---------------------------------------------------------------- 扫描

        [MenuItem("Tools/TurnCard/字体/批量切换 TMP 字体/① 扫描引用", false, 2050)]
        public static void Scan()
        {
            var oldFont = OldFont;
            var newFont = NewFont;
            if (oldFont == null || newFont == null)
            {
                Debug.LogError($"[TmpFont] 字体资源缺失：old={OldFontPath} new={NewFontPath}");
                return;
            }

            var hits = new List<Hit>();
            foreach (var path in AllPrefabs())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.font != oldFont) continue;
                    hits.Add(new Hit
                    {
                        AssetPath = path,
                        ObjectPath = PathOf(text.transform, prefab.transform),
                        FontSize = text.fontSize.ToString("0.#"),
                        CustomMaterial = text.fontSharedMaterial != null && text.fontSharedMaterial != oldFont.material,
                    });
                }
            }

            var scenes = AllFilesWithGuid("t:Scene", AssetDatabase.AssetPathToGUID(OldFontPath));

            Debug.Log($"[TmpFont] Prefab 中引用旧字体的 TMP_Text：{hits.Count} 个，分布在 {hits.Select(h => h.AssetPath).Distinct().Count()} 个 prefab");
            foreach (var group in hits.GroupBy(h => h.AssetPath))
            {
                Debug.Log($"  {group.Key}  ×{group.Count()}");
                foreach (var h in group)
                {
                    var note = h.CustomMaterial ? "  [自定义材质，需人工处理]" : "";
                    Debug.Log($"      {h.ObjectPath}   size={h.FontSize}{note}");
                }
            }

            var odd = hits.Where(h => !string.IsNullOrEmpty(h.FontSize) &&
                                      Mathf.Abs(float.Parse(h.FontSize) % 12f) > 0.01f).ToList();
            if (odd.Count > 0)
            {
                Debug.LogWarning($"[TmpFont] {odd.Count} 处字号不是 12 的整数倍，换成像素字体后会重采样发虚：" +
                                 string.Join(", ", odd.Take(8).Select(h => $"{h.ObjectPath}({h.FontSize})")));
            }

            Debug.Log($"[TmpFont] 引用旧字体的场景文件：{scenes.Count}（场景里没有显式引用，说明用的是 TMP 默认字体）");
            var defaultFont = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TmpSettingsPath) != null
                ? TMP_Settings.defaultFontAsset
                : null;
            Debug.Log($"[TmpFont] TMP 默认字体：{(defaultFont != null ? defaultFont.name : "未设置")}");
        }

        // ---------------------------------------------------------------- 应用

        [MenuItem("Tools/TurnCard/字体/批量切换 TMP 字体/② 应用到 Prefab", false, 2051)]
        public static void ApplyToPrefabs()
        {
            var oldFont = OldFont;
            var newFont = NewFont;
            if (oldFont == null || newFont == null)
            {
                Debug.LogError("[TmpFont] 字体资源缺失，先跑 ① 扫描看提示");
                return;
            }

            var changedAssets = 0;
            var changedTexts = 0;
            foreach (var path in AllPrefabs())
            {
                GameObject contents = null;
                var local = 0;

                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    foreach (var text in contents.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (text.font != oldFont) continue;

                        var hadCustom = text.fontSharedMaterial != null &&
                                        text.fontSharedMaterial != oldFont.material;
                        text.font = newFont;
                        // 自定义材质（描边等）保留，但要提醒它现在配的是像素位图字体
                        if (!hadCustom)
                        {
                            text.fontSharedMaterial = newFont.material;
                        }
                        text.SetAllDirty();
                        local++;
                    }

                    if (local > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        changedAssets++;
                        changedTexts += local;
                        Debug.Log($"[TmpFont] {path}  替换 {local} 处");
                    }
                }
                finally
                {
                    if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TmpFont] 完成：{changedAssets} 个 prefab，共 {changedTexts} 处文本换成 {newFont.name}");
        }

        // ---------------------------------------------------------------- 默认字体

        [MenuItem("Tools/TurnCard/字体/批量切换 TMP 字体/③ 设为 TMP 默认字体", false, 2052)]
        public static void SetAsDefault()
        {
            var newFont = NewFont;
            if (newFont == null)
            {
                Debug.LogError("[TmpFont] 找不到新字体资源");
                return;
            }

            if (!EditorUtility.DisplayDialog("切换 TMP 默认字体",
                    $"把 TMP Settings 的默认字体改成 {newFont.name}？\n之后新建的文本都会用它。", "改", "取消"))
            {
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogError($"[TmpFont] 找不到 {TmpSettingsPath}");
                return;
            }

            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null)
            {
                Debug.LogError("[TmpFont] TMP Settings 里没有 m_defaultFontAsset 字段");
                return;
            }

            prop.objectReferenceValue = newFont;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TmpFont] TMP 默认字体已设为 {newFont.name}");
        }

        // ---------------------------------------------------------------- 回滚

        [MenuItem("Tools/TurnCard/字体/批量切换 TMP 字体/④ 回滚到 SimHei SDF", false, 2053)]
        public static void Revert()
        {
            var oldFont = OldFont;
            var newFont = NewFont;
            if (oldFont == null || newFont == null) return;

            if (!EditorUtility.DisplayDialog("回滚字体",
                    "把所有用 ArkPixel12 的 prefab 文本改回 SimHei SDF？", "回滚", "取消"))
            {
                return;
            }

            var changed = 0;
            foreach (var path in AllPrefabs())
            {
                GameObject contents = null;
                var local = 0;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    foreach (var text in contents.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (text.font != newFont) continue;
                        text.font = oldFont;
                        text.fontSharedMaterial = oldFont.material;
                        text.SetAllDirty();
                        local++;
                    }
                    if (local > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        changed += local;
                    }
                }
                finally
                {
                    if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[TmpFont] 已回滚 {changed} 处文本");
        }

        // ---------------------------------------------------------------- 工具

        private static IEnumerable<string> AllPrefabs()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/") && !p.Contains("/Packages/"));
        }

        private static List<string> AllFilesWithGuid(string filter, string guid)
        {
            var result = new List<string>();
            foreach (var g in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (!File.Exists(path)) continue;
                if (File.ReadAllText(path).Contains(guid)) result.Add(path);
            }
            return result;
        }

        private static string PathOf(Transform t, Transform root)
        {
            var stack = new List<string>();
            while (t != null && t != root)
            {
                stack.Add(t.name);
                t = t.parent;
            }
            stack.Reverse();
            return string.Join("/", stack);
        }
    }
}
