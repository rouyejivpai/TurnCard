using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 运行时构建 UGUI 的小工具。所有文字统一使用 TextMeshPro。
    /// 切片阶段用代码搭界面；有固定结构的界面已改为预制体 + UIBind。
    /// </summary>
    public static class UiFactory
    {
        private const string FontResourcePath = "Fonts/SimHei SDF";

        private static TMP_FontAsset s_Font;

        /// <summary>全局字体：优先用工程内的中文字体资产，退回 TMP 默认字体。</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (s_Font == null)
                {
                    s_Font = Resources.Load<TMP_FontAsset>(FontResourcePath);
                    if (s_Font == null)
                    {
                        s_Font = TMP_Settings.defaultFontAsset;
                    }
                }

                return s_Font;
            }
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform CreateBox(Transform parent, string name, Color color,
            Vector2 size, Vector2 anchor, Vector2 position)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = size;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        public static TextMeshProUGUI CreateLabel(Transform parent, string name, string content,
            float fontSize, Color color, Vector2 size, Vector2 anchor, Vector2 position,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = size;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font;
            text.fontSize = fontSize;
            text.text = content;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string caption,
            Color background, Vector2 size, Vector2 anchor, Vector2 position, Color textColor)
        {
            RectTransform rect = CreateBox(parent, name, background, size, anchor, position);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();

            CreateLabel(rect, "Caption", caption, 26, textColor, size, new Vector2(0.5f, 0.5f),
                Vector2.zero);

            return button;
        }

        /// <summary>给界面里所有 TMP 文本统一成工程字体。</summary>
        public static void EnsureFont(Transform root)
        {
            TMP_FontAsset font = Font;
            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                // TMP 会把 null 字体自动补成默认字体，所以这里不能只判空
                if (text.font != font)
                {
                    text.font = font;
                }
            }
        }

        /// <summary>生成一声很短的翻面音效，避免切片阶段依赖音频资源。</summary>
        public static AudioClip CreateFlipClickClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.08f;
            int sampleCount = (int)(sampleRate * duration);
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float envelope = Mathf.Pow(1f - t, 3f);
                float noise = Random.Range(-1f, 1f) * 0.35f;
                float tone = Mathf.Sin(2f * Mathf.PI * 900f * i / sampleRate) * 0.25f;
                samples[i] = (noise + tone) * envelope;
            }

            AudioClip clip = AudioClip.Create("FlipClick", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
