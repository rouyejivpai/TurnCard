using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 下一幕选项（占位阶段就是「第 N 战」）。结构来自 <c>Resources/UI/Stage/RouteOption</c>。
    /// </summary>
    public sealed class RouteOptionView : MonoBehaviour
    {
        public const string PrefabResourcePath = "UI/Stage/RouteOption";

        [SerializeField] private Button m_Button;
        [SerializeField] private TextMeshProUGUI m_Label;

        private Action<int> m_OnPick;
        private int m_Index;

        public void Bind(int index, string label, Action<int> onPick)
        {
            m_Index = index;
            m_OnPick = onPick;

            if (m_Label != null)
            {
                m_Label.text = label ?? string.Empty;
            }

            if (m_Button == null)
            {
                return;
            }

            m_Button.onClick.RemoveAllListeners();
            m_Button.onClick.AddListener(() =>
            {
                Action<int> callback = m_OnPick;
                m_OnPick = null;
                callback?.Invoke(m_Index);
            });
            m_Button.interactable = true;
        }
    }
}
