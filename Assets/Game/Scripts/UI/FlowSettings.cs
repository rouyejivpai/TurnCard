using UnityEngine;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 流程表现参数：舞台区各阶段的节奏。
    /// 放在 <c>Resources/Config/FlowSettings.asset</c>。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Flow Settings", fileName = "FlowSettings")]
    public sealed class FlowSettings : ScriptableObject
    {
        [Header("宝箱")]
        [Tooltip("点开宝箱后，等多久弹出选卡奖励（秒）")]
        [SerializeField] private float m_ChestOpenDelay = 0.5f;

        [Tooltip("宝箱从出现到可点击的时长（秒），给一个淡入/落下的余韵")]
        [SerializeField] private float m_ChestAppear = 0.25f;

        [Header("路线")]
        [Tooltip("舞台区显示的下一幕选项数量（占位阶段固定 3）")]
        [Range(1, 5)]
        [SerializeField] private int m_RouteOptionCount = 3;

        public float ChestOpenDelay => Mathf.Max(0f, m_ChestOpenDelay);
        public float ChestAppear => Mathf.Max(0f, m_ChestAppear);
        public int RouteOptionCount => Mathf.Clamp(m_RouteOptionCount, 1, 5);

        private const string ResourcePath = "Config/FlowSettings";

        private static FlowSettings s_Current;

        public static FlowSettings Current
        {
            get
            {
                if (s_Current == null)
                {
                    s_Current = Resources.Load<FlowSettings>(ResourcePath);
                    if (s_Current == null)
                    {
                        s_Current = CreateInstance<FlowSettings>();
                    }
                }

                return s_Current;
            }
        }
    }
}
