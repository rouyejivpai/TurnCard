using DG.Tweening;
using TMPro;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 背面预览面板：悬停或右键一张卡时，在这张卡**自己的右上角**弹出它翻面之后的样子。
    ///
    /// 面板本身就是一个 <see cref="CardView"/>（复用卡牌预制体），
    /// 所以文字解析、关键词、边框皮肤与真卡完全一致，样式只需要维护一份。
    /// </summary>
    public sealed class CardInspectPanel : MonoBehaviour
    {
        private const float PreviewScale = 1.35f;
        private const float Gap = 18f;

        private RectTransform m_Root;
        private RectTransform m_CanvasRect;
        private CardView m_Face;
        private TextMeshProUGUI m_Title;

        public static CardInspectPanel Create(Transform parent)
        {
            var go = new GameObject("InspectPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            // 自己的 Canvas：预览必须盖在所有界面之上
            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000;
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var root = (RectTransform)go.transform;
            // 锚点用画布中心（RectTransformUtility 返回的就是中心原点的局部坐标），
            // pivot 用左下角，这样 anchoredPosition 直接就是「面板左下角」的位置。
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = Vector2.zero;
            root.sizeDelta = new Vector2(CardView.CardWidth * PreviewScale,
                CardView.CardHeight * PreviewScale);

            var panel = go.AddComponent<CardInspectPanel>();
            panel.Build(root, parent as RectTransform);
            go.SetActive(false);
            return panel;
        }

        private void Build(RectTransform root, RectTransform canvasRect)
        {
            m_Root = root;
            m_CanvasRect = canvasRect;

            m_Face = CardView.CreateDisplay(root, PreviewScale);

            m_Title = UiFactory.CreateLabel(root, "Title", "翻面后", 20,
                new Color(0.72f, 0.74f, 0.80f), new Vector2(300f, 28f),
                new Vector2(0.5f, 1f), new Vector2(0f, 24f));
            m_Title.raycastTarget = false;
        }

        /// <summary>在这张卡的右上角显示它翻面后的卡面。</summary>
        public void Show(CardInstance card, RectTransform cardRect)
        {
            if (card == null || m_Face == null)
            {
                Hide();
                return;
            }

            // 造一个只用来预览的实例，并把朝向翻到另一面
            var preview = new CardInstance(card.Definition);
            if (card.Side == CardFaceSide.Front)
            {
                preview.Flip();
            }

            CardFace face = preview.Face;
            m_Face.ShowFace(face, CardView.BackgroundFor(face.Type));

            // 战斗里要显示受 buff 影响的真实数值
            CardPreviewSource.Apply(m_Face, face);

            m_Title.text = preview.Side == CardFaceSide.Back ? "翻面后 · 背面" : "翻面后 · 正面";

            m_Root.gameObject.SetActive(true);
            m_Root.SetAsLastSibling();
            PlaceNear(cardRect);
            PlayPop();
        }

        /// <summary>贴到卡牌右上角；贴不下时翻到左边 / 下面，保证整块都在画面内。</summary>
        private void PlaceNear(RectTransform cardRect)
        {
            if (cardRect == null || m_CanvasRect == null)
            {
                return;
            }

            var corners = new Vector3[4];
            cardRect.GetWorldCorners(corners);   // 0:左下 1:左上 2:右上 3:右下

            Vector2 topRight = WorldToCanvasLocal(corners[2]);
            Vector2 topLeft = WorldToCanvasLocal(corners[1]);

            Vector2 size = m_Root.sizeDelta;
            Vector2 canvasSize = m_CanvasRect.rect.size;

            // 默认放在卡牌右上方
            Vector2 pos = topRight + new Vector2(Gap, Gap);

            // 右边放不下 → 放到卡牌左侧
            if (pos.x + size.x > canvasSize.x * 0.5f)
            {
                pos.x = topLeft.x - Gap - size.x;
            }

            // 上边放不下 → 往下贴
            if (pos.y + size.y > canvasSize.y * 0.5f)
            {
                pos.y = canvasSize.y * 0.5f - size.y;
            }

            m_Root.anchoredPosition = pos;
        }

        private Vector2 WorldToCanvasLocal(Vector3 world)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_CanvasRect, screen, null, out Vector2 local);
            return local;
        }

        public void Hide()
        {
            if (m_Root != null)
            {
                m_Root.DOKill();
                m_Root.gameObject.SetActive(false);
            }
        }

        private void PlayPop()
        {
            m_Root.DOKill();
            m_Root.localScale = Vector3.one * 0.92f;
            m_Root.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutQuad);
        }
    }
}