using System.Collections.Generic;
using TMPro;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 一排 buff 图标：图标 + 层数。
    ///
    /// 玩家 HUD 和敌人卡片共用同一个组件，订阅 <see cref="BuffContainer.Changed"/>
    /// 自动刷新，不需要谁去手动同步。
    /// </summary>
    public sealed class BuffIconStrip : MonoBehaviour
    {
        private const float IconSize = 26f;
        private const float Spacing = 4f;

        private RectTransform m_Root;
        private BuffContainer m_Container;
        private readonly List<GameObject> m_Icons = new List<GameObject>();
        private readonly Dictionary<string, Sprite> m_SpriteCache = new Dictionary<string, Sprite>();

        public static BuffIconStrip Attach(RectTransform parent, BuffContainer container,
            Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            var go = new GameObject("BuffStrip", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(200f, IconSize);

            var strip = go.AddComponent<BuffIconStrip>();
            strip.Bind(rect, container);
            return strip;
        }

        private void Bind(RectTransform root, BuffContainer container)
        {
            m_Root = root;

            if (m_Container != null)
            {
                m_Container.Changed -= OnChanged;
            }

            m_Container = container;
            if (m_Container != null)
            {
                m_Container.Changed += OnChanged;
            }

            Rebuild();
        }

        private void OnDestroy()
        {
            if (m_Container != null)
            {
                m_Container.Changed -= OnChanged;
            }
        }

        private void OnChanged(BuffContainer container)
        {
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (GameObject icon in m_Icons)
            {
                if (icon != null)
                {
                    Destroy(icon);
                }
            }

            m_Icons.Clear();

            if (m_Container == null || m_Root == null)
            {
                return;
            }

            int index = 0;
            foreach (BuffInstance buff in m_Container.Buffs)
            {
                if (buff == null || buff.Definition == null || buff.Stacks <= 0)
                {
                    continue;
                }

                CreateIcon(buff, index);
                index++;
            }
        }

        private void CreateIcon(BuffInstance buff, int index)
        {
            var go = new GameObject("Buff_" + buff.Definition.Name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(m_Root, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            rect.anchoredPosition = new Vector2(index * (IconSize + Spacing), 0f);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = LoadIcon(buff.Definition.Icon);

            if (image.sprite == null)
            {
                // 没有图标素材时的兜底：底色块 + 首字
                image.color = ColorFor(buff.Definition.Name);
            }

            // 层数
            TextMeshProUGUI label = UiFactory.CreateLabel(rect, "Stacks", buff.Stacks.ToString(), 14,
                Color.white, new Vector2(IconSize, 14f), new Vector2(1f, 0f), new Vector2(2f, -2f));
            label.alignment = TextAlignmentOptions.BottomRight;
            label.raycastTarget = false;

            m_Icons.Add(go);
        }

        private static Color ColorFor(string buffName)
        {
            switch (buffName)
            {
                case "Vulnerable":
                    return new Color(0.85f, 0.45f, 0.45f, 0.9f);
                case "Strength":
                    return new Color(0.95f, 0.75f, 0.35f, 0.9f);
                case "Weak":
                    return new Color(0.55f, 0.55f, 0.75f, 0.9f);
                default:
                    return new Color(0.6f, 0.7f, 0.6f, 0.9f);
            }
        }

        private Sprite LoadIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }

            if (m_SpriteCache.TryGetValue(iconName, out Sprite cached))
            {
                return cached;
            }

            var tex = Resources.Load<Texture2D>("Art/UI/buffs/" + iconName);
            Sprite sprite = null;
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }

            m_SpriteCache[iconName] = sprite;
            return sprite;
        }
    }
}
