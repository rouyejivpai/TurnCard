using UnityEngine;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 手牌的表现参数：布局过渡、悬停浮动、投影。
    /// 放在 <c>Resources/Config/HandLayoutSettings.asset</c>，改手感不用改代码。
    ///
    /// 与 <c>BattleFxSettings</c>（战斗节奏）、<c>EnemyDeathSettings</c>（死亡动画）、
    /// <c>IntentIconSet</c>（意图图标）放在同一个 <c>Resources/Config</c> 目录下。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Hand Layout Settings", fileName = "HandLayoutSettings")]
    public sealed class HandLayoutSettings : ScriptableObject
    {
        [Header("布局过渡")]
        [Tooltip("手牌增删后，剩余牌滑到新位置的时长（秒）")]
        [SerializeField] private float m_LayoutDuration = 0.20f;

        [Tooltip("每张牌之间错开多少秒，避免整排一起弹")]
        [SerializeField] private float m_LayoutStagger = 0.015f;

        [Tooltip("最小间距占卡宽的比例，手牌挤压时的下限")]
        [Range(0.3f, 1f)]
        [SerializeField] private float m_MinSpacingRatio = 0.55f;

        [Header("悬停")]
        [Tooltip("悬停放大倍数")]
        [SerializeField] private float m_HoverScale = 1.25f;

        [Tooltip("悬停时抬起的高度（像素）")]
        [SerializeField] private float m_LiftHeight = 20f;

        [Tooltip("浮动幅度下限（像素）")]
        [SerializeField] private float m_FloatAmplitudeMin = 2.5f;

        [Tooltip("浮动幅度上限（像素）")]
        [SerializeField] private float m_FloatAmplitudeMax = 4.5f;

        [Tooltip("浮动方向随机范围（度），0 表示只上下浮动")]
        [SerializeField] private float m_FloatAngleRange = 35f;

        [Tooltip("浮动一个来回的时长（秒）")]
        [SerializeField] private float m_FloatPeriod = 1.5f;

        [Tooltip("结束悬停后回位的时长（秒）")]
        [SerializeField] private float m_ReturnDuration = 0.16f;

        [Header("投影")]
        [Tooltip("影子相对卡牌的偏移（右下为正）")]
        [SerializeField] private Vector2 m_ShadowOffset = new Vector2(14f, -18f);

        [Tooltip("影子最浓时的不透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float m_ShadowOpacity = 0.42f;

        [Tooltip("影子淡入 / 淡出的时长（秒）")]
        [SerializeField] private float m_ShadowFade = 0.15f;

        public float LayoutDuration => Mathf.Max(0.01f, m_LayoutDuration);
        public float LayoutStagger => Mathf.Max(0f, m_LayoutStagger);
        public float MinSpacingRatio => Mathf.Clamp(m_MinSpacingRatio, 0.3f, 1f);
        public float HoverScale => Mathf.Max(1f, m_HoverScale);
        public float LiftHeight => m_LiftHeight;
        public float FloatAmplitudeMin => Mathf.Max(0f, m_FloatAmplitudeMin);
        public float FloatAmplitudeMax => Mathf.Max(FloatAmplitudeMin, m_FloatAmplitudeMax);
        public float FloatAngleRange => Mathf.Clamp(m_FloatAngleRange, 0f, 180f);
        public float FloatPeriod => Mathf.Max(0.2f, m_FloatPeriod);
        public float ReturnDuration => Mathf.Max(0.01f, m_ReturnDuration);
        public Vector2 ShadowOffset => m_ShadowOffset;
        public float ShadowOpacity => Mathf.Clamp01(m_ShadowOpacity);
        public float ShadowFade => Mathf.Max(0.01f, m_ShadowFade);

        private const string ResourcePath = "Config/HandLayoutSettings";

        private static HandLayoutSettings s_Current;

        /// <summary>运行时读取；没有配置资产时用默认值，保证不会崩。</summary>
        public static HandLayoutSettings Current
        {
            get
            {
                if (s_Current == null)
                {
                    s_Current = Resources.Load<HandLayoutSettings>(ResourcePath);
                    if (s_Current == null)
                    {
                        s_Current = CreateInstance<HandLayoutSettings>();
                    }
                }

                return s_Current;
            }
        }
    }
}
