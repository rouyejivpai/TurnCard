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
        public CardFaceSide Side { get; private set; } = CardFaceSide.Front;
        public CardFace Face => Definition.GetFace(Side);
        public bool IsFront => Side == CardFaceSide.Front;

        public void Flip()
        {
            Side = Side == CardFaceSide.Front ? CardFaceSide.Back : CardFaceSide.Front;
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
        public int Hp { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>护甲，每回合开始清零。</summary>
        public int Armor { get; set; }

        /// <summary>本场累积的攻击加成，翻面后保留。</summary>
        public int AttackBonus { get; set; }

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

            // Fisher-Yates 洗牌
            for (int i = m_Intents.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                Data.EnemyIntentDefinition swap = m_Intents[i];
                m_Intents[i] = m_Intents[j];
                m_Intents[j] = swap;
            }
        }

        /// <summary>取走队首招式（执行完之后调用）。</summary>
        public void PopIntent()
        {
            if (m_Intents.Count > 0)
            {
                m_Intents.RemoveAt(0);
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
                // 第一形态归零：当场翻面，第二形态满血重开（Q3、Q22）
                Side = CardFaceSide.Back;
                Hp = Form.MaxHp;
                return true;
            }

            Hp = 0;
            IsDead = true;
            return false;
        }
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

        /// <summary>排队中的一次翻面。</summary>
        public readonly struct PendingFlip
        {
            public PendingFlip(CardInstance card, FlipSource source, EnemyInstance target)
            {
                Card = card;
                Source = source;
                Target = target;
            }

            public CardInstance Card { get; }
            public FlipSource Source { get; }

            /// <summary>本次出牌选中的目标，「瞬发」结算时用。</summary>
            public EnemyInstance Target { get; }
        }

        private readonly List<PendingFlip> m_PendingFlips = new List<PendingFlip>();

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
                return;
            }
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

        /// <summary>提交队首翻面，返回是否还有后续。翻面会触发瞬发，可能继续入队。</summary>
        public bool CommitNextFlip()
        {
            if (m_PendingFlips.Count == 0)
            {
                return false;
            }

            PendingFlip flip = m_PendingFlips[0];
            m_PendingFlips.RemoveAt(0);

            if (flip.Card != null)
            {
                m_FlipSource = flip.Source;
                m_PlayTarget = flip.Target;
                m_LastInstantFlip = null;
                try
                {
                    flip.Card.Flip();
                }
                finally
                {
                    m_PlayTarget = null;
                    m_FlipSource = FlipSource.Effect;
                }

                LastInstantFlipEnemy = m_LastInstantFlip;
            }

            return m_PendingFlips.Count > 0;
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

        private int m_PlayerHp;
        private int m_PlayerArmor;
        private int m_PlayerVulnerable;
        private int m_Energy;

        public int PlayerHp
        {
            get => m_PlayerHp;
            private set
            {
                if (m_PlayerHp == value) return;
                m_PlayerHp = value;
                PlayerHpChanged?.Invoke(value);
            }
        }

        /// <summary>易伤：玩家下次受到伤害时额外增加的点数，触发一次后清零。</summary>
        public int PlayerVulnerable
        {
            get => m_PlayerVulnerable;
            set => m_PlayerVulnerable = System.Math.Max(0, value);
        }

        public int PlayerArmor
        {
            get => m_PlayerArmor;
            private set
            {
                if (m_PlayerArmor == value) return;
                m_PlayerArmor = value;
                PlayerArmorChanged?.Invoke(value);
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
                instance.RefillIntents(IntentsFor(instance));
                m_Enemies.Add(instance);
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
            TurnNumber++;
            TurnStarted?.Invoke(TurnNumber);
            Energy = MaxEnergy;
            PlayerArmor = 0; // 护甲每回合开始清零（Q6）

            // 敌人护甲同样每回合清零
            foreach (EnemyInstance enemy in m_Enemies)
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                enemy.Armor = 0;
                if (enemy.CurrentIntent == null)
                {
                    enemy.RefillIntents(IntentsFor(enemy));
                }
            }
            DrawUpTo(HandSize);
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
                if (card.Face.HasKeyword(KeywordMechanics.Keep))
                {
                    continue;
                }

                m_Hand.RemoveAt(i);
                m_DiscardPile.Add(card);
                CardDiscarded?.Invoke(card);
            }
        }

        /// <summary>所有存活敌人依次攻击玩家，护甲逐个抵扣。</summary>
        public List<string> ResolveEnemyTurn()
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

                Data.EnemyIntentDefinition intent = enemy.CurrentIntent;
                if (intent == null)
                {
                    continue;
                }

                switch (intent.Intent)
                {
                    case Data.EnemyIntentType.Attack:
                    {
                        int perHit = intent.Value + enemy.AttackBonus;
                        for (int hit = 0; hit < intent.Hits; hit++)
                        {
                            int raw = perHit + PlayerVulnerable;
                            PlayerVulnerable = 0;
                            int absorbed = Math.Min(PlayerArmor, raw);
                            PlayerArmor -= absorbed;
                            int toHp = raw - absorbed;
                            PlayerHp = Math.Max(0, PlayerHp - toHp);
                            log.Add($"{enemy.Form.DisplayName} 造成 {raw} 点伤害（护甲吸收 {absorbed}，生命 -{toHp}）");

                            if (PlayerHp <= 0)
                            {
                                IsDefeat = true;
                                BattleEnded?.Invoke(false);
                                return log;
                            }
                        }

                        break;
                    }

                    case Data.EnemyIntentType.Defend:
                        enemy.Armor += intent.Value;
                        log.Add($"{enemy.Form.DisplayName} 获得 {intent.Value} 点护甲");
                        break;

                    case Data.EnemyIntentType.Buff:
                        enemy.AttackBonus += intent.Value;
                        log.Add($"{enemy.Form.DisplayName} 攻击 +{intent.Value}");
                        break;

                    case Data.EnemyIntentType.Debuff:
                        PlayerVulnerable += intent.Value;
                        log.Add($"{enemy.Form.DisplayName} 使你易伤 +{intent.Value}");
                        break;
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

            if (card.Face.Cost > Energy)
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
            Energy -= face.Cost;

            var context = new CardEffectContext
            {
                State = this,
                Card = card,
                Target = target,
                Face = face,
            };

            // 出牌期间的伤害（含翻面触发的瞬发）全部先入队，等动画命中再扣血。
            m_DeferDamage = true;
            try
            {
                CardEffects.ExecuteAll(context);
                flippedEnemy = context.FlippedEnemy;

                // 翻面走统一入口：入队，由表现层在动画落地时 Commit。
                RequestFlip(card, FlipSource.Play, target);
            }
            finally
            {
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

            InstantTriggered?.Invoke(card);
            EnemyInstance flipped = TriggerInstant(card, m_PlayTarget);
            if (flipped != null)
            {
                m_LastInstantFlip = flipped;
            }
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
            public PendingHit(EnemyInstance enemy, int amount)
            {
                Enemy = enemy;
                Amount = amount;
            }

            public EnemyInstance Enemy { get; }
            public int Amount { get; }
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

            m_InstantDepth++;
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

            LastDamageDealt += amount;
            LastDamagedEnemy = enemy;
            m_LastDamages.Add(new DamageRecord(enemy, amount));

            if (m_DeferDamage)
            {
                m_Pending.Add(new PendingHit(enemy, amount));
                return false;
            }

            bool flipped = enemy.TakeDamage(amount);
            DamageApplied?.Invoke(enemy, amount);

            if (flipped)
            {
                // 翻面 = 换一套行为：重新洗当前形态的招式池
                enemy.RefillIntents(IntentsFor(enemy));
                EnemyFlipped?.Invoke(enemy);
            }

            if (enemy.IsDead)
            {
                EnemyDied?.Invoke(enemy);
                CheckBattleEnd();
            }

            return flipped;
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
                    hit.Enemy.RefillIntents(IntentsFor(hit.Enemy));
                    EnemyFlipped?.Invoke(hit.Enemy);
                }

                if (hit.Enemy.IsDead)
                {
                    EnemyDied?.Invoke(hit.Enemy);
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
