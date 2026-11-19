using UnityEngine;

namespace TurnCard.Game.Battle
{
    /// <summary>
    /// 战斗表现参数（纯表现层，不影响结算）。
    /// 放在 Resources/BattleFxSettings.asset，改数值不用改代码。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Battle Fx Settings", fileName = "BattleFxSettings")]
    public sealed class BattleFxSettings : ScriptableObject
    {
        [Header("投射物飞行时间")]
        [Tooltip("箭矢从卡牌飞到敌人的时间（秒）")]
        [SerializeField] private float m_ArrowFlight = 0.20f;

        [Tooltip("炮弹从卡牌飞到敌人的时间（秒）")]
        [SerializeField] private float m_CannonFlight = 0.38f;

        [Header("多段伤害的节奏")]
        [Tooltip("两次伤害表现之间至少间隔多久（秒）。瞬发结算两次时，两发之间会等这么久")]
        [SerializeField] private float m_HitInterval = 0.30f;

        [Tooltip("命中之后额外停顿多久再开始下一发（秒）")]
        [SerializeField] private float m_ImpactHold = 0.08f;

        [Header("出牌节奏")]
        [Tooltip("翻面动画时长（秒）")]
        [SerializeField] private float m_FlipDuration = 0.35f;

        public float ArrowFlight => Mathf.Max(0.05f, m_ArrowFlight);
        public float CannonFlight => Mathf.Max(0.05f, m_CannonFlight);
        public float HitInterval => Mathf.Max(0f, m_HitInterval);
        public float ImpactHold => Mathf.Max(0f, m_ImpactHold);
        public float FlipDuration => Mathf.Max(0.05f, m_FlipDuration);

        /// <summary>按卡牌类型取飞行时间。</summary>
        public float FlightFor(bool cannon)
        {
            return cannon ? CannonFlight : ArrowFlight;
        }

        private static BattleFxSettings s_Current;

        /// <summary>运行时读取；没有配置资产时用一套默认值，保证不会崩。</summary>
        public static BattleFxSettings Current
        {
            get
            {
                if (s_Current == null)
                {
                    s_Current = Resources.Load<BattleFxSettings>("BattleFxSettings");
                    if (s_Current == null)
                    {
                        s_Current = CreateInstance<BattleFxSettings>();
                    }
                }

                return s_Current;
            }
        }
    }
}
