using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 开幕幕布：播一张精灵序列（dynamic_curtain_0~9）当作战场开幕动画。
    /// 挂在幕布物体上，BattleForm 开战时会对所有实例调用 <see cref="PlayAll"/>。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class CurtainView : MonoBehaviour
    {
        [Tooltip("用来切换帧的 Image；留空自动取本物体上的")]
        [SerializeField] private Image m_Image;

        [Tooltip("按播放顺序排列的帧（美术是 dynamic_curtain_0~9 的横排精灵条）")]
        [SerializeField] private Sprite[] m_Frames;

        [Tooltip("每秒帧数")]
        [SerializeField] private float m_Fps = 12f;

        [Tooltip("播完停在最后一帧（false 则隐藏）")]
        [SerializeField] private bool m_HoldLastFrame = true;

        [Tooltip("循环播放（调试用）")]
        [SerializeField] private bool m_Loop;

        [Tooltip("用不受 timeScale 影响的时间")]
        [SerializeField] private bool m_UseUnscaledTime = true;

        [Tooltip("播完把幕布压到同层最后（不再盖住手牌/按钮）")]
        [SerializeField] private bool m_SendToBackWhenDone = true;

        private static readonly List<CurtainView> s_Instances = new List<CurtainView>();

        private float m_Timer;
        private int m_Frame;
        private bool m_Playing;
        private bool m_Reverse;

        public bool IsPlaying => m_Playing;
        public int FrameCount => m_Frames == null ? 0 : m_Frames.Length;

        /// <summary>当前帧序号（0 起），调试/外部同步用。</summary>
        public int CurrentFrame => m_Frame;

        /// <summary>播完一帧后触发（参数是当前帧号）。</summary>
        public event System.Action<int> FrameChanged;

        private void Awake()
        {
            if (m_Image == null)
            {
                m_Image = GetComponent<Image>();
            }

            if (m_Image != null)
            {
                // 全屏遮罩不能吃点击
                m_Image.raycastTarget = false;
            }
        }

        private void OnEnable()
        {
            s_Instances.Add(this);
        }

        private void OnDisable()
        {
            s_Instances.Remove(this);
        }

        private void Update()
        {
            if (!m_Playing)
            {
                return;
            }

            float dt = m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            m_Timer += dt * Mathf.Max(1f, m_Fps);

            while (m_Timer >= 1f)
            {
                m_Timer -= 1f;
                Step();
                if (!m_Playing)
                {
                    return;
                }
            }
        }

        private void Step()
        {
            int last = FrameCount - 1;
            if (last < 0)
            {
                m_Playing = false;
                return;
            }

            m_Frame += m_Reverse ? -1 : 1;

            if (m_Frame > last)
            {
                if (m_Loop)
                {
                    m_Frame = 0;
                }
                else
                {
                    m_Frame = last;
                    m_Playing = false;
                    Finish();
                }
            }
            else if (m_Frame < 0)
            {
                if (m_Loop)
                {
                    m_Frame = last;
                }
                else
                {
                    m_Frame = 0;
                    m_Playing = false;
                    Finish();
                }
            }

            ApplyFrame();
        }

        private void Finish()
        {
            if (!m_HoldLastFrame && m_Image != null)
            {
                m_Image.enabled = false;
            }

            // 幕布拉开后露出的边角不该盖住手牌和按钮
            if (m_SendToBackWhenDone)
            {
                transform.SetAsFirstSibling();
            }
        }

        private void ApplyFrame()
        {
            if (m_Image == null || m_Frames == null || m_Frames.Length == 0)
            {
                return;
            }

            m_Frame = Mathf.Clamp(m_Frame, 0, m_Frames.Length - 1);
            m_Image.sprite = m_Frames[m_Frame];
            FrameChanged?.Invoke(m_Frame);
        }

        /// <summary>从头播一遍开幕。</summary>
        public void Play()
        {
            // 播放期间幕布要在最上层，盖住正在登场的手牌
            transform.SetAsLastSibling();

            m_Reverse = false;
            m_Frame = 0;
            m_Timer = 0f;
            m_Playing = true;
            if (m_Image != null)
            {
                m_Image.enabled = true;
            }

            ApplyFrame();
        }

        /// <summary>倒放（闭幕）。</summary>
        public void PlayReverse()
        {
            transform.SetAsLastSibling();

            m_Reverse = true;
            m_Frame = Mathf.Max(0, FrameCount - 1);
            m_Timer = 0f;
            m_Playing = true;
            if (m_Image != null)
            {
                m_Image.enabled = true;
            }

            ApplyFrame();
        }

        public void Stop()
        {
            m_Playing = false;
        }

        /// <summary>直接摆到某一帧（0 起）。</summary>
        public void SetFrame(int frame)
        {
            m_Playing = false;
            m_Frame = Mathf.Clamp(frame, 0, Mathf.Max(0, FrameCount - 1));
            if (m_Image != null)
            {
                m_Image.enabled = true;
            }

            ApplyFrame();
        }

        /// <summary>战斗中每次开战调用：所有幕布从头播一遍。</summary>
        public static void PlayAll()
        {
            for (int i = 0; i < s_Instances.Count; i++)
            {
                if (s_Instances[i] != null)
                {
                    s_Instances[i].Play();
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器用：从 Image 当前 sprite 所在贴图自动收集同名前缀的帧（按后缀数字排序）。
        /// </summary>
        public void CollectFramesFromSprite()
        {
            if (m_Image == null)
            {
                m_Image = GetComponent<Image>();
            }

            if (m_Image == null || m_Image.sprite == null)
            {
                return;
            }

            string path = UnityEditor.AssetDatabase.GetAssetPath(m_Image.sprite);
            var found = new List<Sprite>();
            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                {
                    found.Add(sprite);
                }
            }

            found.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
            m_Frames = found.ToArray();
        }

        private static int FrameIndex(string name)
        {
            int underscore = name.LastIndexOf('_');
            if (underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int index))
            {
                return index;
            }

            return 0;
        }
#endif
    }
}
