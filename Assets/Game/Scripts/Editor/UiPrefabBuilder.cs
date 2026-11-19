using System.IO;
using TurnCard.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 生成界面预制体。层级与运行时兜底完全一致，之后调整视觉都在预制体上改，不需要动代码。
    /// </summary>
    public static class UiPrefabBuilder
    {
        private const string FolderPath = "Assets/Game/Resources/UI";
        private const string CardPrefabPath = FolderPath + "/CardView.prefab";
        private const string PilePrefabPath = FolderPath + "/PilePanel.prefab";
        private const string KeywordTipPrefabPath = FolderPath + "/KeywordTip.prefab";

        [MenuItem("Tools/TurnCard/生成界面预制体", priority = 200)]
        public static void BuildAll()
        {
            EnsureFolder();
            BuildCardPrefab();
            BuildPilePrefab();
            BuildKeywordTipPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[预制体] 已生成 CardView / PilePanel / KeywordTip");
        }

        [MenuItem("Tools/TurnCard/生成关键词提示预制体", priority = 202)]
        public static void BuildKeywordTipPrefab()
        {
            EnsureFolder();

            var go = new GameObject("KeywordTip", typeof(RectTransform));
            var tip = go.AddComponent<KeywordTip>();
            tip.BuildDefault();
            ClearFonts(go);

            PrefabUtility.SaveAsPrefabAsset(go, KeywordTipPrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[预制体] 已生成 " + KeywordTipPrefabPath);
        }

        [MenuItem("Tools/TurnCard/生成卡牌预制体", priority = 201)]
        public static void BuildCardPrefab()
        {
            EnsureFolder();

            // 根节点不再挂 Image：正反两面各自带背景
            var go = new GameObject("CardView", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(CardView.CardWidth, CardView.CardHeight);

            var view = go.AddComponent<CardView>();
            view.BuildDefaultHierarchy();
            ClearFonts(go);

            PrefabUtility.SaveAsPrefabAsset(go, CardPrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[预制体] 已生成 " + CardPrefabPath);
        }

        [MenuItem("Tools/TurnCard/生成牌堆面板预制体", priority = 202)]
        public static void BuildPilePrefab()
        {
            EnsureFolder();

            var go = new GameObject("PilePanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var panel = go.AddComponent<PilePanel>();
            panel.BuildDefaultHierarchy();
            ClearFonts(go);

            PrefabUtility.SaveAsPrefabAsset(go, PilePrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[预制体] 已生成 " + PilePrefabPath);
        }

        /// <summary>
        /// 动态字体不是资产，存进预制体会变成空引用；清掉，运行时由各视图补上。
        /// 想在设计时看清文字，可以导入一个 TTF 并在预制体里手动指定。
        /// </summary>
        private static void ClearFonts(GameObject go)
        {
            foreach (TMPro.TextMeshProUGUI text in go.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                text.font = null;
            }
        }

        private static void EnsureFolder()
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
                AssetDatabase.Refresh();
            }
        }
    }
}
