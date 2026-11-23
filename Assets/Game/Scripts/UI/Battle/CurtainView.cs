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

        [Tooltip("播完把幕布压到最底层（不再盖住手牌/按钮）")]
        [SerializeField] private bool m_SendToBackWhenDone = true;

        private static readonly List<CurtainView> s_Instances = new List<CurtainView>();

        private bool m_Playing;
        private bool m_Reverse;

        public bool IsPlaying => m_Playing;

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
            if (info.IsName(m_StateName) == false)
            {
                return;
            }

            bool done = m_Reverse ? info.normalizedTime <= 0f : info.normalizedTime >= 1f;
            if (done && !m_Animator.IsInTransition(0))
            {
                Finish();
            }
        }

        private void Finish()
        {
            m_Playing = false;
            m_Reverse = false;
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

        /// <summary>倒放（闭幕）。</summary>
        public void PlayReverse()
        {
            m_Playing = false;
            m_Reverse = true;

            if (m_Image != null)
            {
                m_Image.enabled = true;
            }

            transform.SetAsLastSibling();

            if (m_Animator == null || string.IsNullOrEmpty(m_StateName))
            {
                return;
            }

            m_Animator.speed = -1f;
            m_Animator.Play(m_StateName, 0, 1f);
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
    }
}
