using UnityEngine;

namespace TurnCard.Game.Data
{
    /// <summary>敌人所属的池子。</summary>
    public enum EnemyPool
    {
        Normal,
        Elite,
        Boss,
    }

    /// <summary>敌人这一回合要做什么。</summary>
    public enum EnemyIntentType
    {
        /// <summary>对玩家造成伤害，可多段。</summary>
        Attack,

        /// <summary>自己获得护甲。</summary>
        Defend,

        /// <summary>自己攻击力提升（本场持续，翻面后保留）。</summary>
        Buff,

        /// <summary>让玩家易伤：下次受到的伤害增加。</summary>
        Debuff,
    }

    /// <summary>
    /// 一个敌人的一条招式。由 <c>DataTables/EnemyIntents.xlsx</c> 生成。
    /// 表格列：id、enemyId、form（front/back）、intent、value、hits
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Enemy Intent", fileName = "EnemyIntent")]
    public sealed class EnemyIntentDefinition : ScriptableObject
    {
        [Tooltip("所属敌人 id")]
        [SerializeField] private int m_EnemyId;

        [Tooltip("作用于哪一面：front / back")]
        [SerializeField] private string m_Form = "front";

        [SerializeField] private EnemyIntentType m_Intent = EnemyIntentType.Attack;

        [Tooltip("数值：伤害 / 护甲 / 攻击加成 / 易伤值")]
        [SerializeField] private int m_Value = 1;

        [Tooltip("攻击段数，仅攻击意图使用")]
        [SerializeField] private int m_Hits = 1;

        public int EnemyId => m_EnemyId;
        public CardFaceSide Form => string.Equals(m_Form, "back", System.StringComparison.OrdinalIgnoreCase)
            ? CardFaceSide.Back
            : CardFaceSide.Front;
        public EnemyIntentType Intent => m_Intent;
        public int Value => m_Value;
        public int Hits => Mathf.Max(1, m_Hits);
    }
}
