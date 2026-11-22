using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using TurnCard.Game.UI;
using TurnCard.UIBind;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 生成流程类界面（主菜单 / 卡牌网格 / 卡牌列表）的预制体，并回填 UIBind 的绑定半身脚本。
    ///
    /// 与 <see cref="BattleFormPrefabBuilder"/> 的差别：**不覆盖已存在的界面逻辑脚本**。
    /// UIBind 自带的 CreateUIScript 会把正常脚本整个重写成模板，这里的界面逻辑是手写的，
    /// 所以只生成 &lt;类名&gt;.Components.cs（字段 + InitComponents + OnCreate）。
    ///
    /// 预制体放在 Resources/UI/Flow/ 下：UIBind 的 UIPrefabInfo.Make 要求路径在
    /// Resources/UI/ 的子目录里（它取第二段作为模块名），放在 UI/ 根目录会解析失败。
    ///
    /// 两条硬规则：
    /// <list type="number">
    /// <item>预制体已存在就不重建，只补齐缺失的绑定并同步绑定脚本（手工调过的布局不会被覆盖）。</item>
    /// <item>所有 .cs 都写完之后才 Refresh：中途 ImportAsset 会触发脚本重编译，把生成流程打断。</item>
    /// </list>
    /// </summary>
    public static class FlowPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Game/Resources/UI/Flow";
        private const string ScriptFolder = "Assets/Game/Scripts/UI";
        private const string ScriptNamespace = "TurnCard.Game.UI";

        private const string MenuPrefabPath = PrefabFolder + "/MenuScreenWindow.prefab";
        private const string CardGridPrefabPath = PrefabFolder + "/CardGridScreenWindow.prefab";
        private const string CardListPrefabPath = PrefabFolder + "/CardListViewScreenWindow.prefab";

        private static readonly Bind[] MenuBinds =
        {
            new Bind("txtTitle", typeof(TextMeshProUGUI)),
            new Bind("txtSubtitle", typeof(TextMeshProUGUI)),
            new Bind("goButtons", typeof(RectTransform)),
            new Bind("btnTemplate", typeof(Button)),
        };

        private static readonly Bind[] CardGridBinds =
        {
            new Bind("txtTitle", typeof(TextMeshProUGUI)),
            new Bind("txtSubtitle", typeof(TextMeshProUGUI)),
            new Bind("goCards", typeof(RectTransform)),
            new Bind("goBottom", typeof(RectTransform)),
            new Bind("btnTemplate", typeof(Button)),
            new Bind("txtHint", typeof(TextMeshProUGUI)),
        };

        private static readonly Bind[] CardListBinds =
        {
            new Bind("btnOverlay", typeof(Button)),
            new Bind("goPanel", typeof(RectTransform)),
            new Bind("txtTitle", typeof(TextMeshProUGUI)),
            new Bind("txtSubtitle", typeof(TextMeshProUGUI)),
            new Bind("btnClose", typeof(Button)),
            new Bind("scrollList", typeof(ScrollRect)),
            new Bind("goContent", typeof(RectTransform)),
        };

        [MenuItem("Tools/TurnCard/生成流程界面预制体", priority = 211)]
        public static void BuildAll()
        {
            Debug.Log("[流程预制体] 完成：\n" + BuildAllReport());
        }

        /// <summary>
        /// 生成并返回一份报告（命令行 / execute_code 里可直接拿到结果，不用翻 Console）。
        /// </summary>
        public static string BuildAllReport()
        {
            if (!Directory.Exists(PrefabFolder))
            {
                Directory.CreateDirectory(PrefabFolder);
                AssetDatabase.Refresh();
            }

            var outputs = new List<PrefabOutput>
            {
                EnsurePrefab(MenuPrefabPath, "MenuScreen", BuildMenuHierarchy, MenuBinds),
                EnsurePrefab(CardGridPrefabPath, "CardGridScreen", BuildCardGridHierarchy, CardGridBinds),
                EnsurePrefab(CardListPrefabPath, "CardListViewScreen", BuildCardListHierarchy, CardListBinds),
            };

            var report = new StringBuilder();
            // 全部写完之后再统一刷新，避免中途域重载打断生成
            foreach (PrefabOutput output in outputs)
            {
                if (output == null)
                {
                    continue;
                }

                // 绑定不全就绝不写脚本：写出去会把手写的界面逻辑变成一堆「找不到字段」的编译错误
                if (output.Names.Count != output.Expected)
                {
                    report.AppendLine(output.ClassName + " -> 未写脚本，绑定不全 " +
                                      output.Names.Count + "/" + output.Expected + " [" +
                                      string.Join(", ", output.Names.ToArray()) + "]");
                    continue;
                }

                WriteComponentsScript(output);
                report.AppendLine(output.ClassName + " -> " +
                                  (output.Names.Count == 0
                                      ? "(无绑定)"
                                      : string.Join(", ", output.Names.ToArray())));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report.ToString();
        }

        /// <summary>
        /// 不重建已有预制体，只补齐缺失绑定并取回绑定表。
        ///
        /// 关键点：不能直接 AddComponent&lt;UIBindMono&gt;。UIBindMono.OnEnable 会调
        /// UIPrefabInfo.Make 解析路径，而 LoadPrefabContents 打开的对象解析不出路径
        /// （IsPartOfPrefabAsset / PrefabStage 全为 false），解析失败就 DestroyImmediate 自毁。
        /// 所以先把根节点失活（失活时 AddComponent 不触发 OnEnable），手动写好 PrefabInfo 与绑定表，
        /// 再激活、保存。
        /// </summary>
        private static PrefabOutput EnsurePrefab(string path, string className,
            Func<GameObject> buildHierarchy, Bind[] binds)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                // 快路径：绑定已经正确就什么都不做。
                // 预制体一旦进 LoadPrefabContents 就会重新保存，白白制造 diff。
                PrefabOutput cached = TryReadBindings(existing, className, binds);
                if (cached != null)
                {
                    Debug.Log("[流程预制体] 布局与绑定都是最新的，跳过：" + path);
                    return cached;
                }

                Debug.Log("[流程预制体] 已存在但绑定不全，进入预制体上下文补齐：" + path);
            }
            else
            {
                GameObject root = buildHierarchy();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                UnityEngine.Object.DestroyImmediate(root);
                Debug.Log("[流程预制体] 新建 " + path);
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            bool wasActive = contents.activeSelf;
            try
            {
                contents.SetActive(false);

                var mono = contents.GetComponent<UIBindMono>();
                bool dirty = existing == null || !wasActive;
                if (mono == null)
                {
                    mono = contents.AddComponent<UIBindMono>();
                    dirty = true;
                }

                if (mono.PrefabInfo == null)
                {
                    mono.PrefabInfo = MakePrefabInfo(className);
                }

                dirty |= EnsureBindings(mono, contents, className, binds);

                if (mono.UIObjectsArray.Count != binds.Length)
                {
                    Debug.LogError("[流程预制体] " + className + " 绑定数量不对：期望 " +
                                   binds.Length + "，实际 " + mono.UIObjectsArray.Count +
                                   "（[" + string.Join(",", mono.UIObjectsName.ToArray()) + "]）");
                }

                contents.SetActive(wasActive);
                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }

                return new PrefabOutput
                {
                    ClassName = className,
                    Expected = binds.Length,
                    HashCode = mono.GetHashCode(),
                    Names = new List<string>(mono.UIObjectsName),
                    TypeNames = GetTypeNames(mono),
                };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 直接读预制体资产上的绑定表（资产上的组件不会触发 OnEnable，所以不会被自毁）。
        /// 绑定与预期完全一致时返回快照，否则返回 null 表示需要进上下文修。
        /// </summary>
        private static PrefabOutput TryReadBindings(GameObject prefab, string className, Bind[] binds)
        {
            var mono = prefab.GetComponent<UIBindMono>();
            if (mono == null || mono.UIObjectsArray.Count != binds.Length ||
                mono.UIObjectsName.Count != binds.Length)
            {
                return null;
            }

            for (int i = 0; i < binds.Length; i++)
            {
                if (mono.UIObjectsName[i] != binds[i].Name)
                {
                    return null;
                }

                UnityEngine.Object bound = mono.UIObjectsArray[i];
                if (bound == null)
                {
                    return null;
                }

                System.Type expected = binds[i].Type ?? typeof(GameObject);
                if (!expected.IsInstanceOfType(bound))
                {
                    return null;
                }
            }

            return new PrefabOutput
            {
                ClassName = className,
                Expected = binds.Length,
                HashCode = mono.GetHashCode(),
                Names = new List<string>(mono.UIObjectsName),
                TypeNames = GetTypeNames(mono),
            };
        }

        /// <summary>
        /// 手动构造 UIBind 的预制体信息（等价于 UIPrefabInfo.Make 正常路径下的结果）。
        /// 只在编辑器面板展示与「重新绑定」时用到，运行时不依赖它。
        /// </summary>
        private static UIPrefabInfo MakePrefabInfo(string className)
        {
            return new UIPrefabInfo
            {
                ScriptName = className,
                PrefabName = className + "Window",
                ModuleName = "Flow",
                ClipPath = "Flow/",
                AssetPath = "Flow/" + className + "Window",
                NormalScriptPath = ScriptFolder + "/" + className + ".cs",
                ComponentScriptPath = ScriptFolder + "/" + className + ".Components.cs",
                IsCommon = false,
                PrefabType = UIPrefabInfo.UIPrefabType.Window,
                IsCreateByUIBind = true,
            };
        }

        /// <summary>只补缺失项：已经绑好的不重复登记（UIBindMono.AddOrRemove 对重复项是「取消绑定」）。</summary>
        private static bool EnsureBindings(UIBindMono mono, GameObject contents, string className,
            Bind[] binds)
        {
            bool changed = false;
            foreach (Bind bind in binds)
            {
                if (mono.GetObjectByName(bind.Name) != null)
                {
                    continue;
                }

                string nodeName = string.IsNullOrEmpty(bind.Node) ? bind.Name : bind.Node;
                Transform node = FindDeep(contents.transform, nodeName);
                if (node == null)
                {
                    Debug.LogError("[流程预制体] " + className + " 找不到节点 " + nodeName);
                    continue;
                }

                UnityEngine.Object target = bind.Type == null
                    ? (UnityEngine.Object)node.gameObject
                    : node.GetComponent(bind.Type);
                if (target == null)
                {
                    Debug.LogError("[流程预制体] " + className + " 节点 " + nodeName +
                                   " 上没有 " + bind.Type.Name);
                    continue;
                }

                mono.AddOrRemove(target, bind.Name);
                changed = true;
                Debug.Log("[流程预制体] " + className + " 补绑定 " + bind.Name);
            }

            return changed;
        }

        private static List<string> GetTypeNames(UIBindMono mono)
        {
            var names = new List<string>(mono.UIObjectsArray.Count);
            foreach (UnityEngine.Object obj in mono.UIObjectsArray)
            {
                names.Add(obj.GetType().ToString());
            }

            return names;
        }

        private static void WriteComponentsScript(PrefabOutput output)
        {
            string path = ScriptFolder + "/" + output.ClassName + ".Components.cs";

            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using TurnCard.UIBind;");
            sb.AppendLine();
            sb.AppendLine("namespace " + ScriptNamespace);
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 生成的窗口脚本。InitComponents 里的绑定由 UIBind 自动维护，不要手改标记区域。");
            sb.AppendLine("    /// 结构与 UIBind 的 WindowComponentTemplate 一致；基类在 " +
                          output.ClassName + ".cs 里声明。");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public partial class " + output.ClassName);
            sb.AppendLine("    {");
            sb.AppendLine("        #region (禁止编辑)自动生成变量");
            sb.AppendLine("        // HashCode:" + output.HashCode);
            for (int i = 0; i < output.Names.Count; i++)
            {
                sb.AppendLine("        protected " + output.TypeNames[i] + " " + output.Names[i] + ";");
            }

            sb.AppendLine("        #endregion (禁止编辑)自动生成变量");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnCreate()");
            sb.AppendLine("        {");
            sb.AppendLine("            InitComponents();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private void InitComponents()");
            sb.AppendLine("        {");
            sb.AppendLine("            UIBindMono quickUI = RectTransform.GetComponent<UIBindMono>();");
            sb.AppendLine();
            sb.AppendLine("            #region (禁止编辑)自动生成绑定");
            for (int i = 0; i < output.Names.Count; i++)
            {
                sb.AppendLine("            " + output.Names[i] + " = quickUI.UIObjectsArray[" + i +
                              "] as " + output.TypeNames[i] + ";");
            }

            sb.AppendLine("            #endregion (禁止编辑)自动生成绑定");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string content = sb.ToString();
            if (File.Exists(path) && File.ReadAllText(path) == content)
            {
                return;   // 内容没变就不写，避免无意义的时间戳变化
            }

            File.WriteAllText(path, content, new UTF8Encoding(false));
            Debug.Log("[流程预制体] 已写入 " + path);
        }

        // ---------------------------------------------------------------- 主菜单

        /// <summary>标题 + 副标题 + 一列按钮。主菜单 / 地图 / 营地 / 结算都用它。</summary>
        private static GameObject BuildMenuHierarchy()
        {
            var root = CreateRoot("MenuScreenWindow", new Color(0.07f, 0.075f, 0.09f));

            UiFactory.CreateLabel(root.transform, "txtTitle", string.Empty, 64f, Color.white,
                new Vector2(1200f, 90f), new Vector2(0.5f, 1f), new Vector2(0f, -110f));

            UiFactory.CreateLabel(root.transform, "txtSubtitle", string.Empty, 26f,
                new Color(0.75f, 0.78f, 0.86f), new Vector2(1400f, 40f),
                new Vector2(0.5f, 1f), new Vector2(0f, -190f));

            RectTransform buttons = UiFactory.CreateRect(root.transform, "goButtons");
            buttons.anchorMin = new Vector2(0.5f, 0.5f);
            buttons.anchorMax = new Vector2(0.5f, 0.5f);
            buttons.sizeDelta = new Vector2(720f, 600f);
            buttons.anchoredPosition = new Vector2(0f, -80f);

            var layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateButtonTemplate(buttons, 700f, 72f, 30f).gameObject.SetActive(false);
            return root;
        }

        // ------------------------------------------------------------ 卡牌网格

        /// <summary>标题 + 卡牌区 + 底部按钮条 + 提示。奖励三选一 / 商店货架都用它。</summary>
        private static GameObject BuildCardGridHierarchy()
        {
            var root = CreateRoot("CardGridScreenWindow", new Color(0.06f, 0.065f, 0.08f));

            UiFactory.CreateLabel(root.transform, "txtTitle", string.Empty, 56f, Color.white,
                new Vector2(1400f, 80f), new Vector2(0.5f, 1f), new Vector2(0f, -80f));

            UiFactory.CreateLabel(root.transform, "txtSubtitle", string.Empty, 24f,
                new Color(0.75f, 0.78f, 0.86f), new Vector2(1400f, 40f),
                new Vector2(0.5f, 1f), new Vector2(0f, -146f));

            RectTransform cards = UiFactory.CreateRect(root.transform, "goCards");
            cards.anchorMin = new Vector2(0.5f, 0.5f);
            cards.anchorMax = new Vector2(0.5f, 0.5f);
            cards.sizeDelta = new Vector2(1600f, 620f);
            cards.anchoredPosition = new Vector2(0f, -30f);

            RectTransform bottom = UiFactory.CreateRect(root.transform, "goBottom");
            bottom.anchorMin = new Vector2(0.5f, 0f);
            bottom.anchorMax = new Vector2(0.5f, 0f);
            bottom.sizeDelta = new Vector2(900f, 120f);
            bottom.anchoredPosition = new Vector2(0f, 130f);

            var layout = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateButtonTemplate(bottom, 260f, 68f, 26f).gameObject.SetActive(false);

            UiFactory.CreateLabel(root.transform, "txtHint", string.Empty, 22f,
                new Color(0.62f, 0.65f, 0.72f), new Vector2(1400f, 34f),
                new Vector2(0.5f, 0f), new Vector2(0f, 66f));

            return root;
        }

        // ------------------------------------------------------------ 卡牌列表

        /// <summary>遮罩 + 面板 + 标题 + 关闭 + 滚动区。牌组 / 删牌 / 牌堆都用它。</summary>
        private static GameObject BuildCardListHierarchy()
        {
            var root = new GameObject("CardListViewScreenWindow", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(1920f, 1080f);

            // 半透明遮罩：点空白处关闭
            RectTransform overlay = UiFactory.CreateBox(root.transform, "btnOverlay",
                new Color(0f, 0f, 0f, 0.72f), new Vector2(1920f, 1080f),
                new Vector2(0.5f, 0.5f), Vector2.zero);
            Stretch(overlay);
            var overlayButton = overlay.gameObject.AddComponent<Button>();
            overlayButton.transition = Selectable.Transition.None;
            overlayButton.targetGraphic = overlay.GetComponent<Image>();

            // 中央面板
            RectTransform panel = UiFactory.CreateBox(root.transform, "goPanel",
                new Color(0.09f, 0.10f, 0.13f, 0.98f), new Vector2(1560f, 880f),
                new Vector2(0.5f, 0.5f), Vector2.zero);

            // 面板本身吃掉点击，避免点到面板也关闭
            var blocker = panel.gameObject.AddComponent<Button>();
            blocker.transition = Selectable.Transition.None;
            blocker.targetGraphic = panel.GetComponent<Image>();

            UiFactory.CreateLabel(panel, "txtTitle", string.Empty, 42f, Color.white,
                new Vector2(900f, 60f), new Vector2(0f, 1f), new Vector2(30f, -42f),
                TextAlignmentOptions.Left);

            UiFactory.CreateLabel(panel, "txtSubtitle", string.Empty, 22f,
                new Color(0.72f, 0.75f, 0.82f), new Vector2(900f, 34f),
                new Vector2(0f, 1f), new Vector2(30f, -86f), TextAlignmentOptions.Left);

            RectTransform close = UiFactory.CreateBox(panel, "btnClose",
                new Color(0.18f, 0.22f, 0.30f), new Vector2(120f, 52f),
                new Vector2(1f, 1f), new Vector2(-80f, -46f));
            var closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = close.GetComponent<Image>();
            UiFactory.CreateLabel(close, "txtCaption", "关闭", 24f, Color.white,
                new Vector2(120f, 52f), new Vector2(0.5f, 0.5f), Vector2.zero);

            RectTransform viewport = UiFactory.CreateRect(panel, "scrollList");
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(30f, 30f);
            viewport.offsetMax = new Vector2(-30f, -110f);

            // RectMask2D 比 Mask 便宜，且不需要额外材质
            viewport.gameObject.AddComponent<RectMask2D>();

            // 手动排版：不用 GridLayoutGroup，避免它改写卡牌的 sizeDelta 导致「缩放两次」
            RectTransform content = UiFactory.CreateRect(viewport, "goContent");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.scrollSensitivity = 40f;

            return root;
        }

        // ------------------------------------------------------------ 公共部件

        private static GameObject CreateRoot(string name, Color background)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(1920f, 1080f);

            var image = root.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = true;
            return root;
        }

        /// <summary>按钮模板：布局组里的一块，运行时克隆它来加按钮。文字节点名固定为 txtCaption。</summary>
        private static RectTransform CreateButtonTemplate(RectTransform parent, float width, float height,
            float fontSize)
        {
            RectTransform box = UiFactory.CreateBox(parent, "btnTemplate",
                new Color(0.16f, 0.20f, 0.28f), new Vector2(width, height),
                new Vector2(0.5f, 0.5f), Vector2.zero);

            var element = box.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;

            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();

            UiFactory.CreateLabel(box, "txtCaption", string.Empty, fontSize, Color.white,
                new Vector2(width - 20f, height - 8f), new Vector2(0.5f, 0.5f), Vector2.zero);

            return box;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>一条绑定：变量名 + 组件类型（null 表示 GameObject）+ 节点名（为空表示根节点）。</summary>
        private readonly struct Bind
        {
            public Bind(string name, Type type, string node = null)
            {
                Name = name;
                Type = type;
                Node = node;
            }

            public string Name { get; }
            public Type Type { get; }
            public string Node { get; }
        }

        /// <summary>生成绑定脚本所需的快照（预制体卸载后不能再碰 mono）。</summary>
        private sealed class PrefabOutput
        {
            public string ClassName;
            public int Expected;
            public int HashCode;
            public List<string> Names;
            public List<string> TypeNames;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
