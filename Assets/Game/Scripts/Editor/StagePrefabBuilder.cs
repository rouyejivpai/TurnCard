using TMPro;
using TurnCard.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 生成舞台区的两个占位预制体：<c>UI/Stage/Chest</c> 与 <c>UI/Stage/RouteOption</c>。
    /// 美术出图后只需要替换里面的 Sprite，不用改结构。
    /// </summary>
    public static class StagePrefabBuilder
    {
        private const string StageDir = "Assets/Game/Resources/UI/Stage";

        [MenuItem("Tools/TurnCard/生成舞台预制体")]
        public static void Build()
        {
            EnsureFolder();
            BuildChest();
            BuildRouteOption();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[舞台] 预制体生成完成：" + StageDir);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Game/Resources", "UI");
            }

            if (!AssetDatabase.IsValidFolder(StageDir))
            {
                AssetDatabase.CreateFolder("Assets/Game/Resources/UI", "Stage");
            }
        }

        private static void BuildChest()
        {
            // 根只做容器 + 按钮，两张图各占一个子节点，美术替换时不容易搞错
            var root = new GameObject("Chest", typeof(RectTransform), typeof(CanvasGroup),
                typeof(Button), typeof(ChestView));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(220f, 220f);

            RectTransform closed = UiFactory.CreateRect(rect, "imgClosed");
            Stretch(closed, 8f);
            var closedImage = closed.gameObject.AddComponent<Image>();
            closedImage.color = new Color(0.42f, 0.30f, 0.16f);   // 占位：闭箱
            // 关闭态这张图同时是点击热区：Button 需要一张可命中的 Graphic
            closedImage.raycastTarget = true;

            RectTransform open = UiFactory.CreateRect(rect, "imgOpen");
            Stretch(open, 8f);
            var openImage = open.gameObject.AddComponent<Image>();
            openImage.color = new Color(0.86f, 0.68f, 0.28f);     // 占位：开箱
            openImage.raycastTarget = false;
            open.gameObject.SetActive(false);

            TextMeshProUGUI label = UiFactory.CreateLabel(rect, "txtHint", "点击打开", 34f,
                new Color(1f, 0.96f, 0.86f), new Vector2(260f, 60f),
                new Vector2(0.5f, 0f), new Vector2(0f, -46f));
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            var button = root.GetComponent<Button>();
            button.targetGraphic = closedImage;

            var view = root.GetComponent<ChestView>();
            var so = new SerializedObject(view);
            so.FindProperty("m_ClosedArt").objectReferenceValue = closedImage;
            so.FindProperty("m_OpenArt").objectReferenceValue = openImage;
            so.FindProperty("m_Button").objectReferenceValue = button;
            so.FindProperty("m_Hint").objectReferenceValue = label.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, StageDir + "/Chest.prefab");
            Object.DestroyImmediate(root);
        }

        private static void BuildRouteOption()
        {
            var root = new GameObject("RouteOption", typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(RouteOptionView));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(340f, 110f);

            var image = root.GetComponent<Image>();
            image.color = new Color(0.16f, 0.22f, 0.32f);
            image.raycastTarget = true;

            TextMeshProUGUI label = UiFactory.CreateLabel(rect, "txtLabel", "第 1 幕", 38f,
                new Color(0.94f, 0.96f, 1f), new Vector2(320f, 80f),
                new Vector2(0.5f, 0.5f), Vector2.zero);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            var view = root.GetComponent<RouteOptionView>();
            var so = new SerializedObject(view);
            so.FindProperty("m_Button").objectReferenceValue = root.GetComponent<Button>();
            so.FindProperty("m_Label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, StageDir + "/RouteOption.prefab");
            Object.DestroyImmediate(root);
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
