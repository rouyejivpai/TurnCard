using System.Collections.Generic;
using TMPro;
using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 关键词提示的容器：把若干 <see cref="KeywordTip"/> 竖直排在卡牌左侧。
    /// 卡片入场时立刻显示，移出即隐藏；右侧的「背面预览」仍然按原来的 0.8 秒逻辑走，两者并存。
    /// </summary>
    /// <summary>一条提示内容（关键词、buff 都往里塞）。</summary>
    public readonly struct TipEntry
    {
        public TipEntry(string title, string body)
        {
            Title = title;
            Body = body;
        }

        public string Title { get; }
        public string Body { get; }
    }

    public sealed class KeywordTooltipPanel : MonoBehaviour
    {
        private const float Gap = 10f;
        private const float CardGap = 18f;

        private RectTransform m_Rect;
        private readonly List<KeywordTip> m_Tips = new List<KeywordTip>();

        private static KeywordTooltipPanel s_Instance;

        public static KeywordTooltipPanel Ensure(Transform canvas)
        {
            if (s_Instance != null)
            {
                return s_Instance;
            }

            var go = new GameObject("KeywordTooltips", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            s_Instance = go.AddComponent<KeywordTooltipPanel>();
            s_Instance.m_Rect = rect;
            return s_Instance;
        }

        /// <summary>在卡牌左侧显示这些关键词；列表为空则隐藏。</summary>
        public void Show(RectTransform card, IReadOnlyList<KeywordDefinition> keywords)
        {
            if (card == null || keywords == null || keywords.Count == 0)
            {
                Hide();
                return;
            }

            var entries = new List<TipEntry>(keywords.Count);
            foreach (KeywordDefinition keyword in keywords)
            {
                entries.Add(new TipEntry(keyword.Name, keyword.Description));
            }

            ShowEntries(card, entries);
        }

        /// <summary>
        /// 显示任意一组提示条目（buff 图标悬浮等复用同一套外观与避让逻辑）。
        /// </summary>
        public void ShowEntries(RectTransform anchor, IReadOnlyList<TipEntry> entries)
        {
            Hide();

            if (anchor == null || entries == null || entries.Count == 0)
            {
                return;
            }

            float totalHeight = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                KeywordTip tip = KeywordTip.Create(m_Rect);
                tip.gameObject.SetActive(true);
                totalHeight += tip.Apply(entries[i].Title, entries[i].Body);
                if (i > 0)
                {
                    totalHeight += Gap;
                }

                m_Tips.Add(tip);
            }

            Layout(anchor, totalHeight);

            // 放到最上层，避免被手牌挡住
            m_Rect.SetAsLastSibling();
        }

        private void Layout(RectTransform card, float totalHeight)
        {
            // 面板与卡牌同属一个 Canvas，直接在本地坐标系里换算，避免屏幕坐标往返误差
            Vector3 cardLeftWorld = card.TransformPoint(
                new Vector3(-card.rect.width * 0.5f, 0f, 0f));
            Vector2 left = m_Rect.InverseTransformPoint(cardLeftWorld);

            float top = left.y + totalHeight * 0.5f;
            float x = left.x - CardGap;

            // 左侧放不下（贴屏幕边）就翻到卡牌右侧，避免被裁掉
            float bound = m_Rect.rect.xMin + 12f;
            if (x - KeywordTip.Width < bound)
            {
                Vector3 cardRightWorld = card.TransformPoint(
                    new Vector3(card.rect.width * 0.5f, 0f, 0f));
                Vector2 right = m_Rect.InverseTransformPoint(cardRightWorld);
                x = right.x + CardGap + KeywordTip.Width;
            }

            // 纵向别超出上下边界
            float topLimit = m_Rect.rect.yMax - 12f;
            top = Mathf.Min(top, topLimit);
            float bottomLimit = m_Rect.rect.yMin + 12f;
            if (top - totalHeight < bottomLimit)
            {
                top = bottomLimit + totalHeight;
            }

            foreach (KeywordTip tip in m_Tips)
            {
                var tipRect = (RectTransform)tip.transform;
                float height = tipRect.sizeDelta.y;
                // pivot 在右侧中心：anchoredPosition.x 就是右边界，框体向左展开
                tipRect.anchoredPosition = new Vector2(x, top - height * 0.5f);
                top -= height + Gap;
            }
        }

        public void Hide()
        {
            foreach (KeywordTip tip in m_Tips)
            {
                if (tip != null)
                {
                    // Destroy 要到帧末才生效，先关掉，避免同一帧内新旧框叠在一起
                    tip.gameObject.SetActive(false);
                    Destroy(tip.gameObject);
                }
            }

            m_Tips.Clear();
        }
    }
}
