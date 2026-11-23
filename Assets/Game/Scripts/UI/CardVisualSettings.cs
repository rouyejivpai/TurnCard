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
        [Tooltip("描边颜色（暖黄偏白最好认）")]
        [SerializeField] private Color m_OutlineColor = new Color(1f, 0.84f, 0.35f, 1f);

        [Tooltip("描边宽度（像素，按卡面 1:1 算）")]
        [Range(1f, 24f)]
        [SerializeField] private float m_OutlineWidth = 6f;

        [Tooltip("光晕图比卡牌大多少像素（要 ≥ 描边宽度，否则会被裁掉）")]
        [Range(2f, 32f)]
        [SerializeField] private float m_OutlinePadding = 12f;

        [Tooltip("描边强度")]
        [Range(0f, 4f)]
        [SerializeField] private float m_OutlineIntensity = 1.2f;

        [Tooltip("呼吸幅度：0 表示常亮不闪")]
        [Range(0f, 1f)]
        [SerializeField] private float m_PulseAmount = 0.22f;

        [Tooltip("呼吸速度（弧度/秒）")]
        [SerializeField] private float m_PulseSpeed = 2.6f;

        [Tooltip("外圈柔化：越大越柔、越小越像硬描边")]
        [Range(0f, 1f)]
        [SerializeField] private float m_Softness = 0.55f;

        public Color OutlineColor => m_OutlineColor;
        public float OutlineWidth => Mathf.Max(1f, m_OutlineWidth);
        public float OutlinePadding => Mathf.Max(2f, m_OutlinePadding);
        public float OutlineIntensity => Mathf.Max(0f, m_OutlineIntensity);
        public float PulseAmount => Mathf.Clamp01(m_PulseAmount);
        public float PulseSpeed => m_PulseSpeed;
        public float Softness => Mathf.Clamp01(m_Softness);

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
