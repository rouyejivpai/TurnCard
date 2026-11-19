using UnityEngine;

namespace TurnCard.Game.Data
{
    /// <summary>
    /// 一场战斗的敌人阵容里的一员。由 <c>DataTables/Encounters.xlsx</c> 生成。
    ///
    /// 同一 <see cref="Group"/> 的多行组成一个阵容，
    /// <see cref="Order"/> 决定它在地图上从左到右的站位与出手顺序。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Encounter", fileName = "Encounter")]
    public sealed class EncounterEntry : ScriptableObject
    {
        [Tooltip("阵容编号，同号的一组同时出场")]
        [SerializeField] private int m_Group;

        [Tooltip("站位顺序，从 0 开始，小的在左边、先出手")]
        [SerializeField] private int m_Order;

        [Tooltip("敌人 id")]
        [SerializeField] private int m_EnemyId;

        [Tooltip("属于哪个池子：normal / elite / boss")]
        [SerializeField] private EnemyPool m_Pool = EnemyPool.Normal;

        public int Group => m_Group;
        public int Order => m_Order;
        public int EnemyId => m_EnemyId;
        public EnemyPool Pool => m_Pool;
    }
}
