using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 开幕幕布：动画本体是原生 Animator（CurtainOpen.anim 打 Image.m_Sprite 关键帧），
    /// 这个组件只负责「开战时播一次 + 播完退到最底层」。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class CurtainView : MonoBehaviour
    {
        [Tooltip("留空自动取本物体上的 Image")]
        [SerializeField] private Image m_Image;

        [Tooltip("留空自动取本物体上的 Animator")]
        [SerializeField] private Animator m_Animator;

        [Tooltip("AnimatorController 里的状态名")]
        [SerializeField] private string m_StateName = "CurtainOpen";

        [Tooltip("关幕用的状态名（同一个动画的反向 clip）")]
        [SerializeField] private string m_CloseStateName = "CurtainClose";

        [Tooltip("关幕动画时长（秒）；用于「等幕布拉上再初始化」")]
        [SerializeField] private float m_CloseSeconds = 0.83f;

        [Tooltip("播完把幕布压到最底层（不再盖住手牌/按钮）")]
        [SerializeField] private bool m_SendToBackWhenDone = true;

        private static readonly List<CurtainView> s_Instances = new List<CurtainView>();

        private bool m_Playing;
        private bool m_Reverse;

        /// <summary>当前是否处于「关幕」状态（第 0 帧）。</summary>
        private bool m_Closed = true;

        public bool IsPlaying => m_Playing;

        public bool IsClosed => m_Closed;

        private void Awake()
        {
            if (m_Image == null)
            {
                m_Image = GetComponent<Image>();
            }

            if (m_Animator == null)
            {
                m_Animator = GetComponent<Animator>();
            }

            if (m_Image != null)
            {
                // 全屏遮挡不能吃点击
                m_Image.raycastTarget = false;
            }

            // 一出现就是「关着」的状态，等战斗开始再拉开
            CloseImmediate();
        }

        /// <summary>立刻摆成关幕（第 0 帧、盖在最上层、暂停）。</summary>
        public void CloseImmediate()
        {
            m_Playing = false;
            m_Reverse = false;
            m_Closed = true;

            if (m_Image != null)
            {
                m_Image.enabled = true;
            }

            transform.SetAsLastSibling();

            if (m_Animator == null)
            {
                return;
            }

            m_Animator.speed = 0f;
            m_Animator.Play(m_StateName, 0, 0f);
            m_Animator.Update(0f);
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
            if (!m_Playing || m_Animator == null)
            {
                return;
            }

            AnimatorStateInfo info = m_Animator.GetCurrentAnimatorStateInfo(0);
            string stateName = m_Reverse ? m_CloseStateName : m_StateName;
            if (!info.IsName(stateName))
            {
                return;
            }

            if (info.normalizedTime >= 1f && !m_Animator.IsInTransition(0))
            {
                Finish();
            }
        }

        private void Finish()
        {
            bool wasClosing = m_Reverse;
            m_Playing = false;
            m_Reverse = false;
            m_Closed = wasClosing;      // 关幕播完 → 关着；开幕播完 → 开着
            if (m_Animator != null)
            {
                m_Animator.speed = 1f;
            }

            // 幕布拉开后露出的边角不该盖住手牌和按钮
            if (m_SendToBackWhenDone)
            {
                transform.SetAsFirstSibling();
            }
        }

        /// <summary>从头播一遍开幕。</summary>
        public void Play()
        {
            m_Playing = false;
            m_Reverse = false;
            m_Closed = false;

            if (m_Image != null)
            {
                m_Image.enabled = true;
            }

            // 播放期间幕布要在最上层，盖住正在登场的手牌
            transform.SetAsLastSibling();

            if (m_Animator == null || string.IsNullOrEmpty(m_StateName))
            {
                return;
            }

            m_Animator.speed = 1f;
            m_Animator.Play(m_StateName, 0, 0f);
            m_Animator.Update(0f);
            m_Playing = true;
        }

        /// <summary>关幕：播同一个动画的反向 clip（Animator 不支持负 speed，所以单独做了一条反向动画）。</summary>
        public void PlayReverse()
        {
            m_Playing = false;
            m_Reverse = true;

            if (m_Image != null)
            {
                m_Image.enabled = true;
            }

            transform.SetAsLastSibling();

            if (m_Animator == null || string.IsNullOrEmpty(m_CloseStateName))
            {
                return;
            }

            m_Animator.speed = 1f;
            m_Animator.Play(m_CloseStateName, 0, 0f);
            m_Animator.Update(0f);
            m_Playing = true;
        }

        public void Stop()
        {
            m_Playing = false;
            if (m_Animator != null)
            {
                m_Animator.speed = 1f;
            }
        }

        /// <summary>关幕动画时长（秒）。取场景里第一个幕布的配置，没有就按 0.83s。</summary>
        public static float CloseDuration
        {
            get
            {
                for (int i = 0; i < s_Instances.Count; i++)
                {
                    if (s_Instances[i] != null)
                    {
                        return Mathf.Max(0f, s_Instances[i].m_CloseSeconds);
                    }
                }

                return 0.83f;
            }
        }

        /// <summary>
        /// 战斗开始前关幕：已经关着就直接保持（不播动画，避免闪一下从「开」跳到「关」），
        /// 开着才倒放关幕。返回 true 表示确实在播关幕动画。
        /// </summary>
        public static bool CloseAnimatedAll()
        {
            bool animated = false;
            for (int i = 0; i < s_Instances.Count; i++)
            {
                CurtainView view = s_Instances[i];
                if (view == null)
                {
                    continue;
                }

                if (view.IsClosed)
                {
                    view.CloseImmediate();
                    continue;
                }

                view.PlayReverse();
                animated = true;
            }

            return animated;
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

        /// <summary>进场时先把所有幕布关上（等战斗真正开始时再拉开）。</summary>
        public static void CloseAll()
        {
            for (int i = 0; i < s_Instances.Count; i++)
            {
                if (s_Instances[i] != null)
                {
                    s_Instances[i].CloseImmediate();
                }
            }
        }

        /// <summary>用开幕动画的倒放来「关幕」：所有幕布一起倒着播回第 0 帧。</summary>
        public static void PlayReverseAll()
        {
            for (int i = 0; i < s_Instances.Count; i++)
            {
                if (s_Instances[i] != null)
                {
                    s_Instances[i].PlayReverse();
                }
            }
        }
    }
}
