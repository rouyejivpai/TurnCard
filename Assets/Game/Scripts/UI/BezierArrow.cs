using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 二次贝塞尔曲线箭头。用于拖拽有目标卡牌时指示目标。
    /// 坐标是自身 RectTransform 的局部坐标，外部通过 <see cref="SetPoints"/> 传入。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BezierArrow : MaskableGraphic
    {
        [SerializeField] private float m_Width = 14f;
        [SerializeField] private float m_HeadLength = 34f;
        [SerializeField] private float m_HeadWidth = 32f;
        [SerializeField] private float m_Curve = 0.22f;
        [SerializeField] private int m_Segments = 24;

        private Vector2 m_Start;
        private Vector2 m_End;

        public float Width
        {
            get => m_Width;
            set { m_Width = value; SetVerticesDirty(); }
        }

        public float Curve
        {
            get => m_Curve;
            set { m_Curve = value; SetVerticesDirty(); }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void SetPoints(Vector2 start, Vector2 end)
        {
            m_Start = start;
            m_End = end;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Vector2 delta = m_End - m_Start;
            float distance = delta.magnitude;
            if (distance < 2f)
            {
                return;
            }

            Vector2 direction = delta / distance;

            // 控制点沿弦的中垂线偏移，并强制取「朝上」那一侧，
            // 这样无论指针在哪个方位，弧线都是向上凸起。
            Vector2 control = ComputeControl(m_Start, m_End, m_Curve);

            int segments = Mathf.Max(2, m_Segments);
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                points[i] = Quadratic(m_Start, control, m_End, t);
            }

            // 线身
            int baseIndex = 0;
            for (int i = 0; i <= segments; i++)
            {
                Vector2 previous = points[Mathf.Max(0, i - 1)];
                Vector2 next = points[Mathf.Min(segments, i + 1)];
                Vector2 tangent = next - previous;
                Vector2 normal = tangent.sqrMagnitude > 0.000001f
                    ? new Vector2(-tangent.y, tangent.x).normalized
                    : Vector2.up;

                float half = m_Width * 0.5f;
                vh.AddVert(points[i] + normal * half, color, Vector2.zero);
                vh.AddVert(points[i] - normal * half, color, Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = baseIndex + i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                vh.AddTriangle(a, c, b);
                vh.AddTriangle(b, c, d);
            }

            // 箭头
            int headIndex = (segments + 1) * 2;
            Vector2 tipTangent = (points[segments] - points[segments - 1]).normalized;
            Vector2 headNormal = new Vector2(-tipTangent.y, tipTangent.x);
            Vector2 headBase = m_End - tipTangent * m_HeadLength;

            vh.AddVert(m_End, color, Vector2.zero);
            vh.AddVert(headBase + headNormal * (m_HeadWidth * 0.5f), color, Vector2.zero);
            vh.AddVert(headBase - headNormal * (m_HeadWidth * 0.5f), color, Vector2.zero);
            vh.AddTriangle(headIndex, headIndex + 1, headIndex + 2);
        }

        private static Vector2 Quadratic(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        /// <summary>
        /// 计算二次贝塞尔的控制点：沿弦的中垂线偏移，并强制取朝上的一侧，
        /// 保证弧线在任何方位下都是「上突」的。抽成静态方法便于单独验证。
        /// </summary>
        public static Vector2 ComputeControl(Vector2 start, Vector2 end, float curve)
        {
            Vector2 delta = end - start;
            float distance = delta.magnitude;
            if (distance < 0.0001f)
            {
                return (start + end) * 0.5f;
            }

            Vector2 direction = delta / distance;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            // 中垂线有两个方向，取朝上的那个
            if (perpendicular.y < 0f)
            {
                perpendicular = -perpendicular;
            }

            // 目标接近正上方时中垂线接近水平，向垂直方向混合，避免弧线朝两侧歪
            perpendicular = (perpendicular + Vector2.up * 0.45f).normalized;

            return (start + end) * 0.5f + perpendicular * (distance * curve);
        }
    }
}
