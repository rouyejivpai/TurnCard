using UnityEngine;

namespace TurnCard.Game.Data
{
    /// <summary>
    /// 初始牌组的一行：某种卡放几张。
    /// 由 <c>DataTables/StarterDeck.xlsx</c> 生成（列：id、count，其中 id 就是卡牌 id）。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Starter Deck Entry", fileName = "StarterDeckEntry")]
    public sealed class StarterDeckEntry : ScriptableObject
    {
        [Tooltip("对应 Cards 表里的 id")]
        [SerializeField] private int m_Id;

        [Tooltip("初始牌组里放几张")]
        [SerializeField] private int m_Count;

        public int CardId => m_Id;
        public int Count => m_Count;
    }
}
