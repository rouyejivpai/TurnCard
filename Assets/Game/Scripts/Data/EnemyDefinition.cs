using UnityEngine;

namespace TurnCard.Game.Data
{
    /// <summary>运行时读取的敌人一个形态的数据。</summary>
    public readonly struct EnemyForm
    {
        public EnemyForm(string displayName, int maxHp, int attack, string description)
        {
            DisplayName = displayName;
            MaxHp = maxHp;
            Attack = attack;
            Description = description;
        }

        public string DisplayName { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public string Description { get; }
    }

    /// <summary>
    /// 敌人定义。敌人是「角色卡」：带攻击力与生命的卡牌（Q14），
    /// 正面与背面即两种形态，血量与攻击力各自独立配置（Q18、Q21）。
    /// 由 DataTables/Enemies.csv 生成。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Enemy Definition", fileName = "Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private int m_Id;

        [Tooltip("所属池子：normal / elite / boss")]
        [SerializeField] private EnemyPool m_Pool = EnemyPool.Normal;

        [SerializeField] private string m_FrontName = string.Empty;
        [SerializeField] private int m_FrontHp;
        [SerializeField] private int m_FrontAttack;
        [TextArea] [SerializeField] private string m_FrontDescription = string.Empty;

        [SerializeField] private string m_BackName = string.Empty;
        [SerializeField] private int m_BackHp;
        [SerializeField] private int m_BackAttack;
        [TextArea] [SerializeField] private string m_BackDescription = string.Empty;

        public int Id => m_Id;

        /// <summary>所属池子。</summary>
        public EnemyPool Pool => m_Pool;

        public EnemyForm Front => new EnemyForm(m_FrontName, m_FrontHp, m_FrontAttack,
            m_FrontDescription);

        public EnemyForm Back => new EnemyForm(m_BackName, m_BackHp, m_BackAttack,
            m_BackDescription);

        public EnemyForm GetForm(CardFaceSide side)
        {
            return side == CardFaceSide.Front ? Front : Back;
        }
    }
}
