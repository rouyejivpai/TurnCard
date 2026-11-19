using UnityEngine;

namespace TurnCard.Game.Data
{
    /// <summary>
    /// 战斗内容的索引：把生成出来的卡牌、敌人与关键词聚合成一个资产，运行时只需加载它。
    /// 由 <c>DataTableImporter</c> 在每次生成后自动更新。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Battle Content", fileName = "BattleContent")]
    public sealed class BattleContent : ScriptableObject
    {
        [SerializeField] private CardDefinition[] m_Cards = new CardDefinition[0];
        [SerializeField] private EnemyDefinition[] m_Enemies = new EnemyDefinition[0];
        [SerializeField] private KeywordDefinition[] m_Keywords = new KeywordDefinition[0];
        [SerializeField] private StarterDeckEntry[] m_StarterDeck = new StarterDeckEntry[0];
        [SerializeField] private EnemyIntentDefinition[] m_EnemyIntents = new EnemyIntentDefinition[0];
        [SerializeField] private EncounterEntry[] m_Encounters = new EncounterEntry[0];
        [SerializeField] private BuffDefinition[] m_Buffs = new BuffDefinition[0];

        public CardDefinition[] Cards => m_Cards;
        public EnemyDefinition[] Enemies => m_Enemies;
        public KeywordDefinition[] Keywords => m_Keywords;
        public StarterDeckEntry[] StarterDeck => m_StarterDeck;
        public EnemyIntentDefinition[] EnemyIntents => m_EnemyIntents;
        public EncounterEntry[] Encounters => m_Encounters;
        public BuffDefinition[] Buffs => m_Buffs;

        public void SetContent(CardDefinition[] cards, EnemyDefinition[] enemies,
            KeywordDefinition[] keywords, StarterDeckEntry[] starterDeck,
            EnemyIntentDefinition[] enemyIntents, EncounterEntry[] encounters,
            BuffDefinition[] buffs)
        {
            m_Cards = cards;
            m_Enemies = enemies;
            m_Keywords = keywords;
            m_StarterDeck = starterDeck;
            m_EnemyIntents = enemyIntents;
            m_Encounters = encounters;
            m_Buffs = buffs;
        }
    }
}
