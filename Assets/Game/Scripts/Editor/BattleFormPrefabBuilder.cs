using System.IO;
using TurnCard.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 生成战斗界面预制体，并用 UIBind 生成对应的窗口脚本。
    ///
    /// 注意：UIBindMono 在 OnEnable 里会解析预制体信息，解析失败会自毁，
    /// 因此必须先存成预制体，再用 LoadPrefabContents 进入预制体上下文加绑定。
    /// </summary>
    public static class BattleFormPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Game/Resources/UI/Battle";
        private const string PrefabPath = PrefabFolder + "/BattleFormWindow.prefab";
        private const string ScriptFolder = "Assets/Game/Scripts/UI/Battle";

        [MenuItem("Tools/TurnCard/生成战斗界面", priority = 210)]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(ScriptFolder);
            AssetDatabase.Refresh();

            BuildHierarchyAndSave();
            BindAndGenerate();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[战斗界面] 已生成 " + PrefabPath);
        }

        private static void BuildHierarchyAndSave()
        {
            var root = new GameObject("BattleFormWindow", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(1920f, 1080f);

            RectTransform background = UiFactory.CreateBox(root.transform, "Background",
                new Color(0.07f, 0.075f, 0.09f), new Vector2(1920f, 1080f),
                new Vector2(0.5f, 0.5f), Vector2.zero);
            background.SetAsFirstSibling();

            UiFactory.CreateLabel(root.transform, "Stage", "关卡 1 / 5", 28,
                new Color(0.80f, 0.82f, 0.88f), new Vector2(320f, 44f), new Vector2(0f, 1f),
                new Vector2(180f, -60f), TMPro.TextAlignmentOptions.Left);

            UiFactory.CreateLabel(root.transform, "txtStatus", string.Empty, 30,
                new Color(0.92f, 0.93f, 0.96f), new Vector2(760f, 44f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f));

            RectTransform hand = UiFactory.CreateRect(root.transform, "Hand");
            hand.anchorMin = new Vector2(0.5f, 0.5f);
            hand.anchorMax = new Vector2(0.5f, 0.5f);
            hand.sizeDelta = new Vector2(1500f, 320f);
            hand.anchoredPosition = new Vector2(0f, -300f);

            RectTransform drawPile = UiFactory.CreateBox(root.transform, "btnDrawPile",
                new Color(0.13f, 0.15f, 0.20f), new Vector2(170f, 120f), new Vector2(0f, 0f),
                new Vector2(140f, 110f));
            drawPile.gameObject.AddComponent<Button>();
            UiFactory.CreateLabel(drawPile, "Caption", "抽牌堆", 22, new Color(0.75f, 0.78f, 0.85f),
                new Vector2(170f, 32f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f));
            UiFactory.CreateLabel(drawPile, "txtDrawCount", "0", 34, Color.white,
                new Vector2(170f, 40f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f));

            RectTransform discardPile = UiFactory.CreateBox(root.transform, "btnDiscardPile",
                new Color(0.13f, 0.15f, 0.20f), new Vector2(170f, 120f), new Vector2(1f, 0f),
                new Vector2(-140f, 110f));
            discardPile.gameObject.AddComponent<Button>();
            UiFactory.CreateLabel(discardPile, "Caption", "弃牌堆", 22,
                new Color(0.75f, 0.78f, 0.85f), new Vector2(170f, 32f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 26f));
            UiFactory.CreateLabel(discardPile, "txtDiscardCount", "0", 34, Color.white,
                new Vector2(170f, 40f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f));

            UiFactory.CreateButton(root.transform, "btnEndTurn", "结束回合",
                new Color(0.22f, 0.42f, 0.30f), new Vector2(220f, 72f), new Vector2(1f, 0f),
                new Vector2(-170f, 280f), Color.white);

            UiFactory.CreateLabel(root.transform, "txtHint", string.Empty, 26,
                new Color(1f, 0.72f, 0.45f), new Vector2(900f, 44f), new Vector2(0.5f, 0f),
                new Vector2(0f, 400f));

            UiFactory.CreateLabel(root.transform, "txtResult", string.Empty, 64, Color.white,
                new Vector2(1200f, 120f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f));

            foreach (TMPro.TextMeshProUGUI text in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                text.font = null;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BindAndGenerate()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var mono = contents.GetComponent<TurnCard.UIBind.UIBindMono>();
                if (mono == null)
                {
                    mono = contents.AddComponent<TurnCard.UIBind.UIBindMono>();
                }

                if (mono == null)
                {
                    Debug.LogError("[战斗界面] 无法添加 UIBindMono。");
                    return;
                }

                Bind(mono, contents, "txtStatus");
                Bind(mono, contents, "txtDrawCount");
                Bind(mono, contents, "txtDiscardCount");
                Bind(mono, contents, "txtHint");
                Bind(mono, contents, "txtResult");
                Bind(mono, contents, "btnEndTurn", typeof(Button));
                Bind(mono, contents, "btnDrawPile", typeof(Button));
                Bind(mono, contents, "btnDiscardPile", typeof(Button));

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.Refresh();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var prefabMono = prefab.GetComponent<TurnCard.UIBind.UIBindMono>();
            GenerateScripts(prefabMono);
        }

        private static void Bind(TurnCard.UIBind.UIBindMono mono, GameObject root, string childName,
            System.Type componentType = null)
        {
            Transform child = FindDeep(root.transform, childName);
            if (child == null)
            {
                Debug.LogWarning("[战斗界面] 找不到节点 " + childName);
                return;
            }

            Object target = componentType == null ? (Object)child.GetComponent<Text>() : child.GetComponent(componentType);
            if (target == null)
            {
                target = child.gameObject;
            }

            mono.UIObjectsArray.Add(target);
            mono.UIObjectsName.Add(childName);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void GenerateScripts(TurnCard.UIBind.UIBindMono mono)
        {
            if (mono == null)
            {
                Debug.LogError("[战斗界面] 预制体上没有 UIBindMono，跳过脚本生成。");
                return;
            }

            var info = new TurnCard.UIBind.UIPrefabInfo
            {
                PrefabName = "BattleForm",
                ScriptName = "BattleForm",
                ModuleName = "Battle",
                AssetPath = PrefabFolder + "/",
                NormalScriptPath = ScriptFolder + "/BattleForm.cs",
                ComponentScriptPath = ScriptFolder + "/BattleForm.Components.cs",
                PrefabType = TurnCard.UIBind.UIPrefabInfo.UIPrefabType.Window,
                IsCommon = false
            };

            TurnCard.UIBind.Editor.UIBindHelper.CreateUIScript(info, 0);
            TurnCard.UIBind.Editor.UIBindHelper.AddToFile(info, mono);
            AssetDatabase.Refresh();
        }
    }
}
