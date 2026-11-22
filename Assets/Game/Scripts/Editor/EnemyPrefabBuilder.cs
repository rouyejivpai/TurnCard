using System.IO;
using TMPro;
using TurnCard.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 生成敌人卡预制体 <c>Resources/UI/Enemy/EnemyCard</c>。
    ///
    /// 布局按 <c>Docs/21-敌人卡面.md</c>：48×64 网格 ×4 = 192×256，框边 3×4 = 12，
    /// 意图气泡 116×60 贴在卡框上沿，血量横幅 112×24 挂在卡框下沿。
    ///
    /// 现在是**结构版**：卡框用 PixelCardKit 的银框、气泡/横幅用红色面板顶替，
    /// 等美术出 48×64 的三张九宫格后，直接替换预制体里的 Sprite 即可，代码不用动。
    ///
    /// 与其它生成器一致：已存在就跳过，不覆盖手工调整。
    /// </summary>
    public static class EnemyPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Game/Resources/UI/Enemy";
        private const string PrefabPath = PrefabFolder + "/EnemyCard.prefab";

        private const string FrameSpritePath = "Assets/Game/Art/UI/PixelCardKit/Cards/card_frame_silver.png";
        private const string PanelSpritePath = "Assets/Game/Art/UI/PixelCardKit/Panels/Colored/red.png";

        // 显示尺寸：48×64 网格 ×4
        private const float CardWidth = 192f;
        private const float CardHeight = 256f;
        private const float BorderWidth = 12f;
        private const float IntentWidth = 116f;
        private const float IntentHeight = 60f;

        // 血量条：美术原生 50×8（空条）/ 46×6（实心），按 ×3 显示
        private const float HpWidth = 150f;
        private const float HpHeight = 24f;
        private const float HpInsetX = 6f;      // 50→46 的左右各 2px，×3
        private const float HpInsetY = 3f;      // 8→6 的上下各 1px，×3
        private const float HpFillWidth = HpWidth - HpInsetX * 2f;
        private const float HpFillHeight = HpHeight - HpInsetY * 2f;

        [MenuItem("Tools/TurnCard/生成敌人卡预制体", priority = 213)]
        public static void Build()
        {
            if (!Directory.Exists(PrefabFolder))
            {
                Directory.CreateDirectory(PrefabFolder);
                AssetDatabase.Refresh();
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log("[敌人卡] 已存在，跳过（不会覆盖手工修改）：" + PrefabPath);
                return;
            }

            GameObject root = BuildHierarchy();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[敌人卡] 已生成 " + PrefabPath);
        }

        private static GameObject BuildHierarchy()
        {
            var root = new GameObject("EnemyCard", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            var view = root.AddComponent<EnemyCardView>();

            // ---- 旋转体：立绘 + 卡框 ----
            RectTransform body = UiFactory.CreateRect(root.transform, "goBody");
            Stretch(body);

            RectTransform art = UiFactory.CreateRect(body, "imgArt");
            Stretch(art, BorderWidth);
            var artImage = art.gameObject.AddComponent<Image>();
            artImage.raycastTarget = false;
            artImage.preserveAspect = true;
            artImage.color = new Color(0.20f, 0.16f, 0.22f, 1f);

            RectTransform frame = UiFactory.CreateRect(body, "imgFrame");
            Stretch(frame);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.raycastTarget = false;
            frameImage.sprite = LoadSprite(FrameSpritePath);
            frameImage.type = Image.Type.Simple;
            frameImage.color = Color.white;

            TextMeshProUGUI placeholder = UiFactory.CreateLabel(body, "txtPlaceholder",
                string.Empty, 22f, new Color(0.85f, 0.82f, 0.88f),
                new Vector2(CardWidth - BorderWidth * 2f - 8f, 60f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -6f));
            placeholder.enableWordWrapping = true;

            // ---- 意图气泡（溢出在卡框上方）----
            RectTransform intent = UiFactory.CreateBox(root.transform, "goIntent",
                new Color(0.16f, 0.07f, 0.10f, 0.98f), new Vector2(IntentWidth, IntentHeight),
                new Vector2(0.5f, 1f), Vector2.zero);
            intent.pivot = new Vector2(0.5f, 0f);
            intent.anchoredPosition = new Vector2(0f, 4f);

            RectTransform icon = UiFactory.CreateRect(intent, "imgIntentIcon");
            icon.anchorMin = new Vector2(0f, 0.5f);
            icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.sizeDelta = new Vector2(40f, 40f);   // 图标美术 10×10，按 ×4 显示
            icon.anchoredPosition = new Vector2(10f, 0f);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            TextMeshProUGUI intentValue = UiFactory.CreateLabel(intent, "txtIntentValue",
                string.Empty, 30f, new Color(1f, 0.9f, 0.75f), new Vector2(54f, 40f),
                new Vector2(1f, 0.5f), new Vector2(-12f, 0f), TextAlignmentOptions.Right);

            // ---- 血量横幅（挂在卡框下方）----
            RectTransform banner = UiFactory.CreateRect(root.transform, "goHpBanner");
            banner.anchorMin = new Vector2(0.5f, 0f);
            banner.anchorMax = new Vector2(0.5f, 0f);
            banner.pivot = new Vector2(0.5f, 1f);
            banner.sizeDelta = new Vector2(HpWidth, HpHeight);
            banner.anchoredPosition = new Vector2(0f, -4f);

            RectTransform baseFill = UiFactory.CreateBox(banner, "imgHpBase",
                new Color(0.62f, 0.13f, 0.18f), new Vector2(HpWidth, HpHeight),
                new Vector2(0.5f, 0.5f), Vector2.zero);
            baseFill.GetComponent<Image>().sprite = LoadSprite(PanelSpritePath);

            // 裁剪窗口：铺满整条，宽度按血量比例伸缩（anchorMax.x = 比例）
            RectTransform clip = UiFactory.CreateRect(banner, "goHpClip");
            clip.anchorMin = new Vector2(0f, 0f);
            clip.anchorMax = new Vector2(1f, 1f);
            clip.offsetMin = Vector2.zero;
            clip.offsetMax = Vector2.zero;
            clip.gameObject.AddComponent<RectMask2D>();

            // 实心条：内缩在自身、尺寸固定，被裁的只是右侧（不拉伸美术）
            RectTransform fill = UiFactory.CreateRect(clip, "imgHpFill");
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(HpInsetX, 0f);
            fill.sizeDelta = new Vector2(HpFillWidth, -HpInsetY * 2f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.raycastTarget = false;
            fillImage.color = Color.white;
            fillImage.type = Image.Type.Simple;
            fillImage.sprite = LoadSprite(PanelSpritePath);

            UiFactory.CreateLabel(banner, "txtHpText", "0/0", 17f, new Color(1f, 0.94f, 0.86f),
                new Vector2(HpWidth, HpHeight), new Vector2(0.5f, 0.5f), Vector2.zero)
                .alignment = TextAlignmentOptions.Center;

            // ---- 护甲角标（左上角内侧）----
            RectTransform armor = UiFactory.CreateBox(root.transform, "goArmor",
                new Color(0.18f, 0.26f, 0.42f, 0.95f), new Vector2(34f, 34f),
                new Vector2(0f, 1f), new Vector2(6f, -6f));
            armor.GetComponent<Image>().raycastTarget = false;
            UiFactory.CreateLabel(armor, "txtArmor", "0", 18f, new Color(0.86f, 0.92f, 1f),
                new Vector2(34f, 34f), new Vector2(0.5f, 0.5f), Vector2.zero);

            armor.gameObject.SetActive(false);

            WireView(view, body, artImage, placeholder, intent.gameObject, iconImage, intentValue,
                clip, fillImage, banner.Find("txtHpText").GetComponent<TextMeshProUGUI>(),
                armor.gameObject, armor.Find("txtArmor").GetComponent<TextMeshProUGUI>());

            return root;
        }

        private static void WireView(EnemyCardView view, RectTransform body, Image art,
            TextMeshProUGUI placeholder, GameObject intentRoot, Image intentIcon,
            TextMeshProUGUI intentValue, RectTransform hpClip, Image hpFill, TextMeshProUGUI hpText,
            GameObject armorRoot, TextMeshProUGUI armorText)
        {
            var serialized = new SerializedObject(view);
            serialized.FindProperty("m_Body").objectReferenceValue = body;
            serialized.FindProperty("m_Art").objectReferenceValue = art;
            serialized.FindProperty("m_Placeholder").objectReferenceValue = placeholder;
            serialized.FindProperty("m_IntentRoot").objectReferenceValue = intentRoot;
            serialized.FindProperty("m_IntentIcon").objectReferenceValue = intentIcon;
            serialized.FindProperty("m_IntentValue").objectReferenceValue = intentValue;
            serialized.FindProperty("m_HpClip").objectReferenceValue = hpClip;
            serialized.FindProperty("m_HpFill").objectReferenceValue = hpFill;
            serialized.FindProperty("m_HpText").objectReferenceValue = hpText;
            serialized.FindProperty("m_ArmorRoot").objectReferenceValue = armorRoot;
            serialized.FindProperty("m_ArmorText").objectReferenceValue = armorText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning("[敌人卡] 占位素材缺失：" + path);
            }

            return sprite;
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
