using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 奖励宝箱：关闭态 → 点击 → 打开态 → 回调。
    /// 结构来自预制体 <c>Resources/UI/Stage/Chest</c>（关闭/打开两张图，美术可随时替换）。
    /// </summary>
    public sealed class ChestView : MonoBehaviour
    {
        public const string PrefabResourcePath = "UI/Stage/Chest";

        [SerializeField] private Image m_ClosedArt;
        [SerializeField] private Image m_OpenArt;
        [SerializeField] private Button m_Button;
        [SerializeField] private GameObject m_Hint;

        private Action m_OnOpened;
        private bool m_Opened;

        public bool Opened => m_Opened;

        /// <summary>绑定点击回调；宝箱出现时调用。</summary>
        public void Bind(Action onOpened)
        {
            m_OnOpened = onOpened;
            m_Opened = false;
            SetOpened(false);

            if (m_Button == null)
            {
                return;
            }

            m_Button.onClick.RemoveAllListeners();
            m_Button.onClick.AddListener(OnClicked);
            m_Button.interactable = true;
        }

        public void SetOpened(bool opened)
        {
            m_Opened = opened;

            if (m_ClosedArt != null)
            {
                m_ClosedArt.enabled = !opened;
            }

            if (m_OpenArt != null)
            {
                m_OpenArt.enabled = opened;
            }

            if (m_Hint != null)
            {
                m_Hint.SetActive(!opened);
            }
        }

        /// <summary>出现动画：从下方一点落下并淡入。</summary>
        public void PlayAppear(float duration)
        {
            var rect = (RectTransform)transform;
            var group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            rect.DOKill();
            group.DOKill();

            Vector2 target = rect.anchoredPosition;
            rect.anchoredPosition = target + new Vector2(0f, -24f);
            group.alpha = 0f;

            if (duration <= 0.01f)
            {
                group.alpha = 1f;
                rect.anchoredPosition = target;
                return;
            }

            rect.DOAnchorPos(target, duration).SetEase(Ease.OutCubic);
            group.DOFade(1f, duration * 0.8f);
        }

        private void OnClicked()
        {
            if (m_Opened)
            {
                return;
            }

            if (m_Button != null)
            {
                m_Button.interactable = false;
            }

            SetOpened(true);

            // 开箱的「咔哒」一下：轻微弹一下再回调
            var rect = (RectTransform)transform;
            rect.DOKill();
            rect.DOPunchScale(new Vector3(0.06f, 0.06f, 0f), 0.18f, 6, 0.6f);

            Action callback = m_OnOpened;
            m_OnOpened = null;
            callback?.Invoke();
        }
    }
}
