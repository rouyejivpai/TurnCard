using UnityEngine;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 卡牌外形相关的表现参数：可打出描边、悬停投影之外的外观项。
    /// 放在 <c>Resources/Config/CardVisualSettings.asset</c>。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Card Visual Settings", fileName = "CardVisualSettings")]
    public sealed class CardVisualSettings : ScriptableObject
    {
        [Header("可打出描边")]
        [Tooltip("紧贴卡牌的内侧颜色（深）")]
        [SerializeField] private Color m_OutlineColor = new Color(1f, 0.78f, 0.25f, 1f);

        [Tooltip("外沿颜色（浅）")]
        [SerializeField] private Color m_OutlinePaleColor = new Color(1f, 0.95f, 0.75f, 1f);

        [Tooltip("外沿的不透明度：内深外浅就靠它")]
        [Range(0f, 1f)]
        [SerializeField] private float m_OutlineOuterAlpha = 0.35f;

        [Tooltip("描边宽度（像素，按卡面 1:1 算）。不能超过卡框贴图的透明边距")]
        [Range(1f, 12f)]
        [SerializeField] private float m_OutlineWidth = 5f;

        [Tooltip("描边强度")]
        [Range(0f, 4f)]
        [SerializeField] private float m_OutlineIntensity = 1.0f;

        [Tooltip("外圈柔化：越大越柔、越小越像硬描边")]
        [Range(0f, 1f)]
        [SerializeField] private float m_Softness = 0.3f;

        [Header("外扩波")]
        [Tooltip("波纹颜色（一般比描边更白一点）")]
        [SerializeField] private Color m_WaveColor = new Color(1f, 1f, 0.92f, 1f);

        [Tooltip("波纹强度：0 关闭")]
        [Range(0f, 2f)]
        [SerializeField] private float m_WaveStrength = 0.55f;

        [Tooltip("波纹扩散速度（每秒几个周期）")]
        [SerializeField] private float m_WaveSpeed = 1.1f;

        [Tooltip("波纹宽度：占描边宽度的比例（0.35 = 35%）")]
        [Range(0.02f, 1f)]
        [SerializeField] private float m_WaveBand = 0.35f;

        public Color OutlineColor => m_OutlineColor;
        public Color OutlinePaleColor => m_OutlinePaleColor;
        public float OutlineOuterAlpha => Mathf.Clamp01(m_OutlineOuterAlpha);
        public float OutlineWidth => Mathf.Max(1f, m_OutlineWidth);
        public float OutlineIntensity => Mathf.Max(0f, m_OutlineIntensity);
        public float Softness => Mathf.Clamp01(m_Softness);
        public Color WaveColor => m_WaveColor;
        public float WaveStrength => Mathf.Max(0f, m_WaveStrength);
        public float WaveSpeed => m_WaveSpeed;
        public float WaveBand => Mathf.Clamp(m_WaveBand, 0.02f, 1f);

        private const string ResourcePath = "Config/CardVisualSettings";

        private static CardVisualSettings s_Current;

        public static CardVisualSettings Current
        {
            get
            {
                if (s_Current == null)
                {
                    s_Current = Resources.Load<CardVisualSettings>(ResourcePath);
                    if (s_Current == null)
                    {
                        s_Current = CreateInstance<CardVisualSettings>();
                    }
                }

                return s_Current;
            }
        }
    }
}
