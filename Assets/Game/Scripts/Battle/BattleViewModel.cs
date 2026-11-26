using System.Collections.Generic;
using TurnCard.Game.Data;

namespace TurnCard.Game.Battle
{
    /// <summary>
    /// 战斗界面的 ViewModel：把 <see cref="BattleState"/> 里散落的数值算成「UI 直接能用的快照」。
    ///
    /// 分工：
    /// - Model（BattleState/EnemyInstance）只管规则与数值，变化时抛 <c>Changed</c>；
    /// - ViewModel（本类）负责把数值换算成显示形式（血条比例、意图数字、预览伤害…），**不碰任何 UI 对象**；
    /// - View（BattleForm / EnemyCardView / CardView）只做「快照值 → 控件」的写入。
    ///
    /// 刷新由 BattleForm 统一在帧末调用 <see cref="Update"/>，所以一次事件风暴只会算一次。
    /// </summary>
    public sealed class BattleViewModel
    {
        /// <summary>一个敌人的显示数据。</summary>
        public struct EnemyVm
        {
            public EnemyInstance Enemy;
            public float HpRatio;
            public string HpText;
            public int Armor;
            public bool ArmorVisible;

            /// <summary>意图每段伤害（已过 buff）。</summary>
            public int IntentValue;

            /// <summary>和招式原值不同（界面标红）。</summary>
            public bool IntentModified;

            /// <summary>血量 / 最大血量。</summary>
            public int Hp;

            public int HpMax;

            /// <summary>当前形态名（翻面会变）。</summary>
            public string Name;

            /// <summary>敌人身上的 buff（图标条直接画这个）。</summary>
            public List<BuffVm> Buffs;
        }

        /// <summary>一个 buff 图标的显示数据（层数已过滤隐藏 buff）。</summary>
        public struct BuffVm
        {
            public Data.BuffDefinition Definition;
            public int Stacks;
        }

        /// <summary>一张手牌的显示数据。</summary>
        public struct HandVm
        {
            public CardInstance Card;
            public int? Damage;
            public int? Armor;
            public bool Modified;

            /// <summary>显示用的费用（已算 X 费、贪婪免费等修饰）。</summary>
            public int Cost;

            /// <summary>费用是不是 X（X 费牌卡面要显示实际值，这里给显示层用）。</summary>
            public bool CostIsX;

            /// <summary>
            /// 这张牌现在能不能打出（回合、忙碌、费用、打出条件都在这里算好）。
            /// 界面只读这个字段，不再自己去问 BattleState。
            /// </summary>
            public bool Playable;

            public IReadOnlyDictionary<string, int> Tokens;
        }

        public int PlayerHp { get; private set; }
        public int PlayerHpMax { get; private set; }
        public int PlayerArmor { get; private set; }
        public int Energy { get; private set; }
        public int MaxEnergy { get; private set; }

        /// <summary>抽牌堆 / 弃牌堆 / 消耗堆的张数（界面只读这里，不再直接读 state）。</summary>
        public int DrawCount { get; private set; }

        public int DiscardCount { get; private set; }
        public int ExhaustCount { get; private set; }
        public float PlayerHpRatio { get; private set; }

        /// <summary>玩家身上的 buff（图标条直接画这个）。</summary>
        public readonly List<BuffVm> PlayerBuffs = new List<BuffVm>();

        public readonly List<EnemyVm> Enemies = new List<EnemyVm>();
        public readonly List<HandVm> Hand = new List<HandVm>();

        private readonly Dictionary<string, int> m_TokenBuffer = new Dictionary<string, int>();
        private readonly List<BuffVm> m_BuffBuffer = new List<BuffVm>();

        /// <summary>把 BuffContainer 拷成快照（跳过隐藏 buff 与 0 层）。</summary>
        private static List<BuffVm> CollectBuffs(BuffContainer container, List<BuffVm> into = null)
        {
            List<BuffVm> list = into ?? new List<BuffVm>();
            list.Clear();

            if (container == null)
            {
                return list;
            }

            foreach (BuffInstance buff in container.Buffs)
            {
                if (buff == null || buff.Definition == null || buff.Stacks <= 0 || buff.Definition.Hidden)
                {
                    continue;
                }

                list.Add(new BuffVm { Definition = buff.Definition, Stacks = buff.Stacks });
            }

            return list;
        }

        /// <summary>按当前战斗状态重算一遍快照。传 hover 时手牌伤害会算上该目标的易伤。</summary>
        public void Update(BattleState state, EnemyInstance hover)
        {
            Enemies.Clear();
            Hand.Clear();
            PlayerBuffs.Clear();

            if (state == null)
            {
                return;
            }

            PlayerHpMax = BattleState.PlayerMaxHp;
            PlayerHp = state.PlayerHp;
            CollectBuffs(state.PlayerBuffs, PlayerBuffs);
            DrawCount = state.DrawPile.Count;
            DiscardCount = state.DiscardPile.Count;
            ExhaustCount = state.ExhaustPile.Count;
            PlayerArmor = state.PlayerArmor;
            Energy = state.Energy;
            MaxEnergy = BattleState.MaxEnergy;
            PlayerHpRatio = PlayerHpMax <= 0 ? 0f : UnityEngine.Mathf.Clamp01(PlayerHp / (float)PlayerHpMax);

            foreach (EnemyInstance enemy in state.Enemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                EnemyForm form = enemy.Form;
                BattleState.ValuePreview attack = state.PreviewEnemyAttack(enemy);

                Enemies.Add(new EnemyVm
                {
                    Enemy = enemy,
                    HpRatio = form.MaxHp <= 0 ? 0f : UnityEngine.Mathf.Clamp01(enemy.Hp / (float)form.MaxHp),
                    HpText = enemy.Hp + "/" + form.MaxHp,
                    Hp = enemy.Hp,
                    HpMax = form.MaxHp,
                    Name = form.DisplayName,
                    Armor = enemy.Armor,
                    ArmorVisible = enemy.Armor > 0,
                    Buffs = CollectBuffs(enemy.Buffs),
                    IntentValue = attack.Value,
                    IntentModified = attack.Modified,
                });
            }

            foreach (CardInstance card in state.Hand)
            {
                if (card == null)
                {
                    continue;
                }

                CardFace face = card.Face;
                BattleState.ValuePreview damage = state.PreviewPlayerDamage(face, hover);
                BattleState.ValuePreview armor = state.PreviewPlayerArmor(face);

                m_TokenBuffer.Clear();
                state.CollectPreviewTokens(face, hover, m_TokenBuffer);

                int cost = state.EffectiveCostFor(face);
                Hand.Add(new HandVm
                {
                    Card = card,
                    Playable = state.IsPlayerTurn && !state.IsResolving && !state.IsBattleOver &&
                               state.CanPlayFace(face) && cost <= state.Energy,
                    Cost = cost,
                    CostIsX = state.HasXCost(face),
                    Damage = damage.Value,
                    Armor = armor.Value,
                    Modified = damage.Modified || armor.Modified,
                    Tokens = m_TokenBuffer.Count > 0
                        ? new Dictionary<string, int>(m_TokenBuffer)
                        : null,
                });
            }
        }
    }
}
