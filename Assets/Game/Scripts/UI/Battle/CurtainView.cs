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

            // 一出现就是「关着」的状态，等战斗开始再拉开
            CloseImmediate();
        }

        /// <summary>立刻摆成关幕（第 0 帧、盖在最上层、暂停）。</summary>
        public void CloseImmediate()
        {
            m_Playing = false;
            m_Reverse = false;

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
