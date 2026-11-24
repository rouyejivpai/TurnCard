using System;
using System.Collections.Generic;
using TurnCard.Game.Data;

namespace TurnCard.Game.Battle
{
    /// <summary>一张卡牌在战斗中的运行时状态。翻面状态跟随这张卡（Q16）。</summary>
    public sealed class CardInstance
    {
        public CardInstance(CardDefinition definition)
        {
            Definition = definition;
        }

        public CardDefinition Definition { get; }

        /// <summary>本回合结束时不进弃牌堆（「保留」效果临时打上的标记）。</summary>
        public bool RetainedThisTurn { get; set; }

        /// <summary>
        /// 「免费打出过」标记：贪婪让你 0 费打出这张牌时打上，
        /// 之后再打出它（以及翻面结算时）由「代价」读取。
        /// </summary>
        public bool FreePlayed { get; set; }
        public CardFaceSide Side { get; private set; } = CardFaceSide.Front;
        public CardFace Face => Definition.GetFace(Side);
        public bool IsFront => Side == CardFaceSide.Front;

        public void Flip()
        {
            SetSide(Side == CardFaceSide.Front ? CardFaceSide.Back : CardFaceSide.Front);
        }

        /// <summary>直接摆到指定面（同面则没有变化）。</summary>
        public void SetSide(CardFaceSide side)
        {
            if (Side == side)
            {
                return;
            }

            Side = side;
            Flipped?.Invoke(this);
        }

        /// <summary>
        /// 翻面完成时触发。机制（如「瞬发」）通过监听这个事件生效，
        /// 而不是在出牌流程里硬编码调用——这样任何来源的翻面都会正确触发。
        /// </summary>

        public event System.Action<CardInstance> Flipped;
    }

    /// <summary>一个敌人在战斗中的运行时状态。敌人是角色卡，两形态各自独立的血量与攻击（Q14、Q18、Q21）。</summary>
    public sealed class EnemyInstance
    {
        public EnemyInstance(EnemyDefinition definition)
        {
            Definition = definition;
            Side = CardFaceSide.Front;
            Hp = definition.Front.MaxHp;
        }

        public EnemyDefinition Definition { get; }
        public CardFaceSide Side { get; private set; }
        public EnemyForm Form => Definition.GetForm(Side);
        public bool IsDead { get; private set; }

        private int m_Hp;
        private int m_Armor;

        /// <summary>生命变化（受伤 / 回血 / 翻面重置都会触发）。UI 订阅这个做响应式刷新。</summary>
        public event System.Action<EnemyInstance> HpChanged;

        /// <summary>护甲变化。</summary>
        public event System.Action<EnemyInstance> ArmorChanged;

        /// <summary>意图队列变化（选定/推进/重洗）。</summary>
        public event System.Action<EnemyInstance> IntentChanged;

        private void RaiseIntentChanged()
        {
            IntentChanged?.Invoke(this);
        }

        /// <summary>生命。任何写入都会触发 <see cref="HpChanged"/>。</summary>
        public int Hp
        {
            get => m_Hp;
            private set
            {
                if (m_Hp == value)
                {
                    return;
                }

                m_Hp = value;
                HpChanged?.Invoke(this);
            }
        }

        /// <summary>护甲，每回合开始清零（壁垒 buff 可以拦住这次清零）。</summary>
        public int Armor
        {
            get => m_Armor;
            set
            {
                if (m_Armor == value)
                {
                    return;
                }

                m_Armor = value;
                ArmorChanged?.Invoke(this);
            }
        }

        /// <summary>羁绊配对 id（0 = 无配对），同 id 的敌人互为羁绊对象。</summary>
        public int PairId => Definition.PairId;

        /// <summary>下一次攻击额外增加的段数（嗜血类 buff 累积，执行后清零）。</summary>
        public int BonusHits { get; set; }

        /// <summary>本回合「吸收」吃掉的伤害总量（贤者的吸收反噬据此换算力量损失）。</summary>
        public int AbsorbedThisTurn { get; set; }

        public int ConsumeBonusHits()
        {
            int bonus = BonusHits;
            BonusHits = 0;
            return bonus;
        }

        /// <summary>回血（不超过当前形态的满血）。</summary>
        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead)
            {
                return;
            }

