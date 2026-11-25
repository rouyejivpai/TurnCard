using UnityEngine;

namespace TurnCard.Game.Battle
{
    /// <summary>
    /// 战斗规则参数（影响结算，不只是表现）。
    /// 放在 <c>Resources/Config/BattleRules.asset</c>，调平衡只改这个资产，不用改代码。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Battle Rules", fileName = "BattleRules")]
    public sealed class BattleRules : ScriptableObject
    {
        [Header("手牌")]
        [Tooltip("每回合抽几张（是「抽 N 张」，不是「补到 N 张」）。")]
        [SerializeField] private int m_DrawPerTurn = 3;

        [Tooltip("手牌上限：抽牌时最多保留这么多张，0 表示不限制")]
        [SerializeField] private int m_MaxHandSize = 10;

        [Header("能量与生命")]
        [Tooltip("每回合的能量")]
        [SerializeField] private int m_EnergyPerTurn = 3;

        [Tooltip("玩家最大生命")]
        [SerializeField] private int m_PlayerMaxHp = 80;

        public int DrawPerTurn => Mathf.Max(1, m_DrawPerTurn);
        public int MaxHandSize => m_MaxHandSize <= 0 ? int.MaxValue : m_MaxHandSize;
        public int EnergyPerTurn => Mathf.Max(1, m_EnergyPerTurn);
        public int PlayerMaxHp => Mathf.Max(1, m_PlayerMaxHp);

        private static BattleRules s_Current;

        private const string ResourcePath = "Config/BattleRules";

        /// <summary>运行时读取；没有配置资产时用默认值，保证不会崩。</summary>
        public static BattleRules Current
        {
            get
            {
                if (s_Current == null)
                {
                    s_Current = Resources.Load<BattleRules>(ResourcePath);
                    if (s_Current == null)
                    {
                        s_Current = CreateInstance<BattleRules>();
                        s_Current.name = "BattleRules(Default)";
                    }
                }

                return s_Current;
            }
        }

        /// <summary>战斗界面重建时清掉缓存（改完资产后能立刻生效）。</summary>
        public static void ClearCache()
        {
            s_Current = null;
        }
    }
}
