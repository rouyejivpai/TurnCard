using UnityEngine;

namespace TurnCard.Game.Data
{
    /// <summary>
    /// 一个关键词的定义：名字 + 说明。
    /// 由 DataTables/Keywords.xlsx 生成。
    ///
    /// 卡面文本里用 <c>[[名字]]</c> 标记关键词，鼠标悬停时按名字查这张表。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Keyword", fileName = "Keyword")]
    public sealed class KeywordDefinition : ScriptableObject
    {
        [SerializeField] private int m_Id;
        // 注意：不能用 m_Name，那是 UnityEngine.Object 自己的字段，序列化会把它抢走
        [SerializeField] private string m_DisplayName = string.Empty;
        [TextArea] [SerializeField] private string m_Description = string.Empty;

        public int Id => m_Id;
        public string Name => m_DisplayName;
        public string Description => m_Description;
    }
}
