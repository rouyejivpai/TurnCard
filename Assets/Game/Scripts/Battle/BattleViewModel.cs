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
        }

        /// <summary>一张手牌的显示数据。</summary>
        public struct HandVm
        {
            public CardInstance Card;
            public int? Damage;
            public int? Armor;
            public bool Modified;
            public IReadOnlyDictionary<string, int> Tokens;
        }

        public int PlayerHp { get; private set; }
        public int PlayerHpMax { get; private set; }
        public int PlayerArmor { get; private set; }
        public int Energy { get; private set; }
        public int MaxEnergy { get; private set; }
        public float PlayerHpRatio { get; private set; }

        public readonly List<EnemyVm> Enemies = new List<EnemyVm>();
        public readonly List<HandVm> Hand = new List<HandVm>();

        private readonly Dictionary<string, int> m_TokenBuffer = new Dictionary<string, int>();

        /// <summary>按当前战斗状态重算一遍快照。传 hover 时手牌伤害会算上该目标的易伤。</summary>
        public void Update(BattleState state, EnemyInstance hover)
        {
            Enemies.Clear();
            Hand.Clear();

            if (state == null)
            {
                return;
            }

            PlayerHpMax = BattleState.PlayerMaxHp;
            PlayerHp = state.PlayerHp;
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
                    Armor = enemy.Armor,
                    ArmorVisible = enemy.Armor > 0,
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

                Hand.Add(new HandVm
                {
                    Card = card,
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