            Hp = Math.Min(Form.MaxHp, Hp + amount);
        }

        /// <summary>直接扣血：不经过护甲、不吃「受到的伤害」修正（自伤用）。</summary>
        public void LoseHpDirect(int amount)
        {
            if (amount <= 0 || IsDead)
            {
                return;
            }

            Hp -= amount;
            if (Hp > 0)
            {
                return;
            }

            if (Side == CardFaceSide.Front)
            {
                int before = Hp;
                Side = CardFaceSide.Back;
                Hp = ResolveFlipHp(before);
                return;
            }

            Hp = 0;
            IsDead = true;
        }

        /// <summary>晕眩：本回合跳过行动。</summary>
        public bool Stunned { get; set; }

        /// <summary>本回合有没有对玩家造成过伤害（「未造成伤害」类特性用）。</summary>
        public bool DealtDamageThisTurn { get; set; }

        /// <summary>按配置的策略算出翻面后的血量。</summary>
        private int ResolveFlipHp(int current)
        {
            switch (Definition.FlipHpMode)
            {
                case EnemyFlipHpMode.Own:
                    return Form.MaxHp;

                case EnemyFlipHpMode.Inherit:
                    return Math.Max(1, current);

                default:
                    return Math.Max(1, (int)Math.Round(Form.MaxHp * Definition.FlipHpRatio));
            }
        }

        /// <summary>强制翻面（由 buff 触发，不经过血量归零）。</summary>
        public bool ForceFlip()
        {
            if (IsDead || Side == CardFaceSide.Back)
            {
                return false;
            }

            int current = Hp;
            Side = CardFaceSide.Back;
            Hp = ResolveFlipHp(current);
            return true;
        }

        /// <summary>身上的 buff（加攻、易伤、护盾等）。</summary>
        public BuffContainer Buffs { get; } = new BuffContainer();

        /// <summary>攻击加成来自 buff（Strength 的 attack 修饰器）。</summary>
        public int AttackBonus => (int)Buffs.Flat("attack");

        /// <summary>当前形态这一轮洗好的招式队列。</summary>
        private readonly List<Data.EnemyIntentDefinition> m_Intents =
            new List<Data.EnemyIntentDefinition>();

        /// <summary>队列里下一个要执行的招式。</summary>
        public Data.EnemyIntentDefinition CurrentIntent =>
            m_Intents.Count > 0 ? m_Intents[0] : null;

        /// <summary>
        /// 用当前形态的招式池洗牌生成队列（一轮内不重复）。
        /// </summary>
        public void RefillIntents(IEnumerable<Data.EnemyIntentDefinition> pool)
        {
            m_Intents.Clear();
            if (pool != null)
            {
                m_Intents.AddRange(pool);
            }

            // 先按表里的行 id 排好，保证顺序模式的节奏稳定
            m_Intents.Sort((a, b) => a.Id.CompareTo(b.Id));

            bool sequence = Definition != null &&
                            Definition.IntentMode == Data.EnemyIntentMode.Sequence;
            if (sequence)
            {
                RaiseIntentChanged();
                return;
            }

            // Fisher-Yates 洗牌
            for (int i = m_Intents.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                Data.EnemyIntentDefinition swap = m_Intents[i];
                m_Intents[i] = m_Intents[j];
                m_Intents[j] = swap;
            }

            RaiseIntentChanged();
        }

        /// <summary>取走队首招式（执行完之后调用）。</summary>
        public void PopIntent()
        {
            if (m_Intents.Count > 0)
            {
                m_Intents.RemoveAt(0);
                RaiseIntentChanged();
            }
        }

        /// <summary>扣除生命。返回本次是否发生了翻面（用于播放翻转动画）。</summary>
        public bool TakeDamage(int amount)
        {
            if (IsDead || amount <= 0)
            {
                return false;
            }

            // 护甲先吸收伤害
            int absorbed = Math.Min(Armor, amount);
            if (absorbed > 0)
            {
                Armor -= absorbed;
                amount -= absorbed;
                if (amount <= 0)
                {
                    return false;
                }
            }

            Hp -= amount;
            if (Hp > 0)
            {
                return false;
            }

            if (Side == CardFaceSide.Front)
            {
                // 第一形态被打空：按配置的翻面血量策略进入第二形态
                int before = Hp;
                Side = CardFaceSide.Back;
                Hp = ResolveFlipHp(before);
                return true;
            }

            Hp = 0;
            IsDead = true;
            return false;
        }
    }

    /// <summary>伤害来源，表现层据此决定播放时机。</summary>
    public enum HitOrigin
    {
        /// <summary>出牌本身造成的伤害：与翻面动画同时播。</summary>
        Play,

        /// <summary>翻面触发的瞬发伤害：等翻面与自身效果播完再播。</summary>
        Instant,
    }

    /// <summary>需要玩家做选择的类型。</summary>
    public enum ChoiceKind
    {
        None,

        /// <summary>选一张手牌翻面（条件反射）。</summary>
        FlipHandCard,
    }

    public enum PlayResult
    {
        Ok,
        NotPlayerTurn,
        NotEnoughEnergy,
        NeedsTarget,
        InvalidTarget,
        BattleOver,
        Busy,       // 上一手还在播放结算动画，拒绝新的操作
    }

    /// <summary>
    /// 战斗的纯逻辑部分，不依赖任何 UI。表现层只读取状态并播放动画。
    /// 数值全部来自占位配置（Q5）与数据表。
    /// </summary>
    public sealed class BattleState
    {
        public const int MaxEnergy = 3;
        public const int HandSize = 5;
        public const int PlayerMaxHp = 80;

        /// <summary>翻面请求的来源，仅用于调试与表现层区分。</summary>
        public enum FlipSource
        {
            Play,
            Effect,
            Keyword,
        }

        /// <summary>交给表现层的一次翻面：明确的起止面。</summary>
        public readonly struct FlipStep
        {
            public FlipStep(CardInstance card, CardFaceSide from, CardFaceSide to)
            {
                Card = card;
                From = from;
                To = to;
            }

            public CardInstance Card { get; }
            public CardFaceSide From { get; }
            public CardFaceSide To { get; }
        }

        /// <summary>排队中的一次翻面。</summary>
        public readonly struct PendingFlip
        {
            public PendingFlip(CardInstance card, FlipSource source, EnemyInstance target,
                CardFaceSide? targetSide = null)
            {
                Card = card;
                Source = source;
                Target = target;
                TargetSide = targetSide;
            }

            /// <summary>指定的目标面（null = 直接翻到另一面）。</summary>
            public CardFaceSide? TargetSide { get; }

            public CardInstance Card { get; }
            public FlipSource Source { get; }

            /// <summary>本次出牌选中的目标，「瞬发」结算时用。</summary>
            public EnemyInstance Target { get; }
        }

        private readonly List<PendingFlip> m_PendingFlips = new List<PendingFlip>();

        private readonly struct PendingInstant
        {
            public PendingInstant(CardInstance card, EnemyInstance target)
            {
                Card = card;
                Target = target;
            }

            public CardInstance Card { get; }
            public EnemyInstance Target { get; }
        }

        private readonly List<PendingInstant> m_PendingInstants = new List<PendingInstant>();

        /// <summary>翻面已经把瞬发收集起来，等动画播完再执行。</summary>
        public bool HasPendingInstants => m_PendingInstants.Count > 0;

        private bool m_InstantPhase;

        /// <summary>排队中的翻面，表现层按顺序播放。</summary>
        public IReadOnlyList<PendingFlip> PendingFlips => m_PendingFlips;

        /// <summary>还有翻面没落地。</summary>
        public bool IsResolvingFlips => m_PendingFlips.Count > 0;

        /// <summary>当前正在结算的翻面来源。</summary>
        public FlipSource CurrentFlipSource => m_FlipSource;

        private FlipSource m_FlipSource = FlipSource.Effect;

        /// <summary>
        /// 请求翻面：唯一入口。效果、关键词、出牌都走这里，
        /// 真正的翻面由表现层在动画落地时 Commit 触发。
        /// </summary>
        public void RequestFlip(CardInstance card, FlipSource source, EnemyInstance target = null)
        {
            if (card == null || IsBattleOver)
            {
                return;
            }

            m_PendingFlips.Add(new PendingFlip(card, source, target));
        }

        /// <summary>
        /// 请求翻到**指定面**（已经是那一面就什么都不做）。
        /// 电光火石用这个：即使「双重瞬发」连续触发两次，也只会收敛到目标面，不会来回翻。
        /// </summary>
        public void RequestFlipTo(CardInstance card, CardFaceSide side, FlipSource source,
            EnemyInstance target = null)
        {
            if (card == null || IsBattleOver)
            {
                return;
            }

            if (card.Side == side)
            {
                return;
            }

            m_PendingFlips.Add(new PendingFlip(card, source, target, side));
        }

        /// <summary>
        /// 表现层处理翻面队列期间开启延迟结算：
        /// 翻面触发的「瞬发」伤害先入队，等弹道命中再扣血。
        /// </summary>
        public void BeginDeferDamage()
        {
            m_DeferDamage = true;
        }

        public void EndDeferDamage()
        {
            m_DeferDamage = false;
        }

        /// <summary>把指定的一张手牌丢进弃牌堆。不在手里就什么也不做。</summary>
        public void DiscardCard(CardInstance card)
        {
            if (card == null)
            {
                return;
            }

            for (int i = 0; i < m_Hand.Count; i++)
            {
                if (!ReferenceEquals(m_Hand[i], card))
                {
                    continue;
                }

                m_Hand.RemoveAt(i);
                m_DiscardPile.Add(card);
                CardDiscarded?.Invoke(card);
                RaiseChanged();
                return;
            }
        }

        /// <summary>失去护甲，最多扣到 0（不会转为扣血）。</summary>
        public void LosePlayerArmor(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            PlayerArmor = Math.Max(0, PlayerArmor - amount);
        }

        /// <summary>手牌里指定类型的张数（用于「协同攻击」这类按张数结算的牌）。</summary>
        public int CountHandOfType(CardType type)
        {
            int count = 0;
            foreach (CardInstance card in m_Hand)
            {
                if (card != null && card.Face.Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>把手牌全部丢进弃牌堆（不抽新牌）。</summary>
        public void DiscardHand()
        {
            while (m_Hand.Count > 0)
            {
                CardInstance card = m_Hand[0];
                m_Hand.RemoveAt(0);
                m_DiscardPile.Add(card);
                CardDiscarded?.Invoke(card);
            }
        }

        /// <summary>
        /// 提交队首翻面，返回这次翻面的起止面（没有请求时返回 null）。
        /// 翻面触发的瞬发只收集，由 <see cref="ResolvePendingInstants"/> 在动画播完后执行。
        /// </summary>
        public FlipStep? CommitNextFlip()
        {
            if (m_PendingFlips.Count == 0)
            {
                return null;
            }

            PendingFlip flip = m_PendingFlips[0];
            m_PendingFlips.RemoveAt(0);

            if (flip.Card == null)
            {
                return null;
            }

            CardFaceSide from = flip.Card.Side;

            // 指定了目标面就摆到那一面（同面则视为无效翻面）
            if (flip.TargetSide.HasValue && from == flip.TargetSide.Value)
            {
                return null;
            }

            m_FlipSource = flip.Source;
            m_PlayTarget = flip.Target;
            m_LastInstantFlip = null;
            try
            {
                if (flip.TargetSide.HasValue)
                {
                    flip.Card.SetSide(flip.TargetSide.Value);
                }
                else
                {
                    flip.Card.Flip();
                }
            }
            finally
            {
                m_PlayTarget = null;
                m_FlipSource = FlipSource.Effect;
            }

            return new FlipStep(flip.Card, from, flip.Card.Side);
        }

        /// <summary>
        /// 执行翻面收集到的瞬发：表现层在翻面动画与自身效果都播完之后调用，
        /// 保证「瞬发在这两者之后」。
        /// </summary>
        public void ResolvePendingInstants()
        {
            if (m_PendingInstants.Count == 0)
            {
                return;
            }

            var batch = new List<PendingInstant>(m_PendingInstants);
            m_PendingInstants.Clear();

            m_InstantPhase = true;
            try
            {
                foreach (PendingInstant pending in batch)
                {
                    if (pending.Card == null)
                    {
                        continue;
                    }

                    RunTriggers(PlayerBuffs, Data.BuffTrigger.Flip, pending.Target);
                    InstantTriggered?.Invoke(pending.Card);

                    EnemyInstance flipped = TriggerInstant(pending.Card, pending.Target);
                    if (flipped != null)
                    {
                        LastInstantFlipEnemy = flipped;
                    }
                }
            }
            finally
            {
                m_InstantPhase = false;
            }
        }

        /// <summary>
        /// 当前正在执行效果的卡：出牌 / 翻面瞬发 / 相邻牌效果都会设置。
        /// 伤害入队时记录它，表现层才知道特效该从哪张牌出发。
        /// </summary>
        private CardInstance m_ExecutingCard;

        /// <summary>当前效果来源卡（没有则为 null）。</summary>
        public CardInstance ExecutingCard => m_ExecutingCard;

        /// <summary>进入某张卡的效果上下文，返回上一张（配合 <see cref="ExitCardContext"/>）。</summary>
        public CardInstance EnterCardContext(CardInstance card)
        {
            CardInstance previous = m_ExecutingCard;
            m_ExecutingCard = card;
            return previous;
        }

        /// <summary>恢复上一层效果上下文。</summary>
        public void ExitCardContext(CardInstance previous)
        {
            m_ExecutingCard = previous;
        }

        /// <summary>战斗结束时丢掉没执行的瞬发。</summary>
        public void ClearPendingInstants()
        {
            m_PendingInstants.Clear();
        }

        private readonly List<CardInstance> m_DrawPile = new List<CardInstance>();
        private readonly List<CardInstance> m_Hand = new List<CardInstance>();
        private readonly List<CardInstance> m_DiscardPile = new List<CardInstance>();
        private readonly List<EnemyInstance> m_Enemies = new List<EnemyInstance>();
        private readonly System.Random m_Random = new System.Random();

        public IReadOnlyList<CardInstance> DrawPile => m_DrawPile;
        public IReadOnlyList<CardInstance> Hand => m_Hand;
        public IReadOnlyList<CardInstance> DiscardPile => m_DiscardPile;
        public IReadOnlyList<EnemyInstance> Enemies => m_Enemies;

        /// <summary>调试台用：往抽牌堆塞一张牌（放最上面，下一次抽牌就能摸到）。</summary>
        public void DebugAddToDrawPile(Data.CardDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            var instance = new CardInstance(definition);
            instance.Flipped += OnCardFlipped;
            m_DrawPile.Insert(0, instance);
        }

        /// <summary>调试台用：直接改当前能量。</summary>
        public void DebugSetEnergy(int value)
        {
            Energy = System.Math.Max(0, value);
        }

        /// <summary>
        /// 任何会影响界面的数值变化都会触发这个事件（玩家/敌人生命、护甲、能量、buff、意图、手牌）。
        /// UI 侧收到后只置脏位、帧末合并刷新，所以这里可以放心地"多抛"。
        /// </summary>
        public event System.Action Changed;

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }

        private int m_PlayerHp;

        /// <summary>本回合已经结算过多少次「瞬发」（闪电破击这类牌要用）。</summary>
        public int InstantCountThisTurn { get; private set; }

        /// <summary>等待玩家选一张手牌（条件反射）。</summary>
        public bool HasPendingChoice => PendingChoice != ChoiceKind.None;

        public ChoiceKind PendingChoice { get; private set; }

        /// <summary>进入「选一张手牌」状态。</summary>
        public void BeginChoice(ChoiceKind kind)
        {
            PendingChoice = kind;
        }

        /// <summary>当前被选中的那张手牌（还没确认）。</summary>
        public CardInstance SelectedForChoice { get; private set; }

        /// <summary>选一张手牌（只记录，等确认）。</summary>
        public bool SelectForChoice(CardInstance card)
        {
            if (PendingChoice == ChoiceKind.None || card == null)
            {
                return false;
            }

            SelectedForChoice = card;
            return true;
        }

        /// <summary>确认选择，真正执行。</summary>
        public bool ConfirmChoice()
        {
            if (PendingChoice == ChoiceKind.None || SelectedForChoice == null)
            {
                return false;
            }

            ChoiceKind kind = PendingChoice;
            CardInstance card = SelectedForChoice;

            PendingChoice = ChoiceKind.None;
            SelectedForChoice = null;

            if (kind == ChoiceKind.FlipHandCard)
            {
                RequestFlip(card, FlipSource.Effect);
            }

            return true;
        }

        public void ClearChoice()
        {
            PendingChoice = ChoiceKind.None;
            SelectedForChoice = null;
        }

        /// <summary>玩家身上的 buff。</summary>
        public BuffContainer PlayerBuffs { get; } = new BuffContainer();

        private BuffDefinition[] m_BuffPool;

        /// <summary>按 id 找 buff 定义。</summary>
        public BuffDefinition FindBuff(int id)
        {
            if (m_BuffPool == null)
            {
                BattleContent content = UnityEngine.Resources.Load<BattleContent>("BattleContent");
                m_BuffPool = content != null ? content.Buffs : null;
            }

            if (m_BuffPool == null)
            {
                return null;
            }

            foreach (BuffDefinition buff in m_BuffPool)
            {
                if (buff != null && buff.Id == id)
                {
                    return buff;
                }
            }

            return null;
        }

        /// <summary>按内部名找 buff 定义（意图表里用名字更直观）。</summary>
        public BuffDefinition FindBuffByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (m_BuffPool == null)
            {
                FindBuff(0);
            }

            if (m_BuffPool == null)
            {
                return null;
            }

            foreach (BuffDefinition buff in m_BuffPool)
            {
                if (buff != null && string.Equals(buff.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return buff;
                }
            }

            return null;
        }

        /// <summary>给玩家或敌人施加 buff（卡牌效果、意图、脚本都走这里）。</summary>
        public void ApplyBuff(BuffContainer target, int buffId, int stacks, int turns)
        {
            BuffDefinition def = FindBuff(buffId);
            if (def == null || target == null)
            {
                return;
            }

            target.Add(def, Math.Max(1, stacks), turns);
        }

        /// <summary>
        /// 数值预览：把「这张牌现在打出去会是多少」算出来给界面显示。
        /// 和结算走同一套 <see cref="ApplyBuffModifiers"/>，所以显示值和实际值不会打架。
        /// </summary>
        public readonly struct ValuePreview
        {
            public ValuePreview(int value, int baseValue)
            {
                Value = value;
                BaseValue = baseValue;
            }

            public int Value { get; }
            public int BaseValue { get; }

            /// <summary>和卡面原值不同（界面据此标红）。</summary>
            public bool Modified => Value != BaseValue;
        }

        /// <summary>这张牌打出去的实际伤害；给了 target 就把目标的易伤也算进去。</summary>
        public ValuePreview PreviewPlayerDamage(Data.CardFace face, EnemyInstance target)
        {
            int baseValue = face.Damage;
            int value = ApplyBuffModifiers(baseValue, "damage_dealt", PlayerBuffs);

            if (target != null && !target.IsDead)
            {
                // 目标侧的减伤/易伤（预览不能消耗层数）
                value = ApplyBuffModifiers(value, "damage_taken", target.Buffs);
            }

            return new ValuePreview(value, baseValue);
        }

        /// <summary>这张牌打出去的护甲（目前没有护甲类 buff，预留同样的接口）。</summary>
        public ValuePreview PreviewPlayerArmor(Data.CardFace face)
        {
            int baseValue = face.Armor;
            int value = ApplyBuffModifiers(baseValue, "armor_dealt", PlayerBuffs);
            return new ValuePreview(value, baseValue);
        }

        /// <summary>敌人这一招打在玩家身上的实际伤害（含敌人攻击加成与玩家易伤/减伤）。</summary>
        public ValuePreview PreviewEnemyAttack(EnemyInstance enemy)
        {
            if (enemy == null || enemy.CurrentIntent == null)
            {
                return new ValuePreview(0, 0);
            }

            int baseValue = enemy.CurrentIntent.Value;
            int value = ApplyBuffModifiers(baseValue + enemy.AttackBonus,
                "damage_dealt", enemy.Buffs);
            value = ApplyBuffModifiers(value, "damage_taken", PlayerBuffs);
            return new ValuePreview(value, baseValue);
        }

        /// <summary>敌人这一招会获得多少护甲。</summary>
        public ValuePreview PreviewEnemyArmor(EnemyInstance enemy)
        {
            if (enemy == null || enemy.CurrentIntent == null)
            {
                return new ValuePreview(0, 0);
            }

            int baseValue = enemy.CurrentIntent.Value;
            return new ValuePreview(baseValue, baseValue);
        }

        /// <summary>
        /// 收集这张牌这一面的「显示用 token」（如 {demage} 的实时值、协同攻击的总伤害）。
        /// 结果已经过玩家 buff 修正（给了 target 还会算目标易伤）。
        /// </summary>
        public void CollectPreviewTokens(Data.CardFace face, EnemyInstance target,
            System.Collections.Generic.IDictionary<string, int> tokens)
        {
            if (tokens == null)
            {
                return;
            }

            CardEffects.CollectPreviewTokens(face, this, target, tokens);

            // 伤害类 token 统一过一遍 buff，保证和实际结算一致
            if (tokens.ContainsKey("total_damage"))
            {
                int value = ApplyBuffModifiers(tokens["total_damage"], "damage_dealt", PlayerBuffs);
                if (target != null && !target.IsDead)
                {
                    value = ApplyBuffModifiers(value, "damage_taken", target.Buffs);
                }

                tokens["total_damage"] = value;
            }

            if (tokens.ContainsKey("demage"))
            {
                int value = ApplyBuffModifiers(tokens["demage"], "damage_dealt", PlayerBuffs);
                if (target != null && !target.IsDead)
                {
                    value = ApplyBuffModifiers(value, "damage_taken", target.Buffs);
                }

                tokens["demage"] = value;
            }

            if (tokens.ContainsKey("total_armor"))
            {
                tokens["total_armor"] = ApplyBuffModifiers(tokens["total_armor"], "armor_dealt", PlayerBuffs);
            }
        }

        /// <summary>
        /// 按某个 stat 的修饰器修正数值：先叠加 + / -，再乘以 * 系数。
        /// <paramref name="consume"/> 为真时，参与结算的按次数消耗 buff 会掉一层。
        /// </summary>
        public static int ApplyBuffModifiers(int amount, string stat, BuffContainer source,
            bool consume = false)
        {
            if (source == null || source.Buffs.Count == 0)
            {
                return amount;
            }

            float value = (amount + source.Flat(stat)) * source.Factor(stat);
            int result = System.Math.Max(0, (int)System.Math.Round(value));

            if (consume)
            {
                source.ConsumeByStat(stat);
            }

            return result;
        }

        /// <summary>
        /// 随机挑一个活着的敌人。
        /// 设计规则：任何「自动使用/间接释放」的卡牌，若它需要目标，
        /// 一律随机选一个敌人，而不是让目标为空导致效果静默不生效。
        /// </summary>
        public EnemyInstance PickRandomAliveEnemy()
        {
            int alive = 0;
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy != null && !enemy.IsDead)
                {
                    alive++;
                }
            }

            if (alive == 0)
            {
                return null;
            }

            int pick = UnityEngine.Random.Range(0, alive);
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                if (pick == 0)
                {
                    return enemy;
                }

                pick--;
            }

            return null;
        }

        /// <summary>
        /// 自动使用时决定目标：优先用已有目标，失效或没有就随机一个。
        /// 只对「需要目标」的面做这个处理，不需要目标的保持原样。
        /// </summary>
        public EnemyInstance ResolveAutoTarget(CardFace face, EnemyInstance preferred)
        {
            if (!face.NeedsTarget)
            {
                return preferred;
            }

            if (preferred != null && !preferred.IsDead)
            {
                return preferred;
            }

            return PickRandomAliveEnemy();
        }

        /// <summary>第一个活着的敌人（触发型 buff 需要一个默认目标）。</summary>
        public EnemyInstance FirstAliveEnemy()
        {
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy != null && !enemy.IsDead)
                {
                    return enemy;
                }
            }

            return null;
        }

        /// <summary>跑某个容器上指定触发时机的 buff 效果。</summary>
        private void RunTriggers(BuffContainer owner, Data.BuffTrigger trigger, EnemyInstance target,
            EnemyInstance enemyOwner = null, CardInstance self = null)
        {
            if (owner == null)
            {
                return;
            }

            foreach (BuffInstance buff in owner.WithTrigger(trigger))
            {
                // 有阈值就累计，没到就等下回
                if (!owner.TickCounter(buff))
                {
                    continue;
                }

                owner.ResetCounter(buff);

                switch (buff.Definition.TriggerAction)
                {
                    case Data.BuffTriggerAction.FlipSelf:
                        if (enemyOwner != null)
                        {
                            FlipEnemy(enemyOwner);
                        }
                        else if (self != null)
                        {
                            RequestFlip(self, FlipSource.Effect);
                        }

                        break;

                    case Data.BuffTriggerAction.Stun:
                        if (enemyOwner != null)
                        {
                            enemyOwner.Stunned = true;
                        }

                        break;

                    case Data.BuffTriggerAction.StunAndFlip:
                        if (enemyOwner != null)
                        {
                            enemyOwner.Stunned = true;
                            FlipEnemy(enemyOwner);
                        }

                        break;
                }

                string spec = buff.Definition.TriggerEffect;

                // 「自愈」：回血量 = 层数，执行后清零（狼人的 2 层自愈 = 回 2 点）。
                if (enemyOwner != null &&
                    string.Equals(spec, "heal_by_stacks", StringComparison.OrdinalIgnoreCase))
                {
                    int healAmount = buff.Stacks;
                    if (healAmount > 0)
                    {
                        enemyOwner.Heal(healAmount);
                    }

                    owner.Consume(buff, buff.Stacks);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(spec))
                {
                    var context = new CardEffectContext
                    {
                        State = this,
                        Card = null,
                        Target = target,
                        SelfEnemy = enemyOwner,
                        Face = new CardFace(buff.Definition.DisplayName, 0, CardType.Skill, 0, 0,
                            buff.Definition.Description, spec, null, null),
                    };

                    m_DeferDamage = true;
                    try
                    {
                        CardEffects.ExecuteAll(context);
                    }
                    finally
                    {
                        m_DeferDamage = false;
                    }
                }

                if (buff.Definition.Consume == Data.BuffConsume.Count)
                {
                    owner.Consume(buff);
                }
            }

            // 触发跑完之后按数据配置衰减（decayTrigger == 当前时机就掉层）
            owner.DecayByTrigger(trigger);
        }

        private int m_PlayerArmor;
        private int m_Energy;

        public int PlayerHp
        {
            get => m_PlayerHp;
            private set
            {
                if (m_PlayerHp == value) return;
                m_PlayerHp = value;
                PlayerHpChanged?.Invoke(value);
                RaiseChanged();
            }
        }


        public int PlayerArmor
        {
            get => m_PlayerArmor;
            private set
            {
                if (m_PlayerArmor == value) return;
                m_PlayerArmor = value;
                PlayerArmorChanged?.Invoke(value);
                RaiseChanged();
            }
        }

        public int Energy
        {
            get => m_Energy;
            private set
            {
                if (m_Energy == value) return;
                m_Energy = value;
                EnergyChanged?.Invoke(value);
                RaiseChanged();
            }
        }
        public bool IsPlayerTurn { get; private set; }

        /// <summary>当前是第几个玩家回合（从 1 开始）。</summary>
        public int TurnNumber { get; private set; }
        public bool IsVictory { get; private set; }
        public bool IsDefeat { get; private set; }
        public bool IsBattleOver => IsVictory || IsDefeat;

        /// <summary>敌人被翻面时触发，参数为该敌人。</summary>
        public event Action<EnemyInstance> EnemyFlipped;

        /// <summary>战斗结束（胜利或失败）时触发。</summary>
        public event Action<bool> BattleEnded;

        /// <summary>一次伤害真正落地时触发（延迟扣血后的提交点）。</summary>
        public event Action<EnemyInstance, int> DamageApplied;

        /// <summary>敌人死亡。</summary>
        public event Action<EnemyInstance> EnemyDied;

        /// <summary>玩家数值变化。</summary>
        public event Action<int> PlayerHpChanged;
        public event Action<int> PlayerArmorChanged;
        public event Action<int> EnergyChanged;

        /// <summary>手牌增减。</summary>
        public event Action<CardInstance> CardDrawn;
        public event Action<CardInstance> CardDiscarded;

        /// <summary>回合开始 / 结束。</summary>
        public event Action<int> TurnStarted;
        public event Action TurnEnded;

        /// <summary>「瞬发」被触发。</summary>
        public event Action<CardInstance> InstantTriggered;

        private EnemyIntentDefinition[] m_IntentPool;

        public void Setup(IEnumerable<CardDefinition> cards, IEnumerable<EnemyDefinition> enemies)
        {
            m_DrawPile.Clear();
            m_Hand.Clear();
            m_DiscardPile.Clear();
            m_Enemies.Clear();

            // 新一场：贪婪计数器归零（卡牌标记跟着 CardInstance 一起重建，天然清空）
            m_PlaysThisTurn = 0;

            foreach (CardDefinition card in cards)
            {
                var instance = new CardInstance(card);
                instance.Flipped += OnCardFlipped;
                m_DrawPile.Add(instance);
            }

            // 招式池来自数据表，一次读入并缓存
            BattleContent content = UnityEngine.Resources.Load<BattleContent>("BattleContent");
            m_IntentPool = content != null ? content.EnemyIntents : null;

            foreach (EnemyDefinition enemy in enemies)
            {
                var instance = new EnemyInstance(enemy);
                instance.Armor = enemy.FrontArmor;
                ApplyFormBuffs(instance);            // 怪物特性 = 初始 buff
                instance.RefillIntents(IntentsFor(instance));
                m_Enemies.Add(instance);

                // 敌人身上的任何数值变化都上抛给 UI（响应式刷新的数据源）
                instance.HpChanged += _ => RaiseChanged();
                instance.ArmorChanged += _ => RaiseChanged();
                instance.IntentChanged += _ => RaiseChanged();
                instance.Buffs.Changed += _ => RaiseChanged();
            }

            PlayerBuffs.Changed += _ => RaiseChanged();

            // 开幕特性：初始 buff 挂完之后跑一次（给玩家虚弱、给自己力量/荆棘等）
            foreach (EnemyInstance enemy in m_Enemies)
            {
                RunTriggers(enemy.Buffs, Data.BuffTrigger.BattleStart, null, enemy);
            }

            Shuffle(m_DrawPile);
            PlayerHp = PlayerMaxHp;
            PlayerArmor = 0;
            Energy = 0;
            IsVictory = false;
            IsDefeat = false;
            IsPlayerTurn = false;
            TurnEnded?.Invoke();

            RunTriggers(PlayerBuffs, Data.BuffTrigger.TurnEnd, PickRandomAliveEnemy());

            // 「若攻击未对玩家造成伤害」这类特性：在敌人回合结束后判定
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy == null || enemy.IsDead || enemy.DealtDamageThisTurn)
                {
                    continue;
                }

                RunTriggers(enemy.Buffs, Data.BuffTrigger.NoDamageDealt, null, enemy);
            }
            foreach (EnemyInstance e in m_Enemies)
            {
                RunTriggers(e.Buffs, Data.BuffTrigger.TurnEnd, PickRandomAliveEnemy());
            }
        }

        public void StartPlayerTurn()
        {
            if (IsBattleOver)
            {
                return;
            }

            IsPlayerTurn = true;
            InstantCountThisTurn = 0;
            PendingChoice = ChoiceKind.None;
            SelectedForChoice = null;
            m_PlaysThisTurn = 0;
            TurnNumber++;
            TurnStarted?.Invoke(TurnNumber);
            Energy = MaxEnergy;
            // 护甲清零是一条可被 buff 拦住的规则（壁垒 buff 就是拦它）
            if (!PlayerBuffs.BlocksRule("armor_clear"))
            {
                PlayerArmor = 0;
            }

            // buff：回合开始触发，然后扣掉按回合消耗的层数
            RunTriggers(PlayerBuffs, Data.BuffTrigger.TurnStart, PickRandomAliveEnemy());

            // 敌人自己的回合开始触发（自愈等挂在自己身上的 buff 靠这里结算）
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                RunTriggers(enemy.Buffs, Data.BuffTrigger.TurnStart, null, enemy);
            }

            // 敌人护甲同样每回合清零
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                if (!enemy.Buffs.BlocksRule("armor_clear"))
                {
                    enemy.Armor = 0;
                }

                if (enemy.CurrentIntent == null)
                {
                    enemy.RefillIntents(IntentsFor(enemy));
                }
            }

            ResolveAbsorbBacklash();
            RefreshGreedBuff();
            DrawUpTo(HandSize);
        }

        /// <summary>护甲/生命类 buff id（表里约定，改表时要同步）。</summary>
        private const int BuffAbsorb = 10;
        private const int BuffGreed = 27;
        private const int BuffPrice = 28;
        private const int BuffAbsorbBacklash = 29;
        private const int BuffStrengthDown = 30;
        private const int BuffFreeHand = 31;

        /// <summary>本回合已经打出过几张牌（贪婪的计数器，每回合清零）。</summary>
        private int m_PlaysThisTurn;

        /// <summary>场上是否还有活着的敌人带着某个 buff。</summary>
        public bool AnyAliveEnemyHasBuff(int buffId)
        {
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy != null && !enemy.IsDead && enemy.Buffs.Has(buffId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>场上活着的敌人身上「贪婪」的总层数 = 每回合的免费牌张数。</summary>
        public int GreedStacks
        {
            get
            {
                int total = 0;
                foreach (EnemyInstance enemy in m_Enemies)
                {
                    if (enemy == null || enemy.IsDead)
                    {
                        continue;
                    }

                    foreach (BuffInstance buff in enemy.Buffs.Buffs)
                    {
                        if (buff.Definition != null && buff.Definition.Id == BuffGreed)
                        {
                            total += buff.Stacks;
                        }
                    }
                }

                return total;
            }
        }

        /// <summary>玩家手上是否还有「费用归零」buff（UI 据此把手牌显示成 0）。</summary>
        public bool FreeCardAvailable => PlayerBuffs.Has(BuffFreeHand);

        /// <summary>
        /// 实际要付的费用：卡面费用 + 玩家身上所有 cost 修饰（下限 0）。
        /// 「贪婪」给玩家挂的 31 号 buff 就是 cost:-99，所以这里自然变成 0。
        /// </summary>
        public int EffectiveCost(CardFace face)
        {
            float raw = face.Cost + PlayerBuffs.Flat("cost");
            int cost = (int)System.Math.Round(raw);
            return cost < 0 ? 0 : cost;
        }

        /// <summary>
        /// 每回合开始时按「贪婪」层数给玩家补上费用归零 buff。
        /// </summary>
        private void RefreshGreedBuff()
        {
            int allowance = GreedStacks;
            if (allowance <= 0)
            {
                PlayerBuffs.Remove(BuffFreeHand);
                return;
            }

            if (!PlayerBuffs.Has(BuffFreeHand))
            {
                BuffDefinition freeHand = FindBuff(BuffFreeHand);
                if (freeHand != null)
                {
                    PlayerBuffs.Add(freeHand, 1, -1);
                }
            }
        }

        /// <summary>
        /// 出牌后推进贪婪的计数器：本回合打出的张数达到层数就把费用归零 buff 摘掉。
        /// </summary>
        private void TickGreedCounter()
        {
            int allowance = GreedStacks;
            if (allowance <= 0)
            {
                PlayerBuffs.Remove(BuffFreeHand);
                return;
            }

            if (m_PlaysThisTurn >= allowance)
            {
                PlayerBuffs.Remove(BuffFreeHand);
            }
        }

        /// <summary>「代价」：梅菲斯特回 3 点血，玩家吃 3 点伤害（先扣护甲）。</summary>
        private void ApplyPriceOnce()
        {
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy == null || enemy.IsDead || !enemy.Buffs.Has(BuffPrice))
                {
                    continue;
                }

                enemy.Heal(3);

                int absorbed = Math.Min(PlayerArmor, 3);
                PlayerArmor -= absorbed;
                int toHp = 3 - absorbed;
                if (toHp > 0)
                {
                    PlayerHp = Math.Max(0, PlayerHp - toHp);
                }

                if (PlayerHp <= 0)
                {
                    IsDefeat = true;
                    BattleEnded?.Invoke(false);
                }

                return;
            }
        }

        /// <summary>
        /// 吸收反噬：把本回合吸收的伤害按 4:1 换算成下回合的力量损失。
        /// 在玩家回合开始时结算（此时敌人在上一回合攒的吸收量已经确定）。
        /// </summary>
        private void ResolveAbsorbBacklash()
        {
            BuffDefinition strengthDown = FindBuff(BuffStrengthDown);
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                int absorbed = enemy.AbsorbedThisTurn;
                enemy.AbsorbedThisTurn = 0;

                if (absorbed <= 0 || !enemy.Buffs.Has(BuffAbsorbBacklash) || strengthDown == null)
                {
                    continue;
                }

                int loss = absorbed / 4;
                if (loss <= 0)
                {
                    continue;
                }

                enemy.Buffs.Add(strengthDown, loss, 1);
            }
        }

        /// <summary>结束玩家回合：手牌全部进弃牌堆（Q2），然后敌人行动。</summary>
        public void EndPlayerTurn()
        {
            if (!IsPlayerTurn || IsBattleOver || IsResolving)
            {
                return;
            }

            IsPlayerTurn = false;
            TurnEnded?.Invoke();

            // 「保留」的牌回合结束时不进弃牌堆，留在手上（关键词机制，见 keywords 列）
            for (int i = m_Hand.Count - 1; i >= 0; i--)
            {
                CardInstance card = m_Hand[i];
                if (card.RetainedThisTurn || card.Face.HasKeyword(KeywordMechanics.Keep))
                {
                    continue;
                }

                m_Hand.RemoveAt(i);
                m_DiscardPile.Add(card);
                CardDiscarded?.Invoke(card);
            }
        }

        /// <summary>所有存活敌人依次攻击玩家，护甲逐个抵扣。</summary>
        /// <summary>敌人这段攻击每段的基础伤害（已含自身攻击加成）。</summary>
        public int EnemyAttackPerHit(EnemyInstance enemy)
        {
            if (enemy == null || enemy.CurrentIntent == null)
            {
                return 0;
            }

            return ApplyBuffModifiers(enemy.CurrentIntent.Value + enemy.AttackBonus,
                "damage_dealt", enemy.Buffs);
        }

        /// <summary>本回合这段攻击总共打几段（含嗜血类累积的额外段数，调用即清零）。</summary>
        public int ConsumeEnemyHitCount(EnemyInstance enemy)
        {
            if (enemy == null || enemy.CurrentIntent == null)
            {
                return 0;
            }

            return Math.Max(1, enemy.CurrentIntent.Hits) + enemy.ConsumeBonusHits();
        }

        /// <summary>
        /// 敌人的一段攻击命中玩家：护甲先吸收，剩下的扣生命。
        /// 返回实际掉的生命值（护甲全挡住时是 0）。
        /// 表现层逐段调用这个，才能做到「每次伤害后立刻刷新血条」。
        /// </summary>
        public int ApplyEnemyAttackHit(EnemyInstance enemy, int damage)
        {
            if (damage <= 0 || IsBattleOver)
            {
                return 0;
            }

            int raw = ApplyBuffModifiers(damage, "damage_taken", PlayerBuffs, true);
            int absorbed = Math.Min(PlayerArmor, raw);
            PlayerArmor -= absorbed;
            int toHp = raw - absorbed;
            PlayerHp = Math.Max(0, PlayerHp - toHp);

            if (toHp > 0)
            {
                RunTriggers(enemy.Buffs, Data.BuffTrigger.DealtUnblocked, null, enemy);
            }

            if (PlayerHp <= 0)
            {
                IsDefeat = true;
                BattleEnded?.Invoke(false);
            }

            return toHp;
        }

        public List<string> ResolveEnemyTurn(bool skipAttackDamage = false)
        {
            var log = new List<string>();
            if (IsBattleOver)
            {
                return log;
            }

            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                enemy.DealtDamageThisTurn = false;

                if (enemy.Stunned)
                {
                    enemy.Stunned = false;
                    log.Add($"{enemy.Form.DisplayName} 被晕眩，本回合不行动");
                    continue;
                }

                Data.EnemyIntentDefinition intent = enemy.CurrentIntent;
                if (intent == null)
                {
                    continue;
                }

                switch (intent.Intent)
                {
                    case Data.EnemyIntentType.Attack:
                    {
                        int perHit = ApplyBuffModifiers(intent.Value + enemy.AttackBonus,
                            "damage_dealt", enemy.Buffs);
                        int hpBefore = PlayerHp;
                        bool unblocked = false;

                        if (!skipAttackDamage)
                        {
                            // 嗜血类 buff 累积的额外段数，打完即清零
                            int hits = intent.Hits + enemy.ConsumeBonusHits();
                            for (int hit = 0; hit < hits; hit++)
                            {
                                int raw = ApplyBuffModifiers(perHit, "damage_taken", PlayerBuffs, true);
                                int absorbed = Math.Min(PlayerArmor, raw);
                                PlayerArmor -= absorbed;
                                int toHp = raw - absorbed;
                                PlayerHp = Math.Max(0, PlayerHp - toHp);
                                if (toHp > 0)
                                {
                                    unblocked = true;
                                }

                                log.Add($"{enemy.Form.DisplayName} 造成 {raw} 点伤害（护甲吸收 {absorbed}，生命 -{toHp}）");

                                if (PlayerHp <= 0)
                                {
                                    IsDefeat = true;
                                    BattleEnded?.Invoke(false);
                                    return log;
                                }
                            }

                            enemy.DealtDamageThisTurn = PlayerHp < hpBefore;
                        }
                        else
                        {
                            // 伤害已经由表现层逐段结算（为了血条实时刷新），这里只补附带 buff
                            enemy.DealtDamageThisTurn = true;
                        }

                        // 攻击附带 buff（如浮士德「贪婪一击」：造成伤害并 +1 贪婪）
                        if (intent.BuffId != 0)
                        {
                            BuffDefinition attached = FindBuff(intent.BuffId);
                            if (attached != null)
                            {
                                if (intent.BuffTarget == BuffTarget.AllAllies)
                                {
                                    foreach (EnemyInstance ally in m_Enemies)
                                    {
                                        if (ally != null && !ally.IsDead)
                                        {
                                            ally.Buffs.Add(attached, intent.BuffStacks, intent.Turns);
                                        }
                                    }
                                }
                                else if (intent.BuffTarget == BuffTarget.Self)
                                {
                                    enemy.Buffs.Add(attached, intent.BuffStacks, intent.Turns);
                                }
                                else
                                {
                                    PlayerBuffs.Add(attached, intent.BuffStacks, intent.Turns);
                                }

                                log.Add($"{enemy.Form.DisplayName} 附带 {attached.DisplayName} x{intent.BuffStacks}");
                            }
                        }

                        // 「造成未格挡的伤害后」类特性（疯狂 / 嗜血）
                        if (unblocked)
                        {
                            RunTriggers(enemy.Buffs, Data.BuffTrigger.DealtUnblocked, null, enemy);
                        }

                        break;
                    }

                    case Data.EnemyIntentType.Stun:
                        enemy.Stunned = true;
                        log.Add($"{enemy.Form.DisplayName} 准备晕眩");
                        break;

                    case Data.EnemyIntentType.FlipSelf:
                        FlipEnemy(enemy);
                        log.Add($"{enemy.Form.DisplayName} 翻面");
                        break;

                    case Data.EnemyIntentType.Defend:
                        enemy.Armor += intent.Value;
                        log.Add($"{enemy.Form.DisplayName} 获得 {intent.Value} 点护甲");
                        break;

                    case Data.EnemyIntentType.ApplyBuff:
                    {
                        BuffDefinition buff = FindBuff(intent.BuffId);
                        if (buff == null)
                        {
                            log.Add($"{enemy.Form.DisplayName} 施加了未知 buff {intent.BuffId}");
                            break;
                        }

                        if (intent.BuffTarget == BuffTarget.AllAllies)
                        {
                            int stacks = Math.Max(1, intent.Value);
                            foreach (EnemyInstance ally in m_Enemies)
                            {
                                if (ally == null || ally.IsDead)
                                {
                                    continue;
                                }

                                ally.Buffs.Add(buff, stacks, intent.Turns);
                            }

                            log.Add($"{enemy.Form.DisplayName} 为全体友方施加 {buff.DisplayName} x{stacks}");
                            break;
                        }

                        bool onSelf = intent.BuffTarget == BuffTarget.Self;
                        BuffContainer target = onSelf ? enemy.Buffs : PlayerBuffs;
                        target.Add(buff, Math.Max(1, intent.Value), intent.Turns);
                        log.Add($"{enemy.Form.DisplayName} {(onSelf ? "获得" : "施加")} {buff.DisplayName} x{Math.Max(1, intent.Value)}");
                        break;
                    }

                    case Data.EnemyIntentType.Heal:
                    {
                        int amount = Math.Max(1, intent.Value);
                        if (intent.BuffTarget == BuffTarget.AllAllies)
                        {
                            foreach (EnemyInstance ally in m_Enemies)
                            {
                                if (ally == null || ally.IsDead)
                                {
                                    continue;
                                }

                                ally.Heal(amount);
                            }

                            log.Add($"{enemy.Form.DisplayName} 为全体友方恢复 {amount} 点生命");
                            break;
                        }

                        enemy.Heal(amount);
                        log.Add($"{enemy.Form.DisplayName} 恢复 {amount} 点生命");
                        break;
                    }

                    case Data.EnemyIntentType.BuffAllies:
                    {
                        BuffDefinition buff = FindBuff(intent.BuffId);
                        if (buff == null)
                        {
                            log.Add($"{enemy.Form.DisplayName} 施加了未知 buff {intent.BuffId}");
                            break;
                        }

                        int stacks = Math.Max(1, intent.Value);
                        foreach (EnemyInstance ally in m_Enemies)
                        {
                            if (ally == null || ally.IsDead)
                            {
                                continue;
                            }

                            ally.Buffs.Add(buff, stacks, intent.Turns);
                        }

                        log.Add($"{enemy.Form.DisplayName} 强化全体友方：{buff.DisplayName} x{stacks}");
                        break;
                    }

                    case Data.EnemyIntentType.Buff:
                    {
                        BuffDefinition strength = FindBuffByName("Strength");
                        if (strength != null)
                        {
                            enemy.Buffs.Add(strength, Math.Max(1, intent.Value), -1);
                        }

                        log.Add($"{enemy.Form.DisplayName} 攻击 +{intent.Value}");
                        break;
                    }

                    case Data.EnemyIntentType.Debuff:
                    {
                        BuffDefinition vuln = FindBuffByName("Vulnerable");
                        if (vuln != null)
                        {
                            PlayerBuffs.Add(vuln, Math.Max(1, intent.Value), -1);
                        }

                        log.Add($"{enemy.Form.DisplayName} 使你易伤 +{intent.Value}");
                        break;
                    }
                }

                // 招式用掉，轮到队列里的下一个
                enemy.PopIntent();
                if (enemy.CurrentIntent == null)
                {
                    enemy.RefillIntents(IntentsFor(enemy));
                }
            }

            if (AllEnemiesDead())
            {
                IsVictory = true;
                BattleEnded?.Invoke(true);
            }

            return log;
        }

        /// <summary>
        /// 敌人受到一次伤害之后：跑「受击」触发，并检查护甲是否被打空。
        /// 反应型特性（累计受击 N 次翻面、护甲归零翻面）都挂在这里。
        /// </summary>
        private void OnEnemyDamaged(EnemyInstance enemy)
        {
            if (enemy == null)
            {
                return;
            }

            bool armorWas = enemy.Armor > 0;
            RunTriggers(enemy.Buffs, Data.BuffTrigger.Damaged, null, enemy);

            if (armorWas && enemy.Armor <= 0)
            {
                RunTriggers(enemy.Buffs, Data.BuffTrigger.ArmorBroken, null, enemy);
            }
        }

        /// <summary>
        /// 敌人翻面后的统一处理。
        /// 默认正反 buff 不共享（清空后加载新面初始 buff）；
        /// 双生（twin）的敌人共用血量和 buff，翻面时两者都不动。
        /// </summary>
        private void OnEnemyFlipped(EnemyInstance enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (!enemy.Definition.Twin)
            {
                enemy.Buffs.Clear();
                ApplyFormBuffs(enemy);

                // 反面自己的「开幕」特性：换面即生效（例如呼啸山庄的开幕力量+荆棘）
                RunTriggers(enemy.Buffs, Data.BuffTrigger.BattleStart, null, enemy);
            }

            enemy.RefillIntents(IntentsFor(enemy));
            EnemyFlipped?.Invoke(enemy);
        }

        /// <summary>由 buff（如「累计受击 3 次翻面」）触发的强制翻面。</summary>
        public bool FlipEnemy(EnemyInstance enemy)
        {
            if (enemy == null || enemy.IsDead)
            {
                return false;
            }

            if (!enemy.ForceFlip())
            {
                return false;
            }

            OnEnemyFlipped(enemy);
            return true;
        }

        /// <summary>按当前形态挂上初始 buff（怪物特性都走这里）。</summary>
        private void ApplyFormBuffs(EnemyInstance enemy)
        {
            string spec = enemy.Side == CardFaceSide.Front
                ? enemy.Definition.FrontBuffs
                : enemy.Definition.BackBuffs;

            if (string.IsNullOrWhiteSpace(spec))
            {
                return;
            }

            // 写法：buffId:层数:回合，多条用分号
            foreach (string token in spec.Split(';'))
            {
                string entry = token.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                string[] parts = entry.Split(':');
                if (!int.TryParse(parts[0].Trim(), out int buffId))
                {
                    UnityEngine.Debug.LogWarning("[敌人] 初始 buff 写法不对：" + entry);
                    continue;
                }

                int stacks = parts.Length > 1 && int.TryParse(parts[1].Trim(), out int s) ? s : 1;
                int turns = parts.Length > 2 && int.TryParse(parts[2].Trim(), out int tr) ? tr : -1;
                ApplyBuff(enemy.Buffs, buffId, stacks, turns);
            }
        }

        /// <summary>取某个敌人当前形态的招式池。</summary>
        private IEnumerable<EnemyIntentDefinition> IntentsFor(EnemyInstance enemy)
        {
            if (m_IntentPool == null || m_IntentPool.Length == 0)
            {
                return Array.Empty<EnemyIntentDefinition>();
            }

            CardFaceSide side = enemy.Side;
            int id = enemy.Definition.Id;
            var list = new List<EnemyIntentDefinition>();
            foreach (EnemyIntentDefinition intent in m_IntentPool)
            {
                if (intent != null && intent.EnemyId == id && intent.Form == side)
                {
                    list.Add(intent);
                }
            }

            return list;
        }
        public PlayResult CanPlay(CardInstance card, EnemyInstance target)
        {
            if (IsResolving)
            {
                return PlayResult.Busy;
            }

            if (IsBattleOver)
            {
                return PlayResult.BattleOver;
            }

            if (!IsPlayerTurn)
            {
                return PlayResult.NotPlayerTurn;
            }

            if (!m_Hand.Contains(card))
            {
                return PlayResult.InvalidTarget;
            }

            if (EffectiveCost(card.Face) > Energy)
            {
                return PlayResult.NotEnoughEnergy;
            }

            if (card.Face.NeedsTarget && (target == null || target.IsDead))
            {
                return PlayResult.NeedsTarget;
            }

            return PlayResult.Ok;
        }

        /// <summary>
        /// 打出一张牌：执行这一面的效果 → 能量扣除 → 卡牌翻面并留在手牌（Q1、Q8）。
        /// 效果本身由 <see cref="CardEffects"/> 按表里的效果串分发。
        /// </summary>
        public PlayResult PlayCard(CardInstance card, EnemyInstance target,
            out EnemyInstance flippedEnemy, out bool battleEnded)
        {
            flippedEnemy = null;
            battleEnded = false;
            LastDamageDealt = 0;
            LastDamagedEnemy = null;
            m_LastDamages.Clear();

            PlayResult result = CanPlay(card, target);
            if (result != PlayResult.Ok)
            {
                return result;
            }

            CardFace face = card.Face;

            // 1) 实际要付多少：贪婪给玩家挂的「费用归零」buff 会把 cost 修饰成 0
            int paid = EffectiveCost(face);

            // 2) 这张牌之前是否被「免费打出」标记过（代价据此结算）
            bool markedFree = card.FreePlayed;

            Energy -= paid;

            // 3) 检查这次实际消耗的费用：为 0 就给这张牌打标记（翻面/再打出时结算）
            if (paid == 0 && face.Cost > 0)
            {
                card.FreePlayed = true;
            }

            // 4) 「代价」：标记过的牌再次打出 → 梅菲斯特回血并伤害玩家
            if (markedFree && AnyAliveEnemyHasBuff(BuffPrice))
            {
                ApplyPriceOnce();
            }

            // 5) 贪婪计数器：本回合打出的张数够了就把费用归零 buff 摘掉
            m_PlaysThisTurn++;
            TickGreedCounter();

            var context = new CardEffectContext
            {
                State = this,
                Card = card,
                Target = target,
                Face = face,
            };

            // 出牌期间的伤害（含翻面触发的瞬发）全部先入队，等动画命中再扣血。
            m_DeferDamage = true;
            CardInstance previousCard = EnterCardContext(card);
            try
            {
                RunTriggers(PlayerBuffs, Data.BuffTrigger.CardPlayed, target);
                CardEffects.ExecuteAll(context);
                flippedEnemy = context.FlippedEnemy;

                // 翻面走统一入口：入队，由表现层在动画落地时 Commit。
                RequestFlip(card, FlipSource.Play, target);
            }
            finally
            {
                ExitCardContext(previousCard);
                m_DeferDamage = false;
            }

            if (!IsResolving)
            {
                CheckBattleEnd();
            }

            battleEnded = IsBattleOver;
            return PlayResult.Ok;
        }

        private int m_InstantDepth;

        /// <summary>本次出牌选中的目标，翻面事件处理器用它来结算「瞬发」。</summary>
        private EnemyInstance m_PlayTarget;

        /// <summary>最近一次翻面提交时，瞬发把敌人打翻的结果（表现层据此播敌人翻转）。</summary>
        public EnemyInstance LastInstantFlipEnemy { get; private set; }

        /// <summary>表现层播完敌人翻转后清空，避免重复播。</summary>
        public void ClearLastInstantFlipEnemy()
        {
            LastInstantFlipEnemy = null;
        }

        /// <summary>本次翻面由瞬发造成的结果（是否翻面），供 PlayCard 回传。</summary>
        private EnemyInstance m_LastInstantFlip;

        /// <summary>
        /// 翻面事件处理：翻到的这一面带「瞬发」就结算它。
        /// 这里不关心是谁触发的翻面（出牌、效果、将来的其他来源），统一走这一条路。
        /// </summary>
        private void OnCardFlipped(CardInstance card)
        {
            if (card == null || IsBattleOver)
            {
                return;
            }

            if (!card.Face.HasKeyword(KeywordMechanics.Instant))
            {
                return;
            }

            // 只收集，等翻面动画与自身效果播完再执行（ResolvePendingInstants）
            m_PendingInstants.Add(new PendingInstant(card, m_PlayTarget));
        }

        /// <summary>一次伤害结算的记录：打到谁、多少点。</summary>
        public readonly struct DamageRecord
        {
            public DamageRecord(EnemyInstance enemy, int amount)
            {
                Enemy = enemy;
                Amount = amount;
            }

            public EnemyInstance Enemy { get; }
            public int Amount { get; }
        }

        private readonly List<DamageRecord> m_LastDamages = new List<DamageRecord>();

        /// <summary>
        /// 最近一次 PlayCard 里按发生顺序记录的每一次伤害。
        /// 表现层照这个列表逐条播放（两次瞬发 = 两条记录），逻辑层不做任何动画等待。
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<DamageRecord> LastDamages => m_LastDamages;

        /// <summary>这次出牌里「伤害已经入队、但还没落地」。命中动画播到才提交。</summary>
        public readonly struct PendingHit
        {
            public PendingHit(EnemyInstance enemy, int amount, HitOrigin origin, CardInstance source)
            {
                Enemy = enemy;
                Amount = amount;
                Origin = origin;
                Source = source;
            }

            public EnemyInstance Enemy { get; }
            public int Amount { get; }

            /// <summary>伤害来源：出牌本身，还是翻面触发的瞬发。</summary>
            public HitOrigin Origin { get; }

            /// <summary>
            /// 真正产生这次伤害的卡（出牌 / 该牌的瞬发 / 被使用的邻牌）。
            /// 表现层据此让特效从那张牌出发；为 null 表示没有具体来源。
            /// </summary>
            public CardInstance Source { get; }
        }

        private readonly List<PendingHit> m_Pending = new List<PendingHit>();

        /// <summary>待落地的伤害队列；表现层按它播放，命中时调 CommitNextHit。</summary>
        public System.Collections.Generic.IReadOnlyList<PendingHit> PendingHits => m_Pending;

        /// <summary>是否还有未落地的伤害。为 true 时拒绝一切玩家操作。</summary>
        public bool IsResolving => m_Pending.Count > 0;

        /// <summary>出牌期间为 true：伤害只入队不扣血。</summary>
        private bool m_DeferDamage;

        /// <summary>最近一次 PlayCard 里实际造成的伤害总量（含瞬发效果）。</summary>
        public int LastDamageDealt { get; private set; }

        /// <summary>最近一次 PlayCard 里被打到的敌人（可能来自随机/全体效果）。</summary>
        public EnemyInstance LastDamagedEnemy { get; private set; }

        /// <summary>
        /// 「瞬发」关键词：卡牌翻到此面时立即触发这一面的效果一次。
        /// 触发次数会被光环类关键词修正（例如「双重瞬发」）。
        /// 用深度上限兜底，避免两面互相触发造成死循环。
        /// </summary>
        private EnemyInstance TriggerInstant(CardInstance card, EnemyInstance target)
        {
            if (m_InstantDepth >= 3 || IsBattleOver || card == null)
            {
                return null;
            }

            CardFace face = card.Face;
            if (!face.HasKeyword(KeywordMechanics.Instant))
            {
                return null;
            }

            int repeat = InstantRepeat();
            EnemyInstance flipped = null;

            InstantCountThisTurn++;
            m_InstantDepth++;
            CardInstance previousCard = EnterCardContext(card);
            try
            {
                for (int i = 0; i < repeat; i++)
                {
                    var context = new CardEffectContext
                    {
                        State = this,
                        Card = card,
                        Target = target,
                        Face = face,
                    };

                    CardEffects.ExecuteAll(context);
                    if (context.FlippedEnemy != null)
                    {
                        flipped = context.FlippedEnemy;
                    }

                    if (IsBattleOver)
                    {
                        break;
                    }
                }
            }
            finally
            {
                ExitCardContext(previousCard);
                m_InstantDepth--;
            }

            return flipped;
        }

        /// <summary>瞬发本次要触发几次：基础 1 次，再叠加手牌里所有光环关键词的修正。</summary>
        private int InstantRepeat()
        {
            int repeat = 1;

            foreach (CardInstance card in m_Hand)
            {
                foreach (string keyword in card.Face.Keywords)
                {
                    if (AuraKeywords.TryGet(keyword, out IAuraKeyword aura))
                    {
                        repeat += aura.ModifyInstantRepeat(this, card);
                    }
                }
            }

            return Math.Max(1, Math.Min(5, repeat));
        }

        /// <summary>
        /// 对敌人造成伤害。出牌期间只入队（等动画命中再扣血），
        /// 敌人回合等无动画场合则立即结算。
        /// 返回值只对「立即结算」有意义；入队时返回 false。
        /// </summary>
        public bool DamageEnemy(EnemyInstance enemy, int amount)
        {
            if (enemy == null || enemy.IsDead || amount <= 0)
            {
                return false;
            }

            // 「吸收」：这一下完全吃掉，并记进本回合的吸收量（贤者的吸收反噬要用）
            if (enemy.Buffs.Has(BuffAbsorb))
            {
                enemy.AbsorbedThisTurn += amount;
                return false;
            }

            // 先过 buff：出牌方的伤害加成，以及挨打方的减伤/易伤
            amount = ApplyBuffModifiers(amount, "damage_dealt", PlayerBuffs);
            amount = ApplyBuffModifiers(amount, "damage_taken", enemy.Buffs, true);
            if (amount <= 0)
            {
                return false;
            }

            LastDamageDealt += amount;
            LastDamagedEnemy = enemy;
            m_LastDamages.Add(new DamageRecord(enemy, amount));

            if (m_DeferDamage)
            {
                m_Pending.Add(new PendingHit(enemy, amount,
                    m_InstantPhase ? HitOrigin.Instant : HitOrigin.Play, m_ExecutingCard));
                return false;
            }

            bool flipped = enemy.TakeDamage(amount);
            DamageApplied?.Invoke(enemy, amount);

            if (flipped)
            {
                OnEnemyFlipped(enemy);
            }

            OnEnemyDamaged(enemy);

            if (enemy.IsDead)
            {
                HandleEnemyDeath(enemy);
            }

            return flipped;
        }

        /// <summary>
        /// 敌人死亡的统一处理：先跑「自己的死亡特性」（羁绊）和「友方死亡特性」（灵感），
        /// 再清 buff、跑玩家侧的死亡触发，最后广播事件。
        /// </summary>
        private void HandleEnemyDeath(EnemyInstance enemy)
        {
            if (enemy == null)
            {
                return;
            }

            RunTriggers(enemy.Buffs, Data.BuffTrigger.SelfDeath, null, enemy);

            foreach (EnemyInstance ally in m_Enemies)
            {
                if (ally == null || ReferenceEquals(ally, enemy) || ally.IsDead)
                {
                    continue;
                }

                RunTriggers(ally.Buffs, Data.BuffTrigger.AllyDeath, null, ally);
            }

            enemy.Buffs.Clear();
            RunTriggers(PlayerBuffs, Data.BuffTrigger.Death, enemy);
            EnemyDied?.Invoke(enemy);
            CheckBattleEnd();
        }

        /// <summary>羁绊：翻面与自己同 pairId 的其他敌人。</summary>
        public void FlipPairedAllies(EnemyInstance source)
        {
            if (source == null || source.PairId == 0)
            {
                return;
            }

            foreach (EnemyInstance ally in m_Enemies)
            {
                if (ally == null || ReferenceEquals(ally, source) || ally.IsDead)
                {
                    continue;
                }

                if (ally.PairId != source.PairId)
                {
                    continue;
                }

                FlipEnemy(ally);
            }
        }

        /// <summary>让敌人自己失去生命（不经过护甲、不吃伤害修正），用于「疯狂」这类自伤。</summary>
        public void ApplySelfDamage(EnemyInstance enemy, int amount)
        {
            if (enemy == null || enemy.IsDead || amount <= 0)
            {
                return;
            }

            // 自伤直接扣血，不走 TakeDamage 的护甲吸收
            enemy.LoseHpDirect(amount);
            DamageApplied?.Invoke(enemy, amount);

            if (enemy.IsDead)
            {
                HandleEnemyDeath(enemy);
            }
        }

        /// <summary>敌人自己回血，用于「嗜血」「自愈」。</summary>
        public void ApplySelfHeal(EnemyInstance enemy, int amount)
        {
            enemy?.Heal(amount);
        }

        /// <summary>
        /// 提交队首的那一次伤害：命中动画播到这一下时由表现层调用。
        /// 返回是否真的落了地（敌人已死则跳过）。
        /// </summary>
        public bool CommitNextHit()
        {
            if (m_Pending.Count == 0)
            {
                return false;
            }

            PendingHit hit = m_Pending[0];
            m_Pending.RemoveAt(0);

            if (hit.Enemy != null && !hit.Enemy.IsDead)
            {
                bool flipped = hit.Enemy.TakeDamage(hit.Amount);
                DamageApplied?.Invoke(hit.Enemy, hit.Amount);

                if (flipped)
                {
                    OnEnemyFlipped(hit.Enemy);
                }

                OnEnemyDamaged(hit.Enemy);

                if (hit.Enemy.IsDead)
                {
                    HandleEnemyDeath(hit.Enemy);
                }
            }

            if (m_Pending.Count == 0)
            {
                CheckBattleEnd();
            }

            return true;
        }

        /// <summary>清空未落地的伤害（战斗结束或强制中断时用）。</summary>
        public void ClearPendingHits()
        {
            m_Pending.Clear();
        }

        private void CheckBattleEnd()
        {
            if (IsBattleOver)
            {
                return;
            }

            if (AllEnemiesDead())
            {
                IsVictory = true;
                BattleEnded?.Invoke(true);
            }
            else if (IsDefeat)
            {
                BattleEnded?.Invoke(false);
            }
        }

        /// <summary>获得护甲。</summary>
        public void GainArmor(int amount)
        {
            if (amount > 0)
            {
                PlayerArmor += amount;
            }
        }

        /// <summary>额外抽牌。</summary>
        public void DrawCards(int count)
        {
            if (count > 0)
            {
                DrawUpTo(m_Hand.Count + count);
            }
        }

        /// <summary>治疗玩家，不超过上限。</summary>
        public void HealPlayer(int amount)
        {
            if (amount > 0)
            {
                PlayerHp = Math.Min(PlayerMaxHp, PlayerHp + amount);
            }
        }

        /// <summary>失去生命（可致死）。</summary>
        public void LosePlayerHp(int amount)
        {
            if (amount <= 0 || IsBattleOver)
            {
                return;
            }

            PlayerHp = Math.Max(0, PlayerHp - amount);
            if (PlayerHp <= 0)
            {
                IsDefeat = true;
                BattleEnded?.Invoke(false);
            }
        }

        /// <summary>获得能量。</summary>
        public void GainEnergy(int amount)
        {
            if (amount > 0)
            {
                Energy += amount;
            }
        }

        private bool AllEnemiesDead()
        {
            if (m_Enemies.Count == 0)
            {
                return false;
            }

            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (!enemy.IsDead)
                {
                    return false;
                }
            }

            return true;
        }

        private void DrawUpTo(int targetHandSize)
        {
            while (m_Hand.Count < targetHandSize)
            {
                if (m_DrawPile.Count == 0)
                {
                    ReshuffleDiscardIntoDraw();
                    if (m_DrawPile.Count == 0)
                    {
                        return;
                    }
                }

                CardInstance card = m_DrawPile[0];
                m_DrawPile.RemoveAt(0);
                m_Hand.Add(card);
                CardDrawn?.Invoke(card);
            }

            RaiseChanged();
        }

        private void ReshuffleDiscardIntoDraw()
        {
            if (m_DiscardPile.Count == 0)
            {
                return;
            }

            m_DrawPile.AddRange(m_DiscardPile);
            m_DiscardPile.Clear();
            Shuffle(m_DrawPile);
        }

        private void Shuffle(List<CardInstance> pile)
        {
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = m_Random.Next(i + 1);
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
        }
    }
}
