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

        /// <summary>「保留」直到这张牌再次被打出（深吸 / 剑盾 给状态牌的保留）。</summary>
        public bool RetainUntilPlayed { get; set; }

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

        /// <summary>选一张手牌（状态牌）获得「保留到打出为止」（深吸 / 剑盾）。</summary>
        RetainHandCard,

        /// <summary>从抽牌堆选一张置入手牌（协议）。</summary>
        PickFromDrawPile,

        /// <summary>从弃牌堆选一张置入手牌（追悔）。</summary>
        PickFromDiscardPile,
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

        /// <summary>这次出牌需要玩家先选一张牌（表现层进入选牌流程后再调 CompletePlay）。</summary>
        NeedsChoice,

        /// <summary>卡面的打出条件不满足（如围困要求手牌状态牌 > 3）。</summary>
        ConditionNotMet,
    }

    /// <summary>
    /// 战斗的纯逻辑部分，不依赖任何 UI。表现层只读取状态并播放动画。
    /// 数值全部来自占位配置（Q5）与数据表。
    /// </summary>
    public sealed class BattleState
    {
        /// <summary>规则参数都在 Resources/Config/BattleRules.asset 里配（见 <see cref="BattleRules"/>）。</summary>
        public static int MaxEnergy => BattleRules.Current.EnergyPerTurn;

        /// <summary>每回合抽牌数（抽 N 张，不是补到 N 张）。</summary>
        public static int DrawPerTurn => BattleRules.Current.DrawPerTurn;

        public static int PlayerMaxHp => BattleRules.Current.PlayerMaxHp;

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
                RefreshPowerCards();
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
        /// 执行一条 buff 的「触发动作 + 触发效果」。不关心这个 buff 是不是常驻在容器里，
        /// 所以开幕效果（一次性，不挂 buff）也复用它。
        /// </summary>
        private void ExecuteBuffTrigger(Data.BuffDefinition definition, int stacks,
            EnemyInstance enemyOwner, CardInstance self, EnemyInstance target,
            bool formBound = false)
        {
            if (definition == null)
            {
                return;
            }

            if (formBound)
            {
                m_FormBoundDepth++;
            }

            switch (definition.TriggerAction)
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

            string spec = definition.TriggerEffect;
            if (string.IsNullOrWhiteSpace(spec))
            {
                return;
            }

            var context = new CardEffectContext
            {
                State = this,
                Card = null,
                Target = target,
                SelfEnemy = enemyOwner,
                Face = new CardFace(definition.DisplayName, 0, CardType.Skill, 0, 0,
                    definition.Description, spec, null, null),
            };

            m_DeferDamage = true;
            try
            {
                CardEffects.ExecuteAll(context);
            }
            finally
            {
                m_DeferDamage = false;
                if (formBound)
                {
                    m_FormBoundDepth--;
                }
            }
        }

        /// <summary>能力牌（正面朝上、在手牌里）当前提供的 buff，按牌记录，失效时整条移除。</summary>
        private readonly Dictionary<CardInstance, List<BuffInstance>> m_PowerBuffs =
            new Dictionary<CardInstance, List<BuffInstance>>();

        /// <summary>执行能力牌效果时，把新加的 buff 收集到这里。</summary>
        private List<BuffInstance> m_PowerRecording;

        /// <summary>
        /// 重算「手牌里正面朝上的能力牌」：
        /// 生效的牌执行一次它的效果并记住产生的 buff；不再生效的牌把那些 buff 摘掉。
        /// 能力牌没有能力区——它只要正面朝上躺在手牌里就一直生效。
        /// </summary>
        public void RefreshPowerCards()
        {
            var active = new List<CardInstance>();
            foreach (CardInstance card in m_Hand)
            {
                // 当前面是能力牌就生效（能力面可能长在背面，比如「战斗怒火」）
                if (card != null && card.Face.Type == CardType.Power)
                {
                    active.Add(card);
                }
            }

            // 失效：牌被翻到背面 / 打出 / 弃掉
            if (m_PowerBuffs.Count > 0)
            {
                var stale = new List<CardInstance>();
                foreach (KeyValuePair<CardInstance, List<BuffInstance>> pair in m_PowerBuffs)
                {
                    if (!active.Contains(pair.Key))
                    {
                        stale.Add(pair.Key);
                    }
                }

                foreach (CardInstance card in stale)
                {
                    foreach (BuffInstance buff in m_PowerBuffs[card])
                    {
                        PlayerBuffs.Remove(buff);
                    }

                    m_PowerBuffs.Remove(card);
                }
            }

            // 新生效：执行这一面的效果
            foreach (CardInstance card in active)
            {
                if (m_PowerBuffs.ContainsKey(card))
                {
                    continue;
                }

                var recorded = new List<BuffInstance>();
                m_PowerRecording = recorded;
                try
                {
                    var context = new CardEffectContext
                    {
                        State = this,
                        Card = card,
                        Target = PickRandomAliveEnemy(),
                        Face = card.Face,
                    };

                    CardEffects.ExecuteAll(context);
                }
                finally
                {
                    m_PowerRecording = null;
                }

                m_PowerBuffs[card] = recorded;
            }
        }

        /// <summary>
        /// 相邻牌效果（use_adjacent_*）的嵌套深度。
        /// 两张「暗藏杀意」相邻时会互相使用对方的背面/正面，形成无限递归，
        /// 所以同一条结算链里只允许最外层那一张生效。
        /// </summary>
        private int m_AdjacentUseDepth;

        /// <summary>当前是否允许执行「使用相邻牌」的效果（嵌套层不生效）。</summary>
        public bool CanUseAdjacent => m_AdjacentUseDepth == 0;

        public void BeginAdjacentUse()
        {
            m_AdjacentUseDepth++;
        }

        public void EndAdjacentUse()
        {
            m_AdjacentUseDepth = Math.Max(0, m_AdjacentUseDepth - 1);
        }

        /// <summary>这张牌现在还在手牌里吗（防止效果执行期间被弃掉/挪走）。</summary>
        public bool IsInHand(CardInstance card)
        {
            return card != null && m_Hand.Contains(card);
        }

        /// <summary>
        /// 形态能力作用域深度 > 0 时，期间新加的 buff 一律算「形态能力」
        /// （开幕壁垒、嗜血给的自愈…都归属于那一面，翻面要一起消失）。
        /// </summary>
        private int m_FormBoundDepth;

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

        /// <summary>
        /// 战斗开始时把玩家血量摆到整局进度里的值（RunState.Hp），
        /// 而不是每场都满血——这样离开战斗后的血量才不会被重置掉。
        /// </summary>
        public void BeginPlayerHp(int hp)
        {
            PlayerHp = Math.Min(PlayerMaxHp, Math.Max(1, hp));
        }

        /// <summary>
        /// 整场战斗的清理：牌堆/手牌/敌人/buff/各种队列全部清空，并把所有事件摘掉。
        /// 战斗界面关闭时调用，避免战斗状态（手牌、buff、待结算伤害…）泄露到下一场或别的界面。
        /// </summary>
        public void Clear()
        {
            m_Hand.Clear();
            m_DrawPile.Clear();
            m_DiscardPile.Clear();
            m_ExhaustPile.Clear();
            m_Enemies.Clear();
            m_Pending.Clear();
            m_PendingFlips.Clear();
            m_PendingInstants.Clear();
            m_LastDamages.Clear();

            PlayerBuffs.Clear();
            m_PowerBuffs.Clear();
            m_PowerRecording = null;

            // 结算上下文与开关全部归位
            m_DeferDamage = false;
            m_InstantPhase = false;
            m_InstantDepth = 0;
            m_ExecutingCard = null;
            m_PlayTarget = null;
            m_FlipSource = FlipSource.Effect;
            m_LastInstantFlip = null;
            PendingChoice = ChoiceKind.None;
            SelectedForChoice = null;
            m_PlaysThisTurn = 0;
            InstantCountThisTurn = 0;
            TurnNumber = 0;
            IsPlayerTurn = false;

            // 摘掉所有订阅（UI 的 lambda 也是订阅者，不清会一直吊着界面对象）
            Changed = null;
            PlayerHpChanged = null;
            PlayerArmorChanged = null;
            EnergyChanged = null;
            DamageApplied = null;
            EnemyFlipped = null;
            EnemyDied = null;
            BattleEnded = null;
            TurnStarted = null;
            TurnEnded = null;
            CardDrawn = null;
            CardDiscarded = null;
            InstantTriggered = null;
        }

        /// <summary>
        /// 战斗结束（胜/负）时的自我清理：把「只在战斗中成立」的东西清掉。
        /// 放在状态层而不是界面层，保证无论哪条路径结束战斗，
        /// 奖励 / 路线界面都拿不到力量、虚弱这些残留数值。
        /// 不摘事件（界面还要收 BattleEnded 继续走流程），彻底回收交给 Clear()。
        /// </summary>
        /// <summary>结束战斗的唯一入口：置胜负 → 清理战斗态 → 通知界面。</summary>
        private void FinishBattle(bool victory)
        {
            if (IsBattleOver)
            {
                return;
            }

            if (victory)
            {
                IsVictory = true;
            }
            else
            {
                IsDefeat = true;
            }

            ResetOnBattleEnd();
            BattleEnded?.Invoke(victory);
        }

        private void ResetOnBattleEnd()
        {
            PlayerBuffs.Clear();
            PlayerArmor = 0;
            Energy = 0;

            m_Pending.Clear();
            m_PendingInstants.Clear();
            m_PendingFlips.Clear();
            m_ExhaustPile.Clear();
            m_ForcedPlayDepth = 0;
            m_DrawReleaseDepth = 0;
            m_PowerBuffs.Clear();
            m_PowerRecording = null;

            m_Hand.Clear();
            m_DrawPile.Clear();
            m_DiscardPile.Clear();

            m_DeferDamage = false;
            m_InstantPhase = false;
            m_InstantDepth = 0;
            m_ExecutingCard = null;
            m_PlayTarget = null;
            PendingChoice = ChoiceKind.None;
            SelectedForChoice = null;

            RaiseChanged();
        }

        /// <summary>战斗结束时丢掉没执行的瞬发。</summary>
        public void ClearPendingInstants()
        {
            m_PendingInstants.Clear();
        }

        /// <summary>消耗堆：本场战斗内不再回到抽牌堆。</summary>
        private readonly List<CardInstance> m_ExhaustPile = new List<CardInstance>();

        public IReadOnlyList<CardInstance> ExhaustPile => m_ExhaustPile;

        /// <summary>把一张牌移出战斗（进消耗堆）。</summary>
        public void ExhaustCard(CardInstance card)
        {
            if (card == null)
            {
                return;
            }

            m_Hand.Remove(card);
            m_DrawPile.Remove(card);
            m_DiscardPile.Remove(card);

            if (!m_ExhaustPile.Contains(card))
            {
                m_ExhaustPile.Add(card);
            }

            CardExhausted?.Invoke(card);
            RaiseChanged();
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

            if (!CanSelectForChoice(card))
            {
                return false;
            }

            SelectedForChoice = card;
            return true;
        }

        /// <summary>这张牌现在能不能被选（选牌 UI 用它决定亮/暗）。</summary>
        public bool CanSelectForChoice(CardInstance card)
        {
            if (card == null)
            {
                return false;
            }

            switch (PendingChoice)
            {
                case ChoiceKind.FlipHandCard:
                    // 手牌里任意一张（翻面它）
                    return m_Hand.Contains(card);

                case ChoiceKind.RetainHandCard:
                    // 只能是状态牌
                    return m_Hand.Contains(card) && card.Face.Type == CardType.Status;

                case ChoiceKind.PickFromDrawPile:
                    return m_DrawPile.Contains(card);

                case ChoiceKind.PickFromDiscardPile:
                    return m_DiscardPile.Contains(card);

                default:
                    return false;
            }
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

            switch (kind)
            {
                case ChoiceKind.FlipHandCard:
                    RequestFlip(card, FlipSource.Effect);
                    break;

                case ChoiceKind.RetainHandCard:
                    card.RetainUntilPlayed = true;
                    break;

                case ChoiceKind.PickFromDrawPile:
                    if (m_DrawPile.Remove(card))
                    {
                        m_Hand.Add(card);
                    }

                    break;

                case ChoiceKind.PickFromDiscardPile:
                    if (m_DiscardPile.Remove(card))
                    {
                        m_Hand.Add(card);
                    }

                    break;
            }

            RaiseChanged();
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
        /// <param name="formBound">
        /// 是否是「形态能力」：只在当前这一面朝上时存在，翻面时会被换成新面的能力。
        /// </param>
        public void ApplyBuff(BuffContainer target, int buffId, int stacks, int turns,
            bool formBound = false)
        {
            BuffDefinition def = FindBuff(buffId);
            if (def == null || target == null)
            {
                return;
            }

            target.Add(def, Math.Max(1, stacks), turns);

            if (formBound || m_FormBoundDepth > 0)
            {
                BuffInstance bound = target.Find(buffId);
                if (bound != null)
                {
                    bound.FormBound = true;
                }
            }

            // 能力牌施加的 buff 要记来源，牌离开「正面朝上」时整条移除
            if (m_PowerRecording != null)
            {
                BuffInstance recorded = target.Find(buffId);
                if (recorded != null && !m_PowerRecording.Contains(recorded))
                {
                    m_PowerRecording.Add(recorded);
                }
            }
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

        /// <summary>当前出牌结算中的 X 值（X 费 / {X} 占位用）。</summary>
        public int CurrentX { get; private set; }

        /// <summary>
        /// 这一面的 X 来源：energy（剩余能量）/ status（手牌状态牌数）/ hand（手牌数）。
        /// 没配 x 参数返回 null。
        /// </summary>
        public string XSource(Data.CardFace face)
        {
            if (face.Params != null && face.Params.TryGetValue("x", out string src))
            {
                return src == null ? null : src.Trim().ToLowerInvariant();
            }

            return null;
        }

        /// <summary>按来源算这一面的 X 值。</summary>
        public int EvaluateX(Data.CardFace face)
        {
            string src = XSource(face);
            if (string.IsNullOrEmpty(src))
            {
                return 0;
            }

            switch (src)
            {
                case "energy":
                    return Math.Max(0, Energy);
                case "status":
                    return CountHandOfType(CardType.Status);
                case "hand":
                    return m_Hand.Count;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 卡面能不能打：目前支持 <c>params.require = status&gt;N</c>（围困：手牌状态牌多于 N 张）。
        /// </summary>
        public bool CanPlayFace(Data.CardFace face)
        {
            if (face.Params == null || !face.Params.TryGetValue("require", out string rule) ||
                string.IsNullOrWhiteSpace(rule))
            {
                return true;
            }

            rule = rule.Trim().ToLowerInvariant();
            if (rule.StartsWith("status>"))
            {
                if (int.TryParse(rule.Substring("status>".Length), out int limit))
                {
                    return CountHandOfType(CardType.Status) > limit;
                }
            }

            return true;
        }

        /// <summary>给表现层用的费用查询（含 X 费与 buff 修饰）。</summary>
        public int EffectiveCostFor(Data.CardFace face)
        {
            return EffectiveCost(face);
        }

        /// <summary>这张牌打出去的实际伤害；给了 target 就把目标的易伤也算进去。</summary>
        public ValuePreview PreviewPlayerDamage(Data.CardFace face, EnemyInstance target)
        {
            int baseValue = face.Damage;

            // 力量（attack 修饰器）先加到基础伤害上，和 DamageEnemy 的算法保持一致
            int value = baseValue + (int)PlayerBuffs.Flat("attack");
            value = ApplyBuffModifiers(value, "damage_dealt", PlayerBuffs);

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

            // {x}：描述里显示当前 X 值（X 费牌 X = 当前能量，其余按来源算）
            int x = EvaluateX(face);
            if (!string.IsNullOrEmpty(XSource(face)))
            {
                tokens["x"] = x;
            }

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

                // 「自愈」要按层数回血并清零，单独处理
                if (enemyOwner != null &&
                    string.Equals(buff.Definition.TriggerEffect, "heal_by_stacks",
                        StringComparison.OrdinalIgnoreCase))
                {
                    int healAmount = buff.Stacks;
                    if (healAmount > 0)
                    {
                        enemyOwner.Heal(healAmount);
                    }

                    owner.Consume(buff, buff.Stacks);
                    continue;
                }

                ExecuteBuffTrigger(buff.Definition, buff.Stacks, enemyOwner, self, target, buff.FormBound);

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

        /// <summary>一张牌被消耗（进消耗堆）。</summary>
        public event Action<CardInstance> CardExhausted;

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

            // 玩家自己的回合开始触发（含按回合衰减）。敌人的同类触发在敌人阶段，见 BeginEnemyTurn
            RunTriggers(PlayerBuffs, Data.BuffTrigger.TurnStart, PickRandomAliveEnemy());

            // 敌人招式补齐（护甲清零与回合开始触发在敌人阶段处理）
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                if (enemy.CurrentIntent == null)
                {
                    enemy.RefillIntents(IntentsFor(enemy));
                }
            }

            ResolveAbsorbBacklash();
            RefreshGreedBuff();
            DrawCards(DrawPerTurn);
            RefreshPowerCards();
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
            // X 费牌：费用等于按来源算出来的 X（energy 来源就是当前全部能量）
            if (!string.IsNullOrEmpty(XSource(face)))
            {
                return Math.Max(0, EvaluateX(face));
            }

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
                    FinishBattle(false);
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

                // 层数就是本回合吸收的总伤害（每次受击时累加）
                BuffInstance backlash = enemy.Buffs.Find(BuffAbsorbBacklash);
                int absorbed = backlash != null ? backlash.Stacks : enemy.AbsorbedThisTurn;

                if (backlash != null)
                {
                    enemy.Buffs.Remove(backlash);
                }

                enemy.AbsorbedThisTurn = 0;

                if (absorbed <= 0 || strengthDown == null)
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

            // 玩家回合结束的被动要按「弃牌之前的手牌」结算（佯攻、损伤都数手牌里的状态牌），
            // 所以先跑 TurnEnd 触发，再弃牌。
            RunTriggers(PlayerBuffs, Data.BuffTrigger.TurnEnd, PickRandomAliveEnemy());

            // 「保留」的牌回合结束时不进弃牌堆，留在手上（关键词机制，见 keywords 列）
            for (int i = m_Hand.Count - 1; i >= 0; i--)
            {
                CardInstance card = m_Hand[i];
                if (card.RetainedThisTurn || card.RetainUntilPlayed ||
                    card.Face.HasKeyword(KeywordMechanics.Keep))
                {
                    continue;
                }

                m_Hand.RemoveAt(i);
                m_DiscardPile.Add(card);
                CardDiscarded?.Invoke(card);
            }

            // 一次性「保留」标记只对这一回合有效（关键词「保留」每回合都生效）
            foreach (CardInstance kept in m_Hand)
            {
                kept.RetainedThisTurn = false;
            }

            // 敌人阶段开始：敌人自己的「回合开始」触发（自愈、久睡翻面…）与按回合衰减
            BeginEnemyTurn();
        }

        /// <summary>
        /// 敌人阶段开始：只处理敌人自己的回合开始。
        /// 敌人护甲也在这里清零 —— 它在上个敌人回合获得的护甲要能挡住玩家这个回合。
        /// </summary>
        private void BeginEnemyTurn()
        {
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                enemy.DealtDamageThisTurn = false;
                RunTriggers(enemy.Buffs, Data.BuffTrigger.TurnStart, null, enemy);

                if (!enemy.Buffs.BlocksRule("armor_clear"))
                {
                    enemy.Armor = 0;
                }
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
                FinishBattle(false);
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
                                    FinishBattle(false);
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

            // 敌人回合结束：空挥类特性 + 敌人自己的「回合结束」触发（含按回合衰减）
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                if (!enemy.DealtDamageThisTurn)
                {
                    RunTriggers(enemy.Buffs, Data.BuffTrigger.NoDamageDealt, null, enemy);
                }

                RunTriggers(enemy.Buffs, Data.BuffTrigger.TurnEnd, PickRandomAliveEnemy());
            }

            if (AllEnemiesDead())
            {
                FinishBattle(true);
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

            // 形态能力只属于当前面：换面时把旧面的能力摘掉、挂上新面的能力，
            // 战斗中获得的普通 buff（虚弱/易伤/力量…）保留不动。
            enemy.Buffs.RemoveFormBound();
            ApplyFormBuffs(enemy);

            // 新形态自己的「开幕」特性：换面即生效（例如呼啸山庄的开幕力量+荆棘）
            RunTriggers(enemy.Buffs, Data.BuffTrigger.BattleStart, null, enemy);

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

                Data.BuffDefinition definition = FindBuff(buffId);
                if (definition != null && definition.Trigger == Data.BuffTrigger.BattleStart)
                {
                    // 开幕效果：一次性执行，不留在 buff 容器里
                    // （常驻的话会被双生共享，也会在 buff 栏里占一个图标）
                    ExecuteBuffTrigger(definition, stacks, enemy, null, PickRandomAliveEnemy(), true);
                    continue;
                }

                ApplyBuff(enemy.Buffs, buffId, stacks, turns, true);
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
        /// <summary>
        /// 一次出牌的第一段：校验、扣费、记录 X、跑效果。
        /// 效果里如果要求玩家选择，会停在 NeedsChoice —— 这时**不做翻面、不结算瞬发**。
        /// 玩家确认后由 <see cref="CompletePlay"/> 继续。
        /// </summary>
        public PlayResult BeginPlay(CardInstance card, EnemyInstance target,
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

            // 1) 实际要付多少：贪婪给玩家挂的「费用归零」buff 会把 cost 修饰成 0；
            //    X 费牌在这里把 X 记下来，供效果串里的 {X} 使用
            // 打出条件（围困这种）：不满足直接拒绝，不扣费
            if (!CanPlayFace(face))
            {
                flippedEnemy = null;
                battleEnded = false;
                return PlayResult.ConditionNotMet;
            }

            int paid = EffectiveCost(face);
            CurrentX = string.IsNullOrEmpty(XSource(face)) ? 0 : paid;

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

                // 计谋：守 / 计谋：攻 —— 只有「打出的这张牌当前面是状态牌」才触发
                if (face.Type == CardType.Status)
                {
                    RunTriggers(PlayerBuffs, Data.BuffTrigger.StatusPlayed, target);
                }

                CardEffects.ExecuteAll(context);
                flippedEnemy = context.FlippedEnemy;
                m_LastEffectFlipEnemy = context.FlippedEnemy;

                // 记下这次出牌；第二段（CompletePlay）负责翻面与收瞬发
                m_PendingPlayCard = card;
                m_PendingPlayTarget = target;
                m_PlayInProgress = true;
                flippedEnemy = context.FlippedEnemy;
            }
            finally
            {
                ExitCardContext(previousCard);
                m_DeferDamage = false;
            }

            // 需要玩家选择：挂起，不做翻面/瞬发（由 CompletePlay 继续）
            if (HasPendingChoice)
            {
                battleEnded = false;
                return PlayResult.NeedsChoice;
            }

            // 打出的牌离开手牌：它提供的能力（如果是能力牌）随之失效
            RefreshPowerCards();

            if (!IsResolving)
            {
                CheckBattleEnd();
            }

            battleEnded = IsBattleOver;
            return PlayResult.Ok;
        }

        /// <summary>一次出牌是否正卡在「等玩家选牌」。</summary>
        private bool m_PlayInProgress;

        /// <summary>挂起中的出牌（等玩家选牌时用）。</summary>
        private EnemyInstance m_LastEffectFlipEnemy;

        private CardInstance m_PendingPlayCard;
        private EnemyInstance m_PendingPlayTarget;

        /// <summary>第二段：应用玩家选牌结果 → 翻面 → 收瞬发。没有挂起时直接返回 Ok。</summary>
        public PlayResult CompletePlay(out EnemyInstance flippedEnemy, out bool battleEnded)
        {
            flippedEnemy = m_LastEffectFlipEnemy;
            battleEnded = false;

            if (!m_PlayInProgress)
            {
                return PlayResult.Ok;
            }

            m_PlayInProgress = false;
            CardInstance card = m_PendingPlayCard;
            EnemyInstance target = m_PendingPlayTarget;
            m_PendingPlayCard = null;
            m_PendingPlayTarget = null;

            if (card != null)
            {
                RequestFlip(card, FlipSource.Play, target);
            }

            RefreshPowerCards();

            if (!IsResolving)
            {
                CheckBattleEnd();
            }

            battleEnded = IsBattleOver;
            return PlayResult.Ok;
        }

        /// <summary>
        /// 出牌的唯一入口：先跑 <see cref="BeginPlay"/>；
        /// 需要选择时返回 NeedsChoice（表现层等玩家选完再调 CompletePlay），否则直接完成。
        /// </summary>
        public PlayResult PlayCard(CardInstance card, EnemyInstance target,
            out EnemyInstance flippedEnemy, out bool battleEnded)
        {
            PlayResult result = BeginPlay(card, target, out flippedEnemy, out battleEnded);
            if (result != PlayResult.Ok)
            {
                return result;
            }

            return CompletePlay(out flippedEnemy, out battleEnded);
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

            // 翻面可能让能力牌生效 / 失效
            RefreshPowerCards();

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

            // 「吸收」：重写这次受击 —— 实际只吃 0 点伤害，
            // 但「原伤害」要记成层数（吸收反噬据此换算下回合的力量流失），
            // 并且**仍然产生一次命中**，否则表现层看不到弹道和撞击反馈。
            if (enemy.Buffs.Has(BuffAbsorb))
            {
                int absorbed = amount;
                enemy.AbsorbedThisTurn += absorbed;

                BuffDefinition backlash = FindBuff(BuffAbsorbBacklash);
                if (backlash != null)
                {
                    enemy.Buffs.Add(backlash, absorbed, -1);
                }

                if (m_DeferDamage)
                {
                    m_Pending.Add(new PendingHit(enemy, 0,
                        m_InstantPhase ? HitOrigin.Instant : HitOrigin.Play, m_ExecutingCard));
                }
                else
                {
                    DamageApplied?.Invoke(enemy, 0);
                }

                return false;
            }

            // 力量（attack）先加到基础伤害上，再算伤害加成与易伤 —— 与预览算法一致
            amount += (int)PlayerBuffs.Flat("attack");
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
                ResetOnBattleEnd();
                BattleEnded?.Invoke(true);
            }
            else if (IsDefeat)
            {
                ResetOnBattleEnd();
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
                FinishBattle(false);
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
            targetHandSize = Math.Min(targetHandSize, BattleRules.Current.MaxHandSize);

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

                // 关键词「抽到释放」：抽到的瞬间执行这张面自己的效果，牌留在手上
                if (card.Face.HasKeyword(KeywordMechanics.DrawRelease))
                {
                    RunDrawRelease(card);
                }
            }

            RaiseChanged();
        }

        /// <summary>强制把一张牌翻到正面并执行它的正面效果（放逐）。带深度保护，避免互相递归。</summary>
        public void ForceFlipToFrontAndPlay(CardInstance card)
        {
            if (card == null || m_ForcedPlayDepth > 1)
            {
                return;
            }

            if (card.Side != CardFaceSide.Front)
            {
                // Flip() 会通过 Flipped 事件自动进入 OnCardFlipped（收瞬发、刷能力牌）
                card.Flip();
            }

            m_ForcedPlayDepth++;
            CardInstance previous = EnterCardContext(card);
            try
            {
                var context = new CardEffectContext
                {
                    State = this,
                    Card = card,
                    Target = PickRandomAliveEnemy(),
                    Face = card.Face,
                };

                CardEffects.ExecuteAll(context);
            }
            finally
            {
                m_ForcedPlayDepth--;
                ExitCardContext(previous);
            }
        }

        /// <summary>打出正面效果后立刻丢弃（重整旗鼓）。</summary>
        public void PlayFrontThenDiscard(CardInstance card)
        {
            if (card == null)
            {
                return;
            }

            ForceFlipToFrontAndPlay(card);
            DiscardCard(card);
        }

        private int m_ForcedPlayDepth;

        /// <summary>「抽到释放」：按抽到的那一面跑一次效果（不扣费、不离开手牌）。</summary>
        private void RunDrawRelease(CardInstance card)
        {
            if (card == null || m_DrawReleaseDepth > 1)
            {
                return;
            }

            m_DrawReleaseDepth++;
            CardInstance previous = EnterCardContext(card);
            try
            {
                var context = new CardEffectContext
                {
                    State = this,
                    Card = card,
                    Target = PickRandomAliveEnemy(),
                    Face = card.Face,
                };

                CardEffects.ExecuteAll(context);
            }
            finally
            {
                m_DrawReleaseDepth--;
                ExitCardContext(previous);
                RaiseChanged();
            }
        }

        private int m_DrawReleaseDepth;

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
