using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using TurnCard.Game;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using TurnCard.Game.Run;
using TurnCard.Game.UI;
using TurnCard.Game.UI.Fx;
using UnityGameFramework.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Window.BattleForm
{
    /// <summary>
    /// 战斗界面。静态部分来自 BattleForm.prefab（UIBind 绑定），
    /// 手牌、敌人、箭头与牌堆面板在运行时创建。
    /// </summary>
    public partial class BattleForm
    {
        private const float FlipDuration = 0.35f;
        /// <summary>手牌之间想要的间隙（实际会按可用宽度自适应压缩）。</summary>
        private const float HandGap = 14f;

        /// <summary>手牌区左右各留出的余量（给两侧牌堆）。</summary>
        private const float HandSidePadding = 80f;

        private BattleState m_State;
        private BattleContent m_Content;
        private RectTransform m_FormRect;

        /// <summary>调试台指定的下一场阵容（null = 按池子随机抽）。</summary>
        public static TurnCard.Game.Data.EnemyDefinition[] DebugPendingLineup;

        /// <summary>打开界面后先进选关（「开始新游戏」用）。</summary>
        public static bool PendingRouteSelect;

        /// <summary>这一场要用哪个池子（普通 / 精英 / 首领），null 表示普通。</summary>
        private TurnCard.Game.Data.EnemyPool? m_PendingPool;
        private RectTransform m_FxLayer;
        private RectTransform m_HandRoot;
        private RectTransform m_ArrowLayer;
        private BezierArrow m_Arrow;
        private readonly List<EnemyCardView> m_EnemyCardViews = new List<EnemyCardView>();
        private RectTransform m_EnemyArea;
        private EnemyInstance m_LastHitEnemy;
        private bool m_HandDirty;

        private BattleStageView m_Stage;

        // 玩家 HUD：血条（与敌人卡同样的 RectMask2D 裁剪）+ 独立的 buff 区
        private RectTransform m_HpClip;
        private TextMeshProUGUI m_HpText;
        private RectTransform m_BuffArea;

        // 护甲徽标：直接复用「防御意图」的图标
        private GameObject m_ArmorRoot;
        private Image m_ArmorIcon;
        private TextMeshProUGUI m_ArmorText;
        private static IntentIconSet s_IconSet;

        private CardGridScreen m_RewardScreen;
        private bool m_RewardPicked;
        private bool m_VictoryHandled;
        private int m_BattleRound;

        /// <summary>当前手牌是否正显示成 0 费（贪婪），用于避免每帧重刷。</summary>
        private bool m_CostFreeShown;

        /// <summary>开幕/关幕协程。</summary>
        private Coroutine m_BattleIntro;

        /// <summary>数值预览需要重刷（同一帧多次事件只刷一次）。</summary>
        private bool m_PreviewDirty;

        /// <summary>拖牌时指着的目标：手牌上的伤害预览按这个敌人的易伤重算。</summary>
        private EnemyInstance m_HoverTarget;

        /// <summary>选择模式：等玩家选一张手牌并点确认。</summary>
        /// <summary>正在播放的翻面动画数量（同牌串行、跨牌并行）。</summary>
        private int m_FlipAnimating;

        private bool m_ChoiceMode;
        private Button m_BtnConfirmChoice;
        private CardView m_ChoiceView;

        private CardListViewScreen m_ListView;
        private AudioSource m_Audio;
        private readonly List<CardView> m_HandViews = new List<CardView>();
        private bool m_Busy;

        protected override void OnCreate()
        {
            UnityEngine.Debug.Log("[战斗] C OnCreate 开始");
            InitComponents();
            UnityEngine.Debug.Log("[战斗] D InitComponents 完成");

            m_FormRect = RectTransform;

            m_FxLayer = UiFactory.CreateRect(m_FormRect, "FxLayer");
            m_FxLayer.anchorMin = Vector2.zero;
            m_FxLayer.anchorMax = Vector2.one;
            m_FxLayer.offsetMin = Vector2.zero;
            m_FxLayer.offsetMax = Vector2.zero;
            m_HandRoot = goHand != null ? (RectTransform)goHand.transform : m_FormRect;

            // 舞台区：这个窗口的绑定是 UIBind 索引式的，手工改预制体会打乱绑定，
            // 所以舞台区在运行时搭（和 EnemyArea 的兜底逻辑同一条路子），
            // 宝箱/路线选项本身仍然是预制体。
            EnsureStage();
            ResolvePlayerHud();

            EnsureFonts();
            UnityEngine.Debug.Log("[战斗] E 字体完成");
            BuildRuntime();
            UnityEngine.Debug.Log("[战斗] F BuildRuntime 完成");
        }

        /// <summary>
        /// 玩家 HUD 的引用：血条裁剪窗口、血量文字、buff 区容器。
        /// 预制体里的层级：goPlayerHud / goHpBar / goHpClip / imgHpFill，goBuffArea 是血条下方的独立子节点。
        /// </summary>
        private void ResolvePlayerHud()
        {
            Transform hud = m_FormRect.Find("goPlayerHud");
            if (hud == null)
            {
                return;
            }

            m_HpClip = hud.Find("goHpBar/goHpClip") as RectTransform;
            m_HpText = hud.Find("goHpBar/txtHp") == null
                ? null
                : hud.Find("goHpBar/txtHp").GetComponent<TextMeshProUGUI>();
            m_BuffArea = hud.Find("goBuffArea") as RectTransform;

            Transform armor = hud.Find("goArmor");
            if (armor == null)
            {
                Debug.LogError("[战斗] goPlayerHud 下缺少 goArmor（护甲徽标），" +
                               "请在 BattleFormWindow.prefab 里补上");
            }

            if (armor != null)
            {
                m_ArmorRoot = armor.gameObject;
                var icon = armor.Find("imgArmorIcon");
                m_ArmorIcon = icon == null ? null : icon.GetComponent<Image>();
                var value = armor.Find("txtArmor") != null
                    ? armor.Find("txtArmor").GetComponent<TextMeshProUGUI>()
                    : null;
                m_ArmorText = value;
            }

            if (s_IconSet == null)
            {
                s_IconSet = Resources.Load<IntentIconSet>("Config/IntentIconSet");
            }

            if (m_ArmorIcon != null && s_IconSet != null)
            {
                // 护甲就用「防御意图」那张图标
                m_ArmorIcon.sprite = s_IconSet.Get(EnemyIntentType.Defend);
                m_ArmorIcon.preserveAspect = true;
            }

            if (m_ArmorRoot != null)
            {
                m_ArmorRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 搭舞台区：与 EnemyArea 同区域，里面放「宝箱」和「路线选项」两个容器。
        /// 只在运行时建一次；敌人区仍然由 <see cref="BuildEnemyArea"/> 管理。
        /// </summary>
        private void EnsureStage()
        {
            if (m_Stage != null)
            {
                return;
            }

            m_Stage = GetComponentInChildren<BattleStageView>(true);
            if (m_Stage != null)
            {
                return;
            }

            // 以 EnemyArea 的矩形为准（没有就以窗体中心上方为基准）
            RectTransform anchorRect = m_EnemyArea;
            if (anchorRect == null)
            {
                Transform authored = m_FormRect.Find("EnemyArea");
                anchorRect = authored as RectTransform;
            }

            var stageGo = new GameObject("StageArea", typeof(RectTransform), typeof(BattleStageView));
            var stageRect = (RectTransform)stageGo.transform;
            stageRect.SetParent(m_FormRect, false);
            stageRect.anchorMin = new Vector2(0.5f, 1f);
            stageRect.anchorMax = new Vector2(0.5f, 1f);
            stageRect.pivot = new Vector2(0.5f, 1f);
            stageRect.anchoredPosition = new Vector2(0f, -203.98f);
            stageRect.sizeDelta = new Vector2(900f, 347f);

            if (anchorRect != null)
            {
                stageRect.anchorMin = anchorRect.anchorMin;
                stageRect.anchorMax = anchorRect.anchorMax;
                stageRect.pivot = anchorRect.pivot;
                stageRect.anchoredPosition = anchorRect.anchoredPosition;
                stageRect.sizeDelta = anchorRect.sizeDelta;
                stageRect.SetSiblingIndex(anchorRect.GetSiblingIndex());
            }

            RectTransform chest = CreateStageContainer(stageRect, "goChest");
            RectTransform routes = CreateStageContainer(stageRect, "goRoutes");

            m_Stage = stageGo.GetComponent<BattleStageView>();
            m_Stage.Initialize(chest, routes);
        }

        private static RectTransform CreateStageContainer(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.SetActive(false);
            return rect;
        }

        protected override void OnShow()
        {
            UnityEngine.Debug.Log("[战斗] G OnShow 开始");

            // 「开始新游戏」走选关：先只显示路线选项，选中战斗节点才开打
            if (PendingRouteSelect)
            {
                PendingRouteSelect = false;
                EnterRouteSelect();
                return;
            }

            StartBattle();
        }

        private void EnsureFonts()
        {
            UiFactory.EnsureFont(transform);
        }

        private void BuildRuntime()
        {
            var canvas = GetComponentInParent<Canvas>();


            m_ArrowLayer = new GameObject("ArrowLayer", typeof(RectTransform))
                .GetComponent<RectTransform>();
            m_ArrowLayer.SetParent(m_FormRect, false);
            m_ArrowLayer.anchorMin = Vector2.zero;
            m_ArrowLayer.anchorMax = Vector2.one;
            m_ArrowLayer.offsetMin = Vector2.zero;
            m_ArrowLayer.offsetMax = Vector2.zero;

            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(BezierArrow));
            arrowGo.transform.SetParent(m_ArrowLayer, false);
            var arrowRect = (RectTransform)arrowGo.transform;
            arrowRect.anchorMin = Vector2.zero;
            arrowRect.anchorMax = Vector2.one;
            arrowRect.offsetMin = Vector2.zero;
            arrowRect.offsetMax = Vector2.zero;
            m_Arrow = arrowGo.GetComponent<BezierArrow>();
            m_Arrow.color = new Color(1f, 0.85f, 0.35f, 0.95f);
            arrowGo.SetActive(false);



            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.playOnAwake = false;

            BuildChoiceButton();

            btnEndTurn.onClick.AddListener(OnEndTurnClicked);
            btnDrawPile.onClick.AddListener(OpenDrawPile);
            btnDiscardPile.onClick.AddListener(OpenDiscardPile);
        }

        /// <summary>
        /// 按池子挑一个阵容，返回按 order 排好的敌人定义列表。
        /// 找不到阵容时退回该池子的单个敌人，保证能进战斗。
        /// </summary>
        private TurnCard.Game.Data.EnemyDefinition[] ResolveEncounter(TurnCard.Game.Data.EnemyPool pool)
        {
            var groups = new Dictionary<int, List<TurnCard.Game.Data.EncounterEntry>>();
            if (m_Content.Encounters != null)
            {
                foreach (var entry in m_Content.Encounters)
                {
                    if (entry == null || entry.Pool != pool)
                    {
                        continue;
                    }

                    if (!groups.TryGetValue(entry.Group, out var list))
                    {
                        list = new List<TurnCard.Game.Data.EncounterEntry>();
                        groups[entry.Group] = list;
                    }

                    list.Add(entry);
                }
            }

            if (groups.Count > 0)
            {
                var keys = new List<int>(groups.Keys);
                int picked = keys[UnityEngine.Random.Range(0, keys.Count)];
                var members = groups[picked];
                members.Sort((a, b) => a.Order.CompareTo(b.Order));

                var result = new List<TurnCard.Game.Data.EnemyDefinition>();
                foreach (var member in members)
                {
                    foreach (var enemy in m_Content.Enemies)
                    {
                        if (enemy != null && enemy.Id == member.EnemyId)
                        {
                            result.Add(enemy);
                            break;
                        }
                    }
                }

                if (result.Count > 0)
                {
                    return result.ToArray();
                }
            }

            return new[] { PickEnemy(pool) };
        }

        /// <summary>
        /// 敌人区：横向布局组，多个怪物自动排开、整体居中。
        /// </summary>
        private const float EnemySpacing = 36f;

        /// <summary>
        /// 敌人卡尺寸：48×64 网格 ×4（见 <c>Docs/21-敌人卡面.md</c>）。
        /// 预制体已经按这个尺寸出图，代码里不再缩放。
        /// </summary>
        private const float EnemyCardWidth = 192f;

        private const float EnemyCardHeight = 256f;

        /// <summary>
        /// 敌人区：优先使用预制体里的 EnemyArea 节点（位置与站位都可以手动编辑）。
        ///
        /// 预制体结构：
        /// <code>
        /// EnemyArea          锚点 / 位置 / 尺寸自己调
        /// ├─ Slot1          第 1 个敌人的站位（空 RectTransform，不显示）
        /// ├─ Slot2
        /// └─ Slot3
        /// </code>
        /// 代码只把敌人卡塞进对应 Slot 的坐标，不改容器尺寸与站位；
        /// 没有 Slot 时退回「按数量自动横排居中」的旧行为。
        /// </summary>
        private void BuildEnemyArea()
        {
            if (m_EnemyArea == null)
            {
                Transform authored = m_FormRect.Find("EnemyArea");
                if (authored != null)
                {
                    m_EnemyArea = (RectTransform)authored;
                }
                else
                {
                    // 预制体没有 EnemyArea 时的运行时兜底
                    var go = new GameObject("EnemyArea", typeof(RectTransform));
                    m_EnemyArea = (RectTransform)go.transform;
                    m_EnemyArea.SetParent(m_FormRect, false);
                    m_EnemyArea.anchorMin = new Vector2(0.5f, 1f);
                    m_EnemyArea.anchorMax = new Vector2(0.5f, 1f);
                    m_EnemyArea.pivot = new Vector2(0.5f, 1f);
                    m_EnemyArea.anchoredPosition = new Vector2(0f, -96f);
                }
            }

            // 只清敌人卡，保留 Slot 标记（倒序删：Destroy 帧末才生效）
            for (int c = m_EnemyArea.childCount - 1; c >= 0; c--)
            {
                Transform child = m_EnemyArea.GetChild(c);
                if (child.GetComponent<EnemyCardView>() != null)
                {
                    Destroy(child.gameObject);
                }
            }

            m_EnemyCardViews.Clear();

            int count = m_State.Enemies.Count;

            // 预制体配了 Slot 就按 Slot 摆，否则按数量自动横排
            bool hasSlots = m_EnemyArea.Find("Slot1") != null;

            float cardW = EnemyCardWidth;
            float width = count * cardW + Mathf.Max(0, count - 1) * EnemySpacing;

            if (!hasSlots)
            {
                m_EnemyArea.sizeDelta = new Vector2(width, EnemyCardHeight);
            }

            float left = -width * 0.5f + cardW * 0.5f;

            for (int i = 0; i < count; i++)
            {
                EnemyCardView view = EnemyCardView.Create(m_EnemyArea, m_State.Enemies[i]);
                if (view == null)
                {
                    continue;
                }

                var rect = view.Rect;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                // 横向始终按「卡宽 + 间距」自动居中排开：卡牌尺寸是固定的（192×256），
                // 让站位跟着卡宽走才不会因为改尺寸而错位。
                // Slot 只贡献纵向位置，美术/策划仍然可以在预制体里调整整排的高度。
                var slot = hasSlots ? m_EnemyArea.Find("Slot" + (i + 1)) as RectTransform : null;
                float y = slot != null ? slot.anchoredPosition.y : 0f;
                rect.anchoredPosition = new Vector2(left + i * (cardW + EnemySpacing), y);

                m_EnemyCardViews.Add(view);
            }

            // Slot 只是站位标记，不显示
            for (int c = 0; c < m_EnemyArea.childCount; c++)
            {
                Transform child = m_EnemyArea.GetChild(c);
                if (child.name.StartsWith("Slot"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // 敌人区要在背景之上、手牌之下
            m_EnemyArea.transform.SetSiblingIndex(1);
        }

        /// <summary>
        /// 从指定池子里随机抽一个敌人。
        /// 池子为空时退回全部敌人，避免配置漏项直接进不了战斗。
        /// </summary>
        private TurnCard.Game.Data.EnemyDefinition PickEnemy(TurnCard.Game.Data.EnemyPool pool)
        {
            var poolList = new List<TurnCard.Game.Data.EnemyDefinition>();
            foreach (var e in m_Content.Enemies)
            {
                // 占位怪（HP 还是 1 的半成品）不进随机池
                if (e != null && e.Pool == pool && !e.Disabled)
                {
                    poolList.Add(e);
                }
            }

            if (poolList.Count == 0)
            {
                foreach (var e in m_Content.Enemies)
                {
                    if (e != null && !e.Disabled)
                    {
                        poolList.Add(e);
                    }
                }
            }

            if (poolList.Count == 0)
            {
                return m_Content.Enemies.Length > 0 ? m_Content.Enemies[0] : null;
            }

            return poolList[UnityEngine.Random.Range(0, poolList.Count)];
        }

        /// <summary>
        /// 开一场战斗的完整时序：
        /// 清场 → 关幕（倒放）→ 停在关幕帧 → 初始化怪物 → 战斗初始化 → 开幕。
        /// </summary>
        private void StartBattle()
        {
            m_Content = Resources.Load<BattleContent>("BattleContent");
            if (m_Content == null || m_Content.Cards.Length == 0)
            {
                txtHint.text = "缺少 BattleContent，请先生成数据表";
                return;
            }

            // 1) 清场：上一场的手牌/敌人卡/结算文字/buff 条/舞台全部清掉
            ResetBattleState();

            if (m_BattleIntro != null)
            {
                StopCoroutine(m_BattleIntro);
            }

            m_BattleIntro = StartCoroutine(BattleIntroRoutine());
        }

        private System.Collections.IEnumerator BattleIntroRoutine()
        {
            // 1) 战斗开始前：播放关幕动画（倒放），并停在关幕帧。
            //    本来就是关着的（第一场）就保持关幕，不播动画，免得闪一下。
            bool closing = CurtainView.CloseAnimatedAll();
            if (closing)
            {
                float close = CurtainView.CloseDuration;
                if (close > 0f)
                {
                    yield return new WaitForSecondsRealtime(close);
                }
            }

            // 2) 初始化怪物 + 战斗初始化
            BuildBattle();

            // 3) 保持在关幕帧，让「关 → 开」看得出来
            float hold = FlowSettings.Current.CurtainHold;
            if (hold > 0f)
            {
                yield return new WaitForSecondsRealtime(hold);
            }

            // 4) 开幕
            CurtainView.PlayAll();
            m_BattleIntro = null;
        }

        private void BuildBattle()
        {
            UnityEngine.Debug.Log("[战斗] 1 创建 BattleState");
            m_State = new BattleState();
            HookState(m_State);

            // 玩家 buff 图标条：挂在血条下方的 buff 区里，订阅容器事件自动刷新
            Transform buffRoot = m_BuffArea != null ? m_BuffArea : (txtStatus != null ? txtStatus.transform : null);
            if (buffRoot != null)
            {
                BuffIconStrip.Attach((RectTransform)buffRoot, m_State.PlayerBuffs,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero);
            }

            // 有全局进度时用牌组，没有（直接运行本场景调试）就用全部卡牌
            TurnCard.Game.Data.CardDefinition[] deck =
                TurnCard.Game.Run.RunState.Current.ResolveDeck(m_Content);
            if (deck == null || deck.Length == 0)
            {
                deck = m_Content.Cards;
            }

            // 阵容来自配置表：同一 group 的多行按 order 从左到右排开
            UnityEngine.Debug.Log("[战斗] 2 解析阵容");
            TurnCard.Game.Data.EnemyPool pool = m_PendingPool ?? TurnCard.Game.Data.EnemyPool.Normal;
            m_PendingPool = null;
            var lineup = DebugPendingLineup ?? ResolveEncounter(pool);
            DebugPendingLineup = null;
            UnityEngine.Debug.Log("[战斗] 3 阵容人数=" + lineup.Length);

            m_State.Setup(deck, lineup);
            UnityEngine.Debug.Log("[战斗] 4 Setup 完成");

            // 敌人的 buff 变化同样会影响意图数值的显示
            foreach (EnemyInstance enemy in m_State.Enemies)
            {
                if (enemy != null)
                {
                    enemy.Buffs.Changed += _ => MarkPreviewDirty();
                }
            }

            BuildEnemyArea();
            UnityEngine.Debug.Log("[战斗] 5 敌人区完成");

            m_HandRoot.SetAsLastSibling();
            m_ArrowLayer.SetAsLastSibling();

            UnityEngine.Debug.Log("[战斗] 6 开始玩家回合");
            m_State.StartPlayerTurn();
            UnityEngine.Debug.Log("[战斗] 7 抽牌完成 手牌=" + m_State.Hand.Count);

            BuildHand();
            UnityEngine.Debug.Log("[战斗] 8 手牌视图完成");

            RefreshHud();
            UnityEngine.Debug.Log("[战斗] 9 刷新完成");

            // 手牌与敌人卡刚建好，先把数值预览刷一遍
            RefreshAllPreviews();
        }

        /// <summary>战斗状态退出时的清场：手牌、敌人卡、buff 条、结算文字、舞台全部复位。</summary>
        private void ResetBattleState()
        {
            ClearHand();

            if (m_EnemyArea != null)
            {
                for (int c = m_EnemyArea.childCount - 1; c >= 0; c--)
                {
                    Transform child = m_EnemyArea.GetChild(c);
                    if (child.GetComponent<EnemyCardView>() != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }

            m_EnemyCardViews.Clear();
            m_LastHitEnemy = null;

            if (m_BuffArea != null)
            {
                for (int i = m_BuffArea.childCount - 1; i >= 0; i--)
                {
                    Transform child = m_BuffArea.GetChild(i);
                    if (child.name == "BuffStrip")
                    {
                        Destroy(child.gameObject);
                    }
                }
            }

            if (txtResult != null)
            {
                txtResult.text = string.Empty;
            }

            if (txtHint != null)
            {
                txtHint.text = string.Empty;
            }

            if (m_Stage != null)
            {
                m_Stage.ClearRoutes();
                m_Stage.HideChest();
                m_Stage.ShowBattle();
            }

            if (btnEndTurn != null)
            {
                btnEndTurn.interactable = true;
            }

            m_State = null;
            m_Busy = false;
            m_HandDirty = false;
            m_ResultShown = false;
            m_RewardPicked = false;
            m_VictoryHandled = false;
            m_CostFreeShown = false;
        }

        private void Update()
        {
            if (!m_PreviewDirty || m_State == null)
            {
                return;
            }

            m_PreviewDirty = false;
            RefreshAllPreviews();
        }

        /// <summary>
        /// 把所有「受 buff 影响的数值」刷成真实值：
        /// 手牌上的 {damage}/{armor}，以及敌人意图的伤害数字。
        /// 拖牌指着某个目标时，手牌伤害按该目标的易伤重算。
        /// </summary>
        public void RefreshAllPreviews()
        {
            if (m_State == null)
            {
                return;
            }

            for (int i = m_HandViews.Count - 1; i >= 0; i--)
            {
                CardView view = m_HandViews[i];
                if (view == null)
                {
                    m_HandViews.RemoveAt(i);
                    continue;
                }

                CardFace face = view.Card.Face;
                BattleState.ValuePreview damage = m_State.PreviewPlayerDamage(face, m_HoverTarget);
                BattleState.ValuePreview armor = m_State.PreviewPlayerArmor(face);
                view.SetValuePreview(damage.Value, armor.Value, damage.Modified || armor.Modified);
            }

            foreach (EnemyCardView view in m_EnemyCardViews)
            {
                if (view == null || view.Enemy == null)
                {
                    continue;
                }

                BattleState.ValuePreview attack = m_State.PreviewEnemyAttack(view.Enemy);
                view.SetIntentPreview(attack.Value, attack.Modified);
            }
        }

        private void MarkPreviewDirty()
        {
            m_PreviewDirty = true;
        }

        /// <summary>
        /// 订阅战斗状态事件。UI 只通过事件被动刷新，
        /// 不主动去「猜」状态什么时候变了。
        /// </summary>
        private void HookState(BattleState state)
        {
            // 任何 buff 变化都可能改变显示数值 → 标记数值预览需要重刷
            state.PlayerBuffs.Changed += _ => MarkPreviewDirty();

            state.DamageApplied += (enemy, amount) =>
            {
                m_LastHitEnemy = enemy;
                PlayHitFeedback(amount);

                // 伤害一落地就把**所有**血条刷一遍：
                // 单刷新被打的那张，在连击/瞬发/翻面继承等情况下会和实际血量差一拍。
                RefreshAllHpBars();
                MarkPreviewDirty();
            };
            state.PlayerHpChanged += _ => { RefreshHud(); MarkPreviewDirty(); };
            state.PlayerArmorChanged += _ => { RefreshHud(); MarkPreviewDirty(); };
            state.EnergyChanged += _ => { RefreshHud(); MarkPreviewDirty(); };

            // 抽牌只标脏，帧末统一重建一次。
            // 逐张重建会让手牌视图反复销毁/创建（抽 5 张要重建 5 次），
            // 销毁时打断正在跑的 tween，既刷警告也容易卡住。
            state.CardDrawn += _ => m_HandDirty = true;

            // 弃牌：不再整手重建，而是把被弃的那张飞向弃牌堆再销毁，
            // 否则视图会在动画开始前就被重建掉，看起来像「瞬间消失」。
            state.CardDiscarded += PlayDiscardAnimation;
            state.CardDiscarded += _ => MarkPreviewDirty();
            state.TurnStarted += _ =>
            {
                BuildHand();
                MarkPreviewDirty();
            };

            state.EnemyFlipped += enemy =>
            {
                EnemyCardView flipView = ViewFor(enemy);
                if (flipView != null)
                {
                    flipView.PlayFlip(BattleFxSettings.Current.FlipDuration, null);
                }
            };

            state.EnemyDied += OnEnemyDied;

            state.BattleEnded += victory =>
            {
                if (victory)
                {
                    // 胜利不再弹结果面板，交给舞台区走「宝箱 → 选卡 → 路线」
                    // （BeginVictoryFlow 内部自己做了去重）
                    BeginVictoryFlow();
                    return;
                }

                if (!m_ResultShown)
                {
                    m_ResultShown = true;
                    StartCoroutine(ShowResultDelayed(false));
                }
            };
        }

        private IEnumerator ShowResultDelayed(bool victory)
        {
            yield return new WaitForSeconds(0.6f);
            ShowResult(victory);
        }

        private bool m_ResultShown;

        private void LateUpdate()
        {
            if (!m_HandDirty)
            {
                return;
            }

            m_HandDirty = false;
            if (m_State != null)
            {
                BuildHand();
            }
        }

        /// <summary>
        /// 让视图和手牌数据保持一致：
        /// 只**补建新抽到的牌**、移除已离手的牌，然后把所有牌平滑推到新位置。
        /// 旧实现是整组销毁重建，所以手牌一变就会看到一轮「全部飞入」，没有位移过渡。
        /// </summary>
        private void BuildHand()
        {
            IReadOnlyList<CardInstance> hand = m_State.Hand;

            // 1) 移除已经不在手牌里的视图（打出/弃牌会自己播飞出动画并先摘掉自己）
            for (int i = m_HandViews.Count - 1; i >= 0; i--)
            {
                CardView view = m_HandViews[i];
                if (view == null)
                {
                    m_HandViews.RemoveAt(i);
                    continue;
                }

                bool still = false;
                for (int k = 0; k < hand.Count; k++)
                {
                    if (ReferenceEquals(hand[k], view.Card))
                    {
                        still = true;
                        break;
                    }
                }

                if (!still)
                {
                    Unsubscribe(view);
                    m_HandViews.RemoveAt(i);
                    Destroy(view.gameObject);
                }
            }

            // 2) 按手牌顺序整理视图，缺的补建（新牌从抽牌堆飞入）
            var ordered = new List<CardView>(hand.Count);
            var fresh = new HashSet<CardView>();
            for (int i = 0; i < hand.Count; i++)
            {
                CardView view = FindView(hand[i]);
                if (view == null)
                {
                    view = CardView.Create(m_HandRoot, hand[i], 1f);
                    view.Clicked += OnCardClicked;
                    view.DragMoved += OnCardDragMoved;
                    view.DragFinished += OnCardDragFinished;
                    fresh.Add(view);
                }

                ordered.Add(view);
            }

            m_HandViews.Clear();
            m_HandViews.AddRange(ordered);

            // 3) 统一布局：新牌飞入，老牌平滑位移
            ApplyHandLayout(fresh);

            RefreshInteractivity();
        }

        /// <summary>按当前手牌数量算位置并过渡过去（新牌用发牌动画，老牌用补间）。</summary>
        private void ApplyHandLayout(HashSet<CardView> fresh = null)
        {
            int count = m_HandViews.Count;
            if (count == 0)
            {
                return;
            }

            // 间距自适应：手牌多了就压缩间隙，避免压到两侧牌堆或互相遮挡
            HandLayoutSettings cfg = HandLayoutSettings.Current;
            float maxWidth = Mathf.Max(CardView.CardWidth * 2f,
                m_HandRoot.rect.width - HandSidePadding * 2f);

            float spacing = CardView.CardWidth + HandGap;
            if (count > 1)
            {
                float fit = (maxWidth - CardView.CardWidth) / (count - 1);
                spacing = Mathf.Min(spacing, Mathf.Max(CardView.CardWidth * cfg.MinSpacingRatio, fit));
            }

            float startX = -(count - 1) * spacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                CardView view = m_HandViews[i];
                if (view == null)
                {
                    continue;
                }

                // 取整，避免像素级错位（像素风尤其明显）
                var target = new Vector2(Mathf.Round(startX + i * spacing), 0f);

                view.SetHomeSilent(target);
                view.transform.SetSiblingIndex(i);

                if (fresh != null && fresh.Contains(view))
                {
                    // 新牌：从抽牌堆位置错峰飞入
                    view.PlayDeal(HandLocalOfDrawPile(), 0.3f, i * 0.045f);
                }
                else
                {
                    // 老牌：往新位置滑过去，稍微错峰一点点避免整排一起动
                    view.TweenHome(cfg.LayoutDuration, i * cfg.LayoutStagger);
                }
            }
        }

        private void Unsubscribe(CardView view)
        {
            view.Clicked -= OnCardClicked;
            view.DragMoved -= OnCardDragMoved;
            view.DragFinished -= OnCardDragFinished;
        }

        private void ClearHand()
        {
            foreach (CardView view in m_HandViews)
            {
                if (view == null)
                {
                    continue;
                }

                view.Clicked -= OnCardClicked;
                view.DragMoved -= OnCardDragMoved;
                view.DragFinished -= OnCardDragFinished;
                Destroy(view.gameObject);
            }

            m_HandViews.Clear();
        }

        /// <summary>
        /// 回合结束时手牌飞向弃牌堆再销毁（规则：回合结束手牌全部弃掉，Q2）。
        /// 注意这只在回合结束发生，出牌本身不会移除卡牌。
        /// </summary>
        /// <summary>
        /// 一张牌被弃掉时的表现：从当前位置飞向弃牌堆并缩小淡出。
        /// 由 <c>CardDiscarded</c> 事件驱动，出牌效果弃牌与回合结束弃牌共用。
        /// </summary>
        private void PlayDiscardAnimation(CardInstance card)
        {
            CardView view = FindView(card);
            if (view == null)
            {
                return;
            }

            m_HandViews.Remove(view);
            view.Interactable = false;

            // 牌离开了手牌 → 剩下的牌要让位，帧末统一重排（带补间）
            m_HandDirty = true;

            Vector2 target = btnDiscardPile != null
                ? HandLocalOf(btnDiscardPile.transform.position)
                : new Vector2(820f, -130f);

            view.PlayOut(target, null, 0.22f);
        }

        private void ScatterHandToDiscard()
        {
            if (m_HandViews.Count == 0)
            {
                return;
            }

            var views = new List<CardView>(m_HandViews);
            m_HandViews.Clear();

            Vector2 discardLocal = btnDiscardPile != null
                ? HandLocalOf(btnDiscardPile.transform.position)
                : new Vector2(820f, -130f);

            foreach (CardView view in views)
            {
                if (view == null)
                {
                    continue;
                }

                view.Interactable = false;
                view.PlayOut(discardLocal, null, 0.22f);
            }
        }

        private void OnCardClicked(CardView view)
        {
            if (m_ChoiceMode)
            {
                SelectCardForChoice(view);
                return;
            }

            if (m_Busy)
            {
                return;
            }

            StartCoroutine(PlayCardRoutine(view, null));
        }

        /// <summary>选择模式：点一张手牌把它抬起来（还没确认）。</summary>
        private void SelectCardForChoice(CardView view)
        {
            if (view == null || view.Card == null || m_State == null)
            {
                return;
            }

            if (!m_State.SelectForChoice(view.Card))
            {
                return;
            }

            if (m_ChoiceView != null && !ReferenceEquals(m_ChoiceView, view))
            {
                m_ChoiceView.SetChoiceSelected(false);
            }

            m_ChoiceView = view;
            view.SetChoiceSelected(true);

            if (m_BtnConfirmChoice != null)
            {
                m_BtnConfirmChoice.interactable = true;
            }

            TurnCard.Game.Audio.Sfx.Flip(view.Card.Side == CardFaceSide.Back);
        }

        /// <summary>点勾：确认选择，然后播放翻面与后续结算。</summary>
        private void OnConfirmChoiceClicked()
        {
            if (!m_ChoiceMode || m_State == null)
            {
                return;
            }

            if (!m_State.ConfirmChoice())
            {
                return;
            }

            StartCoroutine(FinishChoiceRoutine());
        }

        private IEnumerator FinishChoiceRoutine()
        {
            ExitChoiceMode();

            m_Busy = true;
            RefreshHud();

            CommitAndDispatchFlips();
            yield return PlayDamageQueue(m_ChoiceView, m_State.FirstAliveEnemy(), default, HitOrigin.Play);
            yield return WaitForFlipAnimations();

            if (m_State.HasPendingInstants)
            {
                m_State.ResolvePendingInstants();
                RefreshEnemyCardViews();
                yield return PlayDamageQueue(m_ChoiceView, m_State.FirstAliveEnemy(), default, HitOrigin.Instant);
            }

            m_ChoiceView = null;
            m_Busy = false;
            RefreshHud();
            BuildHand();
        }

        /// <summary>进入选择模式：弹出勾按钮、提示文案。</summary>
        private void EnterChoiceMode()
        {
            if (m_ChoiceMode)
            {
                return;
            }

            m_ChoiceMode = true;
            m_ChoiceView = null;

            if (m_BtnConfirmChoice != null)
            {
                m_BtnConfirmChoice.gameObject.SetActive(true);
                m_BtnConfirmChoice.interactable = false;
            }

            ShowHint("选择一张手牌翻面，然后点勾确认");
            RefreshInteractivity();
        }

        private void ExitChoiceMode()
        {
            m_ChoiceMode = false;

            if (m_BtnConfirmChoice != null)
            {
                m_BtnConfirmChoice.gameObject.SetActive(false);
            }

            if (m_ChoiceView != null)
            {
                m_ChoiceView.SetChoiceSelected(false);
            }
        }

        /// <summary>勾按钮：右下角，样式跟「结束回合」一致。</summary>
        private void BuildChoiceButton()
        {
            RectTransform box = UiFactory.CreateBox(m_FormRect, "BtnConfirmChoice",
                new Color(0.28f, 0.55f, 0.35f, 1f), new Vector2(96f, 56f),
                new Vector2(1f, 0f), new Vector2(-160f, 150f));

            m_BtnConfirmChoice = box.gameObject.AddComponent<Button>();
            m_BtnConfirmChoice.targetGraphic = box.GetComponent<Image>();
            m_BtnConfirmChoice.onClick.AddListener(OnConfirmChoiceClicked);

            // √ 而不是 ✓：两份字体都没有 U+2713
            UiFactory.CreateLabel(box, "Confirm", "√", 34, Color.white,
                new Vector2(96f, 56f), new Vector2(0.5f, 0.5f), Vector2.zero);

            box.gameObject.SetActive(false);
        }

        private void OnCardDragMoved(CardView view, PointerEventData eventData)
        {
            if (view == null || m_State == null)
            {
                return;
            }

            EnemyCardView hovered = ViewUnder(eventData.position);
            foreach (EnemyCardView enemyView in m_EnemyCardViews)
            {
                if (enemyView != null)
                {
                    enemyView.SetHighlight(ReferenceEquals(enemyView, hovered));
                }
            }

            // 指着某个敌人时，手牌伤害按这个目标的易伤重算
            EnemyInstance hoveredEnemy = hovered != null && !hovered.Enemy.IsDead ? hovered.Enemy : null;
            if (!ReferenceEquals(hoveredEnemy, m_HoverTarget))
            {
                m_HoverTarget = hoveredEnemy;
                MarkPreviewDirty();
            }

            if (m_Busy)
            {
                HideArrow();
                return;
            }

            // 拖着牌就一直显示箭头：没悬停敌人时指向鼠标，悬停时吸附到那只敌人
            Vector2 to = hovered != null ? (Vector2)hovered.Rect.position : eventData.position;
            ShowArrow(((RectTransform)view.transform).position, to);
        }

        private void OnCardDragFinished(CardView view, PointerEventData eventData)
        {
            if (view == null || m_State == null)
            {
                return;
            }

            foreach (EnemyCardView enemyView in m_EnemyCardViews)
            {
                if (enemyView != null)
                {
                    enemyView.SetHighlight(false);
                }
            }

            HideArrow();

            // 松手后不再对着某个目标，手牌恢复成只算玩家自身 buff 的数值
            if (m_HoverTarget != null)
            {
                m_HoverTarget = null;
                MarkPreviewDirty();
            }

            EnemyCardView hovered = ViewUnder(eventData.position);
            if (hovered != null && !hovered.Enemy.IsDead && !m_Busy)
            {
                StartCoroutine(PlayCardRoutine(view, hovered.Enemy));
                return;
            }

            view.SetLifted(false);
            view.SnapHome();
        }

        /// <summary>按 CardInstance 找对应的手牌视图。</summary>
        private CardView FindView(CardInstance card)
        {
            foreach (CardView v in m_HandViews)
            {
                if (v != null && ReferenceEquals(v.Card, card))
                {
                    return v;
                }
            }

            return null;
        }

        /// <summary>
        /// 提交并播放翻面队列：翻面本身会触发瞬发，瞬发可能继续入队。
        /// 出牌和「选择确认」都走这里，保证规则一致。
        /// </summary>
        /// <summary>
        /// 提交翻面请求并把动画分发给各自的 CardView（同一张牌内部串行，不同牌并行）。
        /// 这里不等待动画结束，调用方用 <see cref="WaitForFlipAnimations"/> 等。
        /// </summary>
        private void CommitAndDispatchFlips()
        {
            BattleFxSettings fx = BattleFxSettings.Current;

            while (m_State.IsResolvingFlips)
            {
                BattleState.FlipStep? step = m_State.CommitNextFlip();
                if (step == null)
                {
                    continue;
                }

                CardView view = FindView(step.Value.Card);
                if (view == null)
                {
                    continue;   // 牌已经被弃掉，只结算逻辑
                }

                m_FlipAnimating++;
                view.PlayFlipTo(step.Value.To, fx.FlipDuration, null, () => m_FlipAnimating--);
                TurnCard.Game.Audio.Sfx.Flip(step.Value.To == CardFaceSide.Back);
            }
        }

        /// <summary>等所有翻面动画播完。</summary>
        private IEnumerator WaitForFlipAnimations()
        {
            while (m_FlipAnimating > 0)
            {
                yield return null;
            }
        }

        /// <summary>把排队的伤害逐发打出去，命中才扣血。</summary>
        /// <summary>取队首中指定来源的那条伤害；没有就返回 false。</summary>
        private bool TryPeekHit(HitOrigin origin, out BattleState.PendingHit hit)
        {
            foreach (BattleState.PendingHit pending in m_State.PendingHits)
            {
                if (pending.Origin == origin)
                {
                    hit = pending;
                    return true;
                }
            }

            hit = default;
            return false;
        }

        private IEnumerator PlayDamageQueue(CardView sourceView, EnemyInstance target,
            CardFace playedFace, HitOrigin origin)
        {
            if (sourceView == null || m_State == null)
            {
                yield break;
            }

            BattleFxSettings fx = BattleFxSettings.Current;
            Vector2 from = (Vector2)m_FxLayer.InverseTransformPoint(((RectTransform)sourceView.transform).position);

            while (m_State.IsResolving)
            {
                // 队列按来源分流：Play 与翻面动画并行，Instant 等动画播完再播
                if (!TryPeekHit(origin, out BattleState.PendingHit next))
                {
                    yield break;
                }

                int amount = next.Amount;
                bool cannon = IsCannon(playedFace, sourceView.Card.Face, amount);

                TurnCard.Game.Audio.Sfx.Attack(amount);

                bool hit = false;
                EnemyCardView hitTargetView = ViewFor(next.Enemy) ?? ViewFor(target);
                Vector2 to = hitTargetView != null
                    ? (Vector2)m_FxLayer.InverseTransformPoint(hitTargetView.Rect.position)
                    : (Vector2)m_FxLayer.InverseTransformPoint(m_FormRect.position);

                TurnCard.Game.UI.Fx.ProjectileView.Play(m_FxLayer, from, to, cannon, () =>
                {
                    TurnCard.Game.Audio.Sfx.Hit();
                    m_State.CommitNextHit();
                    hit = true;
                }, fx.FlightFor(cannon));

                yield return new WaitUntil(() => hit);

                if (m_State.IsBattleOver)
                {
                    m_State.ClearPendingHits();
                    break;
                }

                if (m_State.IsResolving)
                {
                    yield return new WaitForSeconds(fx.HitInterval + fx.ImpactHold);
                }
            }
        }

        private IEnumerator PlayCardRoutine(CardView view, EnemyInstance target)
        {
            // 打出前的这一面才是结算用的面（结算会把卡翻到另一面）
            CardFace playedFace = view.Card.Face;
            int damage = playedFace.Damage;

            PlayResult result = m_State.PlayCard(view.Card, target,
                out EnemyInstance flippedEnemy, out bool battleEnded);

            if (result != PlayResult.Ok)
            {
                ShowHint(DescribeFailure(result));
                view.SetLifted(false);
                view.SnapHome();
                yield break;
            }

            m_Busy = true;
            HideArrow();
            view.SetLifted(false);
            view.SnapHome();
            RefreshHud();

            // 顺序：
            //   ① 提交翻面并启动动画（同牌串行、跨牌并行）
            //   ② 立刻播「出牌自身」的伤害弹道 —— 与翻面动画同时进行
            //   ③ 等翻面动画结束
            //   ④ 执行翻面收集到的瞬发，再播瞬发弹道
            //   ⑤ 瞬发若又引发翻面（双面火炮），回到 ①
            m_State.BeginDeferDamage();
            try
            {
                int guard = 0;
                bool firstRound = true;
                while (true)
                {
                    guard++;
                    if (guard > 8)
                    {
                        m_State.ClearPendingHits();
                        m_State.ClearPendingInstants();
                        break;
                    }

                    CommitAndDispatchFlips();

                    if (firstRound)
                    {
                        firstRound = false;
                        yield return PlayDamageQueue(view, target, playedFace, HitOrigin.Play);
                    }

                    yield return WaitForFlipAnimations();

                    if (m_State.IsBattleOver || !m_State.HasPendingInstants)
                    {
                        break;
                    }

                    m_State.ResolvePendingInstants();

                    if (m_State.LastInstantFlipEnemy != null)
                    {
                        EnemyCardView flippedView = ViewFor(m_State.LastInstantFlipEnemy);
                        m_State.ClearLastInstantFlipEnemy();
                        if (flippedView != null)
                        {
                            flippedView.PlayFlip(FlipDuration, null);
                        }
                    }

                    RefreshEnemyCardViews();

                    yield return PlayDamageQueue(view, target, playedFace, HitOrigin.Instant);

                    if (!m_State.IsResolvingFlips)
                    {
                        break;
                    }
                }
            }
            finally
            {
                m_State.EndDeferDamage();
            }

            // 规则：使用后只翻面，卡牌留在手上（Q1、Q8）。
            // 这里不做任何移除，下一次可用背面继续出牌，直到能量耗尽。
            m_Busy = false;
            RefreshHud();

            if (m_State.HasPendingChoice)
            {
                EnterChoiceMode();
            }

            if (battleEnded)
            {
                BeginVictoryFlow();
            }
        }

        // ---------------------------------------------------------------- 胜利后的舞台流程
        // 胜利 → 宝箱（自动出现，等玩家点）→ 0.5s → 三选一 → 舞台区路线选项 → 同一界面重开下一场。
        // 不再弹「战斗胜利」结果面板，也不再切场景。

        private const int GoldReward = 25;

        /// <summary>胜利后进入奖励房：舞台区放宝箱。</summary>
        private void BeginVictoryFlow()
        {
            if (m_VictoryHandled)
            {
                return;
            }

            m_VictoryHandled = true;

            if (btnEndTurn != null)
            {
                btnEndTurn.interactable = false;
            }

            if (m_Stage == null)
            {
                // 预制体没挂舞台区时退回旧行为，避免卡死流程
                ShowResult(true);
                return;
            }

            m_RewardPicked = false;
            m_Stage.ShowChest(OnChestOpened);
        }

        /// <summary>宝箱被点开：等 FlowSettings 里的延迟再弹选卡界面。</summary>
        private void OnChestOpened()
        {
            float delay = FlowSettings.Current.ChestOpenDelay;
            DG.Tweening.DOVirtual.DelayedCall(Mathf.Max(0f, delay), OpenRewardSelection);
        }

        private void OpenRewardSelection()
        {
            TurnCard.Game.Run.RunState run = TurnCard.Game.Run.RunState.Current;
            run.AddGold(GoldReward);

            CardGridScreen screen = UIHost.Shared.Open<CardGridScreen>(CardGridScreen.ResourcePath);
            if (screen == null)
            {
                ShowRoutes();
                return;
            }

            m_RewardScreen = screen;
            m_RewardPicked = false;

            screen.SetHeader("战斗胜利",
                $"获得 {GoldReward} 金币    当前金币 {run.Gold}",
                "点击一张牌拿走；也可以跳过");

            List<CardDefinition> offer = PickRewardCards();
            screen.ShowCards(offer, 1.25f, offer.Count, card =>
            {
                if (m_RewardPicked)
                {
                    return;
                }

                m_RewardPicked = true;
                if (card != null)
                {
                    run.AddCard(card.Id);
                }

                CloseRewardSelection();
                ShowRoutes();
            });

            screen.AddButton("跳过", () =>
            {
                if (m_RewardPicked)
                {
                    return;
                }

                m_RewardPicked = true;
                CloseRewardSelection();
                ShowRoutes();
            });
        }

        private void CloseRewardSelection()
        {
            if (m_RewardScreen == null)
            {
                return;
            }

            var form = m_RewardScreen.GetComponent<UnityGameFramework.Runtime.UIForm>();
            if (form != null)
            {
                UIHost.Shared.Close(form);
            }

            m_RewardScreen = null;
        }

        /// <summary>清场并显示这一步的路线选项（普通战斗 / 精英战斗 / 火堆 / 商店）。</summary>
        private void ShowRoutes()
        {
            if (m_Stage == null)
            {
                return;
            }

            RunState run = RunState.Current;
            run.RollRouteOptions();
            m_Stage.ShowRoutes(run.RouteOptions, OnRoutePicked);
        }

        /// <summary>玩家选中了一个路线节点。</summary>
        private void OnRoutePicked(int index)
        {
            RunState run = RunState.Current;
            TurnCard.Game.Run.MapNode node = run.ChooseRoute(index);
            if (node == null)
            {
                return;
            }

            m_BattleRound++;
            m_Stage.ShowBattle();

            switch (node.Type)
            {
                case TurnCard.Game.Run.MapNodeType.Elite:
                    StartBattleWithPool(TurnCard.Game.Data.EnemyPool.Elite);
                    break;

                case TurnCard.Game.Run.MapNodeType.Rest:
                {
                    // 火堆：回血后重新抽路线
                    int healed = run.RestHeal();
                    if (txtHint != null)
                    {
                        txtHint.text = "火堆：回复 " + healed + " 点生命";
                    }

                    ShowRoutes();
                    break;
                }

                case TurnCard.Game.Run.MapNodeType.Shop:
                    // 商店还没实装：先跳过并重新抽路线
                    if (txtHint != null)
                    {
                        txtHint.text = "商店（待实装）";
                    }

                    ShowRoutes();
                    break;

                case TurnCard.Game.Run.MapNodeType.Boss:
                    StartBattleWithPool(TurnCard.Game.Data.EnemyPool.Boss);
                    break;

                default:
                    StartBattleWithPool(TurnCard.Game.Data.EnemyPool.Normal);
                    break;
            }
        }

        /// <summary>用指定池子开一场战斗（普通 / 精英 / 首领）。</summary>
        private void StartBattleWithPool(TurnCard.Game.Data.EnemyPool pool)
        {
            m_PendingPool = pool;
            StartBattle();
        }

        /// <summary>进入选关：不打架，只显示这一步的路线选项。</summary>
        public void EnterRouteSelect()
        {
            m_Content = Resources.Load<BattleContent>("BattleContent");
            if (m_Content == null || m_Content.Cards.Length == 0)
            {
                txtHint.text = "缺少 BattleContent，请先生成数据表";
                return;
            }

            ResetBattleState();
            if (txtStatus != null)
            {
                txtStatus.text = string.Empty;
            }

            ShowRoutes();

            // 选关时幕布是拉开的
            CurtainView.PlayAll();
        }

        /// <summary>同一界面重开一场：清场与重建都交给 StartBattle 的时序。</summary>
        private void RestartBattle()
        {
            StartBattle();
        }

        // ---------------- 调试台入口（不进正式逻辑分支） ----------------

        /// <summary>用指定阵容重开一场；传 null 表示按池子随机。</summary>
        public void DebugRestartWith(TurnCard.Game.Data.EnemyDefinition[] lineup)
        {
            DebugPendingLineup = lineup;
            RestartBattle();
        }

        /// <summary>往本场抽牌堆塞一张牌（同时已经加进 RunState 牌组）。</summary>
        public bool DebugAddCardToBattle(TurnCard.Game.Data.CardDefinition def)
        {
            if (m_State == null || def == null)
            {
                return false;
            }

            m_State.DebugAddToDrawPile(def);
            RefreshHud();
            return true;
        }

        public bool DebugSetEnergy(int value)
        {
            if (m_State == null)
            {
                return false;
            }

            m_State.DebugSetEnergy(value);
            RefreshHud();
            return true;
        }

        public bool DebugKillAll()
        {
            if (m_State == null || m_State.Enemies.Count == 0)
            {
                return false;
            }

            bool killed = false;
            var snapshot = new List<EnemyInstance>(m_State.Enemies);
            foreach (EnemyInstance enemy in snapshot)
            {
                // 敌人血量归零会先翻面（双形态），所以要多打几轮才真的死
                for (int i = 0; i < 8 && enemy != null && !enemy.IsDead; i++)
                {
                    m_State.DamageEnemy(enemy, 9999);
                    killed = true;
                }
            }

            RefreshAllHpBars();
            return killed;
        }

        private static List<CardDefinition> PickRewardCards()
        {
            var result = new List<CardDefinition>();
            BattleContent content = Resources.Load<BattleContent>("BattleContent");
            if (content == null || content.Cards.Length == 0)
            {
                return result;
            }

            var pool = new List<CardDefinition>(content.Cards);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }

        /// <summary>
        /// 这一下该用炮弹还是箭矢。
        /// 规则：伤害够大（≥8）用炮；「随机 / 全体」这类间接伤害也用炮（比如 装填→开火）。
        /// 以后如果要给某张卡指定特效，可以在表里加一列明确写死，不必靠猜。
        /// </summary>
        private static bool IsCannon(CardFace playedFace, CardFace currentFace, int damage)
        {
            if (damage >= 8)
            {
                return true;
            }

            // 伤害可能来自翻面后的那一面（装填 → 开火），两面都要看
            return IsIndirect(playedFace) || IsIndirect(currentFace);
        }

        /// <summary>是否是「随机 / 全体」这类间接伤害（用炮弹表现）。</summary>
        private static bool IsIndirect(CardFace face)
        {
            string effects = face.Effects;
            if (string.IsNullOrEmpty(effects))
            {
                return false;
            }

            return effects.IndexOf("random", System.StringComparison.OrdinalIgnoreCase) >= 0
                || effects.IndexOf("all", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnEndTurnClicked()
        {
            if (m_Busy || m_State == null || !m_State.IsPlayerTurn || m_State.IsBattleOver)
            {
                return;
            }

            StartCoroutine(EndTurnRoutine());
        }

        /// <summary>
        /// 敌人出招的表现：攻击放弹道（每段一发），防御/强化/削弱放提示。
        /// 伤害本身仍由 ResolveEnemyTurn 结算，这里只负责「看得见」。
        /// </summary>
        private IEnumerator PlayEnemyIntent(EnemyCardView enemyView, EnemyIntentDefinition intent, BattleFxSettings fx)
        {
            if (enemyView == null)
            {
                yield break;
            }

            Vector3 popupAt = txtStatus.rectTransform.position + new Vector3(0f, 10f, 0f);
            Vector2 to = (Vector2)m_FxLayer.InverseTransformPoint(popupAt);

            switch (intent.Intent)
            {
                case EnemyIntentType.Attack:
                    // 攻击动作由 PlayEnemyAttack 负责（卡牌蓄力 + 撞击，不走弹道）
                    break;

                case EnemyIntentType.Defend:
                    PopDamage(popupAt, "护甲 +" + intent.Value, new Color(0.65f, 0.85f, 1f));
                    yield return new WaitForSeconds(0.35f);
                    break;

                case EnemyIntentType.Buff:
                    PopDamage(popupAt, "攻击 +" + intent.Value, new Color(1f, 0.85f, 0.45f));
                    yield return new WaitForSeconds(0.35f);
                    break;

                default:
                    PopDamage(popupAt, "易伤 +" + intent.Value, new Color(1f, 0.6f, 0.85f));
                    yield return new WaitForSeconds(0.35f);
                    break;
            }
        }

        /// <summary>
        /// 敌人的攻击：卡牌抬起来蓄力 → 向下撞一下 → 回位；
        /// 撞击那一下结算这一段伤害，并**立即刷新所有血条**（多段时每段都刷）。
        /// </summary>
        private IEnumerator PlayEnemyAttack(EnemyCardView enemyView, BattleFxSettings fx)
        {
            if (enemyView == null || m_State == null)
            {
                yield break;
            }

            int perHit = m_State.EnemyAttackPerHit(enemyView.Enemy);
            int hits = m_State.ConsumeEnemyHitCount(enemyView.Enemy);
            Vector3 popupAt = txtStatus.rectTransform.position + new Vector3(0f, 10f, 0f);

            for (int hit = 0; hit < hits; hit++)
            {
                bool impacted = false;
                TurnCard.Game.Audio.Sfx.Attack(perHit);

                enemyView.PlayAttack(() =>
                {
                    int lost = m_State.ApplyEnemyAttackHit(enemyView.Enemy, perHit);
                    TurnCard.Game.Audio.Sfx.Hit();

                    if (lost > 0)
                    {
                        PopDamage(popupAt, "-" + lost, new Color(1f, 0.45f, 0.45f));
                        m_FormRect.DOShakePosition(0.16f, 6f, 12, 80f, false, true);
                    }

                    // 每一段打完立刻刷新玩家血条与所有敌人卡（不等整轮结算）
                    RefreshAllHpBars();
                    impacted = true;
                });

                yield return new WaitUntil(() => impacted);

                if (m_State.IsBattleOver)
                {
                    yield break;
                }

                if (hit + 1 < hits)
                {
                    yield return new WaitForSeconds(fx.HitInterval);
                }
            }
        }

        private IEnumerator EndTurnRoutine()
        {
            m_Busy = true;
            btnEndTurn.interactable = false;

            RefreshPlayableStates();

            UnityEngine.Debug.Log("[回合] 1 结束回合，开始弃手牌");
            m_State.EndPlayerTurn();
            UnityEngine.Debug.Log("[回合] 2 弃牌完成，手牌=" + m_State.Hand.Count);
            ScatterHandToDiscard();
            UnityEngine.Debug.Log("[回合] 3 散牌动画启动");   // 没被事件接管的残留视图也一并飞走
            RefreshHud();

            yield return new WaitForSeconds(0.35f);

            BattleFxSettings fx = BattleFxSettings.Current;

            // 敌人出招：从左到右逐个播动作，全部播完再统一结算
            // （结算顺序同样是列表顺序，即地图上的从左到右）
            UnityEngine.Debug.Log("[回合] 4 敌人开始出招");
            foreach (EnemyCardView enemyView in m_EnemyCardViews)
            {
                if (enemyView == null || enemyView.Enemy.IsDead)
                {
                    continue;
                }

                EnemyIntentDefinition intent = enemyView.Enemy.CurrentIntent;
                if (intent == null)
                {
                    continue;
                }

                if (intent.Intent == EnemyIntentType.Attack)
                {
                    // 攻击：卡牌蓄力 + 撞击，每段命中立刻扣血并刷血条
                    yield return PlayEnemyAttack(enemyView, fx);
                }
                else
                {
                    yield return PlayEnemyIntent(enemyView, intent, fx);
                }
            }

            UnityEngine.Debug.Log("[回合] 5 敌人出招播完，结算非攻击部分");
            int hpBefore = m_State.PlayerHp;

            // 攻击伤害已经在表现里逐段结算，这里只补防御/强化/晕眩/回血等
            m_State.ResolveEnemyTurn(true);
            UnityEngine.Debug.Log("[回合] 6 敌人结算完成 hp=" + m_State.PlayerHp);

            RefreshEnemyCardViews();

            int hpLost = hpBefore - m_State.PlayerHp;
            if (hpLost > 0)
            {
                m_FormRect.DOShakePosition(0.22f, 9f, 10, 90f, false, true);
                PopDamage(txtStatus.rectTransform.position + new Vector3(0f, 10f, 0f),
                    "-" + hpLost, new Color(1f, 0.45f, 0.45f));
            }

            RefreshAllHpBars();
            yield return new WaitForSeconds(0.5f);

            if (m_State.IsBattleOver)
            {
                ShowResult(false);
                yield break;
            }

            UnityEngine.Debug.Log("[回合] 7 开始新回合");
            m_State.StartPlayerTurn();
            UnityEngine.Debug.Log("[回合] 8 抽牌完成 手牌=" + m_State.Hand.Count);
            BuildHand();
            UnityEngine.Debug.Log("[回合] 9 手牌重建完成");
            m_Busy = false;
            RefreshHud();
            btnEndTurn.interactable = true;
        }

        /// <summary>
        /// 刷新每张手牌的「当前可打出」光边：
        /// 玩家回合 + 不在结算中 + 能量足够，三个条件同时满足才亮。
        /// </summary>
        private void RefreshPlayableStates()
        {
            if (m_State == null)
            {
                return;
            }

            bool canAct = m_State.IsPlayerTurn && !m_State.IsResolving && !m_Busy;
            foreach (CardView view in m_HandViews)
            {
                if (view == null || view.Card == null)
                {
                    continue;
                }

                bool playable = canAct && view.Card.Face.Cost <= m_State.Energy;
                view.SetPlayable(playable);
            }
        }

        /// <summary>
        /// 把所有血条立刻刷到最新：玩家血条 + 每个敌人卡（含意图、护甲角标）。
        /// 伤害每次落地都调用，保证视觉和数值同步。
        /// </summary>
        private void RefreshAllHpBars()
        {
            RefreshHud();

            for (int i = m_EnemyCardViews.Count - 1; i >= 0; i--)
            {
                EnemyCardView view = m_EnemyCardViews[i];
                if (view == null)
                {
                    m_EnemyCardViews.RemoveAt(i);
                    continue;
                }

                view.RefreshVisual();
            }
        }

        private void RefreshHud()
        {
            if (m_State == null)
            {
                return;
            }

            // 血量用和敌人卡一样的血条：裁剪窗口按比例伸缩
            int maxHp = BattleState.PlayerMaxHp;
            int hp = m_State.PlayerHp;
            float ratio = maxHp <= 0 ? 0f : Mathf.Clamp01(hp / (float)maxHp);
            if (m_HpClip != null)
            {
                Vector2 anchorMax = m_HpClip.anchorMax;
                anchorMax.x = ratio;
                m_HpClip.anchorMax = anchorMax;
            }

            if (m_HpText != null)
            {
                m_HpText.text = hp + "/" + maxHp;
            }

            // 护甲徽标：有护甲才显示（图标是「防御意图」那张）
            int armorValue = m_State.PlayerArmor;
            if (m_ArmorRoot != null)
            {
                m_ArmorRoot.SetActive(armorValue > 0);
            }

            if (m_ArmorText != null)
            {
                m_ArmorText.text = armorValue.ToString();
            }

            txtStatus.text = $"能量 {m_State.Energy}/{BattleState.MaxEnergy}";
            txtDrawCount.text = m_State.DrawPile.Count.ToString();
            txtDiscardCount.text = m_State.DiscardPile.Count.ToString();
            RefreshHandCosts();
            RefreshInteractivity();

            RefreshPlayableStates();
        }

        /// <summary>
        /// 贪婪生效时把手牌费用显示成 0（免额度用掉后恢复原价）。
        /// 只在状态切换时刷一次，避免每帧重刷整手牌。
        /// </summary>
        private void RefreshHandCosts()
        {
            if (m_State == null)
            {
                return;
            }

            bool free = m_State.FreeCardAvailable;
            if (free == m_CostFreeShown)
            {
                return;
            }

            m_CostFreeShown = free;
            foreach (CardView view in m_HandViews)
            {
                if (view == null)
                {
                    continue;
                }

                view.SetCostOverride(free ? 0 : (int?)null);
            }
        }

        private void RefreshInteractivity()
        {
            foreach (CardView view in m_HandViews)
            {
                if (view == null)
                {
                    continue;
                }

                // 选择模式下所有牌不可「打出」，但可以点选
                view.Interactable = m_ChoiceMode ||
                                    (!m_Busy && m_State != null && m_State.IsPlayerTurn &&
                                     !m_State.IsBattleOver && view.Card.Face.Cost <= m_State.Energy);
            }
        }

        private void ShowResult(bool victory)
        {
            txtResult.text = victory ? "胜利" : "失败";
            txtResult.color = victory ? new Color(0.60f, 1f, 0.70f) : new Color(1f, 0.45f, 0.45f);
            btnEndTurn.interactable = false;

            // 让玩家看清结果，再通知流程往下走
            int remainingHp = m_State != null ? m_State.PlayerHp : 0;
            DG.Tweening.DOVirtual.DelayedCall(1.1f, () =>
            {
                TurnCard.Game.EventBus.Publish(
                    new TurnCard.Game.Run.BattleFinishedEvent
                    {
                        Victory = victory,
                        RemainingHp = remainingHp,
                    });
            });
        }

        private void OpenDrawPile()
        {
            if (m_State == null)
            {
                return;
            }

            OpenPileList("抽牌堆", m_State.DrawPile);
        }

        private void OpenDiscardPile()
        {
            if (m_State == null)
            {
                return;
            }

            OpenPileList("弃牌堆", m_State.DiscardPile);
        }

        /// <summary>用通用的卡牌列表界面展示牌堆（遮罩 + 滚动网格）。</summary>
        private void OpenPileList(string title, IReadOnlyList<CardInstance> pile)
        {
            ClosePileList();

            var definitions = new List<CardDefinition>();
            foreach (CardInstance card in pile)
            {
                definitions.Add(card.Definition);
            }

            CardListViewScreen screen = UIHost.Shared.Open<CardListViewScreen>(
                CardListViewScreen.ResourcePath);
            if (screen == null)
            {
                return;
            }

            m_ListView = screen;
            screen.SetHeader(title, $"共 {definitions.Count} 张");
            screen.ShowCards(definitions, null);
            screen.OnCloseRequested = ClosePileList;
        }

        private void ClosePileList()
        {
            if (m_ListView == null)
            {
                return;
            }

            UIForm form = m_ListView.GetComponent<UIForm>();
            if (form != null)
            {
                UIHost.Shared.Close(form);
            }

            m_ListView = null;
        }

        private void ShowArrow(Vector2 fromScreen, Vector2 toScreen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_ArrowLayer, fromScreen, null,
                out Vector2 from);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_ArrowLayer, toScreen, null,
                out Vector2 to);

            m_Arrow.SetPoints(from, to);
            m_Arrow.gameObject.SetActive(true);
        }

        private void HideArrow()
        {
            if (m_Arrow != null)
            {
                m_Arrow.gameObject.SetActive(false);
            }
        }

        /// <summary>把世界坐标换算到手牌容器的本地坐标。</summary>
        private Vector2 HandLocalOf(Vector3 worldPosition)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_HandRoot, screen, null,
                out Vector2 local);
            return local;
        }

        /// <summary>抽牌堆在手牌容器里的位置（发牌动画的起点）。</summary>
        private Vector2 HandLocalOfDrawPile()
        {
            return btnDrawPile != null ? HandLocalOf(btnDrawPile.transform.position)
                : new Vector2(-820f, -130f);
        }


        /// <summary>找到某个敌人对应的视图。</summary>
        private EnemyCardView ViewFor(EnemyInstance enemy)
        {
            if (enemy == null)
            {
                return null;
            }

            foreach (EnemyCardView view in m_EnemyCardViews)
            {
                if (view != null && ReferenceEquals(view.Enemy, enemy))
                {
                    return view;
                }
            }

            return null;
        }

        /// <summary>屏幕坐标下命中的敌人视图（用于拖拽选目标）。</summary>
        private EnemyCardView ViewUnder(Vector2 screenPosition)
        {
            foreach (EnemyCardView view in m_EnemyCardViews)
            {
                if (view == null || view.Enemy.IsDead)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(view.Rect, screenPosition, null))
                {
                    return view;
                }
            }

            return null;
        }

        /// <summary>
        /// 敌人死亡：从视图中摘除 → 播死亡动画 → 销毁 → 重排剩余敌人。
        /// 逻辑层的 <c>m_State.Enemies</c> 不动（结算与选目标都靠 IsDead 过滤）。
        /// </summary>
        private void OnEnemyDied(TurnCard.Game.Battle.EnemyInstance enemy)
        {
            EnemyCardView view = ViewFor(enemy);
            if (view == null)
            {
                return;
            }

            // 先摘掉，避免动画期间被选中/被刷新
            m_EnemyCardViews.Remove(view);

            EnemyDeathFx.Play(view, () =>
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }

                LayoutEnemyRow();
            });
        }

        /// <summary>把存活的敌人重新横向居中；带 0.2s 过渡，避免瞬移。</summary>
        private void LayoutEnemyRow()
        {
            for (int i = m_EnemyCardViews.Count - 1; i >= 0; i--)
            {
                if (m_EnemyCardViews[i] == null)
                {
                    m_EnemyCardViews.RemoveAt(i);
                }
            }

            int count = m_EnemyCardViews.Count;
            if (count == 0)
            {
                return;
            }

            float cardW = EnemyCardWidth;
            float width = count * cardW + Mathf.Max(0, count - 1) * EnemySpacing;
            float left = -width * 0.5f + cardW * 0.5f;

            for (int i = 0; i < count; i++)
            {
                EnemyCardView view = m_EnemyCardViews[i];
                RectTransform rect = view.Rect;
                float y = rect.anchoredPosition.y;
                rect.DOKill();
                rect.DOAnchorPos(new Vector2(left + i * (cardW + EnemySpacing), y), 0.2f)
                    .SetEase(Ease.OutQuad);
            }
        }

        /// <summary>刷新所有敌人的显示。</summary>
        private void RefreshEnemyCardViews()
        {
            foreach (EnemyCardView view in m_EnemyCardViews)
            {
                if (view != null)
                {
                    view.RefreshVisual();
                }
            }
        }

        /// <summary>命中反馈：敌人抖动 + 伤害飘字。</summary>
        private void PlayHitFeedback(int damage)
        {
            TurnCard.Game.Audio.Sfx.Hit();

            EnemyCardView view = m_LastHitEnemy != null ? ViewFor(m_LastHitEnemy) : null;
            RectTransform enemyRect = view != null ? view.Rect : m_FormRect;
            if (enemyRect == null)
            {
                return;
            }

            enemyRect.DOKill();
            enemyRect.DOShakePosition(0.28f, 14f, 12, 90f, false, true);
            PopDamage(enemyRect.position + new Vector3(0f, 40f, 0f), "-" + damage,
                new Color(1f, 0.55f, 0.45f));
        }

        /// <summary>在界面某处弹出一段上浮淡出的文字。</summary>
        private void PopDamage(Vector3 worldPosition, string text, Color color)
        {
            TextMeshProUGUI label = UiFactory.CreateLabel(m_FormRect, "Popup", text, 46, color,
                new Vector2(240f, 64f), new Vector2(0.5f, 0.5f), Vector2.zero);
            label.raycastTarget = false;

            RectTransform rt = label.rectTransform;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_FormRect, screen, null,
                out Vector2 local);
            rt.anchoredPosition = local;

            rt.DOAnchorPos(local + new Vector2(0f, 90f), 0.8f).SetEase(Ease.OutCubic);
            label.DOFade(0f, 0.8f).SetDelay(0.2f)
                .OnComplete(() => Destroy(label.gameObject));
        }

        private void ShowHint(string message)
        {            txtHint.text = message;
            CancelInvoke(nameof(ClearHint));
            Invoke(nameof(ClearHint), 1.6f);
        }

        private void ClearHint()
        {
            txtHint.text = string.Empty;
        }

        private static string DescribeFailure(PlayResult result)
        {
            switch (result)
            {
                case PlayResult.NotEnoughEnergy:
                    return "能量不足";
                case PlayResult.NeedsTarget:
                    return "这张牌需要拖到敌人身上使用";
                case PlayResult.NotPlayerTurn:
                    return "现在不是你的回合";
                case PlayResult.BattleOver:
                    return "战斗已经结束";
                case PlayResult.Busy:
                    return "结算中，稍候";
                default:
                    return "无法使用这张牌";
            }
        }
    }
}
