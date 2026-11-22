using UnityEngine;

namespace TurnCard.Game.Data
{
    /// <summary>
    /// 意图图标的映射表：**枚举 → Sprite 引用**。
    ///
    /// 放在 <c>Resources/UI/Enemy/IntentIconSet.asset</c>，运行时由 <c>EnemyCardView</c> 读取。
    /// 改图标只要在这一个资产上拖 7 张图，不用碰意图行、不用管资源名、不用改表。
    ///
    /// 优先级：意图行自己的 <c>icon</c> 覆盖 &gt; 本表 &gt; 内置回退图。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/UI/Intent Icon Set", fileName = "IntentIconSet")]
    public sealed class IntentIconSet : ScriptableObject
    {
        [Header("按意图类型配图标（缺的自动回退）")]
        [SerializeField] private Sprite m_Attack;
        [SerializeField] private Sprite m_Defend;
        [SerializeField] private Sprite m_Buff;
        [SerializeField] private Sprite m_Debuff;
        [SerializeField] private Sprite m_ApplyBuff;
        [SerializeField] private Sprite m_Stun;
        [SerializeField] private Sprite m_Flip;

        public Sprite Get(EnemyIntentType type)
        {
            switch (type)
            {
                case EnemyIntentType.Attack: return m_Attack;
                case EnemyIntentType.Defend: return m_Defend;
                case EnemyIntentType.Buff: return m_Buff;
                case EnemyIntentType.Debuff: return m_Debuff;
                case EnemyIntentType.ApplyBuff: return m_ApplyBuff != null ? m_ApplyBuff : m_Debuff;
                case EnemyIntentType.Stun: return m_Stun != null ? m_Stun : m_Defend;
                case EnemyIntentType.FlipSelf: return m_Flip != null ? m_Flip : m_Defend;
                default: return m_Attack;
            }
        }
    }
}
