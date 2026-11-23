using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 舞台区：同一块区域按流程阶段换内容。
    ///
    /// <code>
    /// 战斗   → 敌人卡（EnemyArea，仍由 BattleForm 管理）
    /// 奖励房 → 宝箱（关闭 → 点击 → 打开）
    /// 路线   → 若干「下一幕」选项
    /// </code>
    ///
    /// 宝箱与选项都用预制体实例化，不在这里拼布局。
    /// </summary>
    public sealed class BattleStageView : MonoBehaviour
    {
        [Header("阶段容器（预制体里已经摆好，与 EnemyArea 同区域）")]
        [SerializeField] private RectTransform m_ChestRoot;
        [SerializeField] private RectTransform m_RouteRoot;

        [Tooltip("宝箱：预制体里已经摆好就直接用，没摆才按 Resources 实例化")]
        [SerializeField] private ChestView m_Chest;

        private readonly List<RouteOptionView> m_Routes = new List<RouteOptionView>();

        /// <summary>运行时搭舞台区时用：注入两个容器（预制体里则是序列化好的）。</summary>
        public void Initialize(RectTransform chestRoot, RectTransform routeRoot)
        {
            m_ChestRoot = chestRoot;
            m_RouteRoot = routeRoot;
        }

        /// <summary>回到战斗：收起宝箱和路线选项。</summary>
        public void ShowBattle()
        {
            HideChest();
            ClearRoutes();

            if (m_ChestRoot != null)
            {
                m_ChestRoot.gameObject.SetActive(false);
            }

            if (m_RouteRoot != null)
            {
                m_RouteRoot.gameObject.SetActive(false);
            }
        }

        /// <summary>舞台区在预制体里可能是隐藏的，显示内容前先把自己打开。</summary>
        private void EnsureVisible()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 显示宝箱（关闭态），等玩家点击。
        /// 点击后切到打开态并回调，延迟由调用方控制（见 FlowSettings.ChestOpenDelay）。
        /// </summary>
        public void ShowChest(Action onOpened)
        {
            EnsureVisible();
            ClearRoutes();

            if (m_RouteRoot != null)
            {
                m_RouteRoot.gameObject.SetActive(false);
            }

            if (m_ChestRoot == null)
            {
                Debug.LogError("[舞台] 预制体缺少 goChest 容器");
                return;
            }

            m_ChestRoot.gameObject.SetActive(true);

            if (m_Chest == null)
            {
                // 优先用预制体里已经摆好的那张宝箱
                m_Chest = m_ChestRoot.GetComponentInChildren<ChestView>(true);
            }

            if (m_Chest == null)
            {
                m_Chest = CreatePrefab<ChestView>(ChestView.PrefabResourcePath, m_ChestRoot);
            }

            if (m_Chest == null)
            {
                return;
            }

            m_Chest.gameObject.SetActive(true);
            m_Chest.Bind(onOpened);
            m_Chest.PlayAppear(FlowSettings.Current.ChestAppear);
        }

        public void HideChest()
        {
            if (m_Chest != null)
            {
                m_Chest.gameObject.SetActive(false);
            }
        }

        /// <summary>显示下一幕选项；onPick 收到的是选项下标。</summary>
        public void ShowRoutes(int count, Action<int> onPick)
        {
            EnsureVisible();
            HideChest();

            if (m_RouteRoot == null)
            {
                Debug.LogError("[舞台] 预制体缺少 goRoutes 容器");
                return;
            }

            m_RouteRoot.gameObject.SetActive(true);
            ClearRoutes();

            count = Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                RouteOptionView option = CreatePrefab<RouteOptionView>(
                    RouteOptionView.PrefabResourcePath, m_RouteRoot);
                if (option == null)
                {
                    return;
                }

                option.gameObject.SetActive(true);
                option.Bind(i, "第 " + (i + 1) + " 幕", onPick);

                var rect = (RectTransform)option.transform;
                rect.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * 380f, 0f);

                m_Routes.Add(option);

                // 依次淡入，避免三个按钮同时蹦出来
                var group = option.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = option.gameObject.AddComponent<CanvasGroup>();
                }

                group.alpha = 0f;
                group.DOFade(1f, 0.18f).SetDelay(i * 0.06f);
            }
        }

        public void ClearRoutes()
        {
            foreach (RouteOptionView option in m_Routes)
            {
                if (option != null)
                {
                    Destroy(option.gameObject);
                }
            }

            m_Routes.Clear();
        }

        private static T CreatePrefab<T>(string resourcePath, Transform parent) where T : Component
        {
            var prefab = Resources.Load<T>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError("[舞台] 预制体缺失：" + resourcePath);
                return null;
            }

            return Instantiate(prefab, parent, false);
        }
    }
}
