using UnityEngine;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 敌人死亡表现参数（纯表现层，不影响结算）。
    /// 放在 <c>Resources/Config/EnemyDeathSettings.asset</c>，改数值不用改代码。
    ///
    /// 注意区分两类参数：
    /// <list type="bullet">
    /// <item>这里的字段 → 由 <see cref="EnemyDeathFx"/> 每帧驱动，改这里才生效；</item>
    /// <item>材质 <c>CardSlashDeath.mat</c> 上的同名属性 → 会被这里覆盖，只当默认值用。</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Enemy Death Settings", fileName = "EnemyDeathSettings")]
    public sealed class EnemyDeathSettings : ScriptableObject
    {
        [Header("时间（秒）")]
        [Tooltip("刀痕从起始位置扫到卡心的时长（此时两半还贴在一起）")]
        [SerializeField] private float m_CutDuration = 0.10f;

        [Tooltip("两半分离 + 旋转 + 淡出的时长")]
        [SerializeField] private float m_SplitDuration = 0.35f;

        [Tooltip("溶解时长（与分离重叠，收尾更干脆）")]
        [SerializeField] private float m_DissolveDuration = 0.25f;

        [Tooltip("分离开始后多久开始淡出，给两半分离开一点可见时间")]
        [SerializeField] private float m_FadeDelay = 0.06f;

        [Header("分离")]
        [Tooltip("上下两半各自移动的像素距离")]
        [SerializeField] private float m_SplitDistance = 36f;

        [Tooltip("上下两半各自的旋转角度（度）")]
        [SerializeField] private float m_SplitAngle = 6f;

        [Header("刀痕（写入材质）")]
        [Tooltip("切割线角度，-28 表示右高左低")]
        [SerializeField] private float m_CutAngle = -28f;

        [Tooltip("刀痕扫过前的起始偏移，越小扫过的距离越长")]
        [SerializeField] private float m_CutSweepFrom = -0.9f;

        [Tooltip("刀痕亮边宽度")]
        [Range(0.001f, 0.3f)]
        [SerializeField] private float m_EdgeWidth = 0.05f;

        [Tooltip("刀痕亮边颜色")]
        [SerializeField] private Color m_EdgeColor = new Color(1f, 1f, 0.86f, 1f);

        [Tooltip("溶解颗粒密度，越大越细碎")]
        [SerializeField] private float m_DissolveScale = 14f;

        [Header("刀光（那条扫过去的长条）")]
        [SerializeField] private bool m_SlashEnabled = true;

        [SerializeField] private Vector2 m_SlashSize = new Vector2(300f, 10f);

        [Tooltip("刀光起点（相对卡片中心）")]
        [SerializeField] private Vector2 m_SlashFrom = new Vector2(-110f, 70f);

        [Tooltip("刀光终点（相对卡片中心）")]
        [SerializeField] private Vector2 m_SlashTo = new Vector2(110f, -70f);

        [Tooltip("刀光自身倾角（度），0 表示水平")]
        [SerializeField] private float m_SlashAngle = 28f;

        [SerializeField] private Color m_SlashColor = new Color(1f, 1f, 0.9f, 0.95f);

        [SerializeField] private float m_SlashFadeIn = 0.03f;

        [Tooltip("划到终点前多久开始变透明")]
        [SerializeField] private float m_SlashFadeOutDelay = 0.06f;

        [SerializeField] private float m_SlashFadeOut = 0.09f;

        public float CutDuration => Mathf.Max(0.01f, m_CutDuration);
        public float SplitDuration => Mathf.Max(0.01f, m_SplitDuration);
        public float DissolveDuration => Mathf.Max(0.01f, m_DissolveDuration);
        public float FadeDelay => Mathf.Max(0f, m_FadeDelay);
        public float SplitDistance => m_SplitDistance;
        public float SplitAngle => m_SplitAngle;
        public float CutAngle => m_CutAngle;
        public float CutSweepFrom => m_CutSweepFrom;
        public float EdgeWidth => m_EdgeWidth;
        public Color EdgeColor => m_EdgeColor;
        public float DissolveScale => m_DissolveScale;
        public bool SlashEnabled => m_SlashEnabled;
        public Vector2 SlashSize => m_SlashSize;
        public Vector2 SlashFrom => m_SlashFrom;
        public Vector2 SlashTo => m_SlashTo;
        public float SlashAngle => m_SlashAngle;
        public Color SlashColor => m_SlashColor;
        public float SlashFadeIn => Mathf.Max(0f, m_SlashFadeIn);
        public float SlashFadeOutDelay => Mathf.Max(0f, m_SlashFadeOutDelay);
        public float SlashFadeOut => Mathf.Max(0f, m_SlashFadeOut);

        /// <summary>整段动画时长（分离结束时收尾）。</summary>
        public float TotalDuration => CutDuration + SplitDuration;

        private const string ResourcePath = "Config/EnemyDeathSettings";

        private static EnemyDeathSettings s_Current;

        /// <summary>运行时读取；没有配置资产时用一套默认值，保证不会崩。</summary>
        public static EnemyDeathSettings Current
        {
            get
            {
                if (s_Current == null)
                {
                    s_Current = Resources.Load<EnemyDeathSettings>(ResourcePath);
                    if (s_Current == null)
                    {
                        s_Current = CreateInstance<EnemyDeathSettings>();
                    }
                }

                return s_Current;
            }
        }
    }
}
