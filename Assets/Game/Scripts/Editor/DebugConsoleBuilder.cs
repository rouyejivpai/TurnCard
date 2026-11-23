#if UNITY_EDITOR
using TMPro;
using TurnCard.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 生成调试台面板预制体（Resources/UI/Debug/DebugConsolePanel）。
    /// 菜单：Tools/TurnCard/生成调试台预制体
    /// </summary>
    public static class DebugConsoleBuilder
    {
        private const string Folder = "Assets/Game/Resources/UI/Debug";
        private const string PrefabPath = Folder + "/DebugConsolePanel.prefab";

        [MenuItem("Tools/TurnCard/生成调试台预制体")]
        public static void BuildFromMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[调试台] 预制体已生成：" + PrefabPath);
        }

        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Game/Resources/UI", "Debug");
            }

            var root = new GameObject("DebugConsolePanel", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24f, 24f);
            rect.sizeDelta = new Vector2(1200f, 460f);

            var bg = new GameObject("bg", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bg.transform;
            bgRect.SetParent(rect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = new Color(0.03f, 0.04f, 0.06f, 0.92f);
            bgImage.raycastTarget = true;

            TextMeshProUGUI title = UiFactory.CreateLabel(rect, "txtTitle",
                "调试控制台   ·   ` 开关   ·   help 查看命令", 20f,
                new Color(0.55f, 0.64f, 0.74f),
                new Vector2(1152f, 32f), new Vector2(0f, 1f), new Vector2(24f, -18f));
            title.alignment = TextAlignmentOptions.Left;
            title.rectTransform.pivot = new Vector2(0f, 1f);

            TextMeshProUGUI output = UiFactory.CreateLabel(rect, "txtOutput", string.Empty, 18f,
                new Color(0.92f, 0.92f, 0.92f),
                new Vector2(1152f, 340f), new Vector2(0f, 1f), new Vector2(24f, -58f));
            output.alignment = TextAlignmentOptions.TopLeft;
            output.enableWordWrapping = false;
            output.rectTransform.pivot = new Vector2(0f, 1f);

            TextMeshProUGUI input = UiFactory.CreateLabel(rect, "txtInput", "> _", 22f,
                new Color(0.61f, 1f, 0.61f),
                new Vector2(1152f, 32f), new Vector2(0f, 0f), new Vector2(24f, 18f));
            input.alignment = TextAlignmentOptions.Left;
            input.enableWordWrapping = false;
            input.rectTransform.pivot = new Vector2(0f, 0f);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
