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
        private const string CardFacePrefabPath = FolderPath + "/CardFace.prefab";

        /// <summary>整图模式的卡框图（160×196）。</summary>
        private const string CardBackdropPath = "Assets/Game/Art/BG/cardframeWood.png";
        private const string PilePrefabPath = FolderPath + "/PilePanel.prefab";
        private const string KeywordTipPrefabPath = FolderPath + "/KeywordTip.prefab";

        [MenuItem("Tools/TurnCard/生成界面预制体", priority = 200)]
        public static void BuildAll()
        {
            EnsureFolder();
            BuildCardFacePrefab();
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

        /// <summary>
        /// 生成卡面预制体（正反两面共用这一份结构）。
        /// 改布局只改这个文件，正反面同时生效。
        /// </summary>
        [MenuItem("Tools/TurnCard/生成卡面预制体", priority = 200)]
        public static void BuildCardFacePrefab()
        {
            EnsureFolder();

            var go = new GameObject("CardFace", typeof(RectTransform));
            var face = go.AddComponent<CardFaceView>();

            // 整图模式：直接把卡框图赋给背景（预制体里序列化，运行时不依赖 Resources）
            var backdrop = AssetDatabase.LoadAssetAtPath<Sprite>(CardBackdropPath);
            if (backdrop == null)
            {
                Debug.LogWarning("[预制体] 没找到卡框图：" + CardBackdropPath + "，卡面会是空白背景");
            }

            face.BuildDefault(backdrop);
            ClearFonts(go);

            PrefabUtility.SaveAsPrefabAsset(go, CardFacePrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            Debug.Log("[预制体] 已生成 " + CardFacePrefabPath);
        }

        [MenuItem("Tools/TurnCard/生成卡牌预制体", priority = 201)]
        public static void BuildCardPrefab()
        {
            EnsureFolder();

            // 卡面必须先存在，CardView 里放的是它的两个嵌套实例
            var facePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardFacePrefabPath);
            if (facePrefab == null)
            {
                BuildCardFacePrefab();
                facePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardFacePrefabPath);
            }

            if (facePrefab == null)
            {
                Debug.LogError("[预制体] 卡面预制体生成失败，CardView 未生成");
                return;
            }

            // 根节点不挂 Image：正反两面各自带背景
            var go = new GameObject("CardView", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(CardView.CardWidth, CardView.CardHeight);

            var view = go.AddComponent<CardView>();

            GameObject front = CreateFaceInstance(facePrefab, go.transform, "FrontFace", false);
            GameObject back = CreateFaceInstance(facePrefab, go.transform, "BackFace", true);

            // 把两个实例的视图挂到 CardView 上（部件引用由视图提供）
            var so = new SerializedObject(view);
            so.FindProperty("m_FrontView").objectReferenceValue = front.GetComponent<CardFaceView>();
            so.FindProperty("m_BackView").objectReferenceValue = back.GetComponent<CardFaceView>();
            so.ApplyModifiedPropertiesWithoutUndo();

            ClearFonts(go);

            PrefabUtility.SaveAsPrefabAsset(go, CardPrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[预制体] 已生成 " + CardPrefabPath);
        }

        private static GameObject CreateFaceInstance(GameObject facePrefab, Transform parent,
            string name, bool isBack)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(facePrefab, parent);
            instance.name = name;

            var rect = (RectTransform)instance.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            // 背面预先翻转 180°，整张卡转到 180° 时正好正对镜头
            rect.localEulerAngles = isBack ? new Vector3(0f, 180f, 0f) : Vector3.zero;
            return instance;
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
