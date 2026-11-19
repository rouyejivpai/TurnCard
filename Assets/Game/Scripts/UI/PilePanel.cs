using System.Collections.Generic;
using TMPro;
using TurnCard.Game.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 抽牌堆 / 弃牌堆的查看面板：以真实卡面陈列。
    /// 面板框（遮罩、标题、关闭按钮）来自预制体 <c>Resources/UI/PilePanel</c>，
    /// 卡牌网格仍由 <see cref="CardView"/> 逐张生成。
    /// </summary>
    public sealed class PilePanel : MonoBehaviour
    {
        private const string PrefabResourcePath = "UI/PilePanel";

        private const float PanelWidth = 1920f;
        private const float PanelHeight = 1080f;
        private const int Columns = 5;
        private const float DisplayWidth = 250f;
        private const float DisplayScale = DisplayWidth / CardView.CardWidth;
        private const float ColumnStep = DisplayWidth + 36f;
        private const float RowStep = CardView.CardHeight * DisplayScale + 36f;

        [Header("面板框节点（由预制体绑定）")]
        [SerializeField] private RectTransform m_Panel;
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private TextMeshProUGUI m_Empty;

        private static PilePanel s_Prefab;
        private readonly List<GameObject> m_Displayed = new List<GameObject>();

        public bool IsOpen => gameObject.activeSelf;

        public static PilePanel Create(Transform parent)
        {
            PilePanel prefab = LoadPrefab();
            PilePanel panel;

            if (prefab != null)
            {
                panel = Instantiate(prefab, parent, false);
            }
            else
            {
                var go = new GameObject("PilePanel", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                panel = go.AddComponent<PilePanel>();
                panel.BuildDefaultHierarchy();
            }

            var rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panel.WireButtons();
            panel.EnsureFonts();
            panel.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>预制体里没存字体（动态字体不是资产），运行时统一补上，否则标题与按钮是空白。</summary>
        private void EnsureFonts()
        {
            TMP_FontAsset font = UiFactory.Font;
            foreach (TextMeshProUGUI text in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.font != font)
                {
                    text.font = font;
                }
            }
        }

        private static PilePanel LoadPrefab()
        {
            if (s_Prefab == null)
            {
                s_Prefab = Resources.Load<PilePanel>(PrefabResourcePath);
            }

            return s_Prefab;
        }

        /// <summary>按规范结构搭建面板框。编辑器预制体生成器与运行时兜底共用。</summary>
        public void BuildDefaultHierarchy()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var backdrop = GetComponent<Image>();
            if (backdrop == null)
            {
                backdrop = gameObject.AddComponent<Image>();
            }

            backdrop.color = new Color(0f, 0f, 0f, 0.78f);
            backdrop.raycastTarget = true;

            m_Panel = UiFactory.CreateBox(root, "Panel", new Color(0.08f, 0.09f, 0.12f),
                new Vector2(PanelWidth, PanelHeight), new Vector2(0.5f, 0.5f), Vector2.zero);

            m_Title = UiFactory.CreateLabel(m_Panel, "Title", string.Empty, 34, Color.white,
                new Vector2(900f, 46f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, PanelHeight * 0.5f - 52f));

            m_CloseButton = UiFactory.CreateButton(m_Panel, "Close", "关闭",
                new Color(0.28f, 0.30f, 0.36f), new Vector2(120f, 54f), new Vector2(0.5f, 0.5f),
                new Vector2(PanelWidth * 0.5f - 90f, PanelHeight * 0.5f - 52f), Color.white);

            m_Empty = UiFactory.CreateLabel(m_Panel, "Empty", "（空）", 28,
                new Color(0.6f, 0.62f, 0.68f), new Vector2(400f, 50f), new Vector2(0.5f, 0.5f),
                Vector2.zero);
            m_Empty.gameObject.SetActive(false);
        }

        private void WireButtons()
        {
            var backdropButton = gameObject.GetComponent<Button>();
            if (backdropButton == null)
            {
                backdropButton = gameObject.AddComponent<Button>();
                backdropButton.targetGraphic = GetComponent<Image>();
                backdropButton.transition = Selectable.Transition.None;
            }

            backdropButton.onClick.RemoveAllListeners();
            backdropButton.onClick.AddListener(Hide);

            if (m_CloseButton != null)
            {
                m_CloseButton.onClick.RemoveAllListeners();
                m_CloseButton.onClick.AddListener(Hide);
            }
        }

        public void Show(string title, IReadOnlyList<CardInstance> cards, bool hideOrder)
        {
            ClearDisplayed();

            var order = new List<CardInstance>(cards);
            if (hideOrder)
            {
                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }
            }

            m_Title.text = $"{title}（{cards.Count} 张）";

            if (order.Count == 0)
            {
                m_Empty.gameObject.SetActive(true);
            }
            else
            {
                m_Empty.gameObject.SetActive(false);
                BuildCardGrid(order);
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            ClearDisplayed();
        }

        private void BuildCardGrid(List<CardInstance> cards)
        {
            int rows = Mathf.CeilToInt(cards.Count / (float)Columns);
            float startY = (rows - 1) * RowStep * 0.5f - 34f;

            for (int row = 0; row < rows; row++)
            {
                int rowStart = row * Columns;
                int rowCount = Mathf.Min(Columns, cards.Count - rowStart);
                float startX = -(rowCount - 1) * ColumnStep * 0.5f;

                for (int column = 0; column < rowCount; column++)
                {
                    CardInstance card = cards[rowStart + column];
                    CardView view = CardView.Create(m_Panel, card, DisplayScale);

                    view.SetHome(new Vector2(startX + column * ColumnStep,
                        startY - row * RowStep));
                    // 不能出牌，但仍要能右键/悬停查看背面——查看是卡牌的通用能力
                    view.Interactable = false;

                    m_Displayed.Add(view.gameObject);
                }
            }
        }

        private void ClearDisplayed()
        {
            foreach (GameObject go in m_Displayed)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }

            m_Displayed.Clear();
        }
    }
}
