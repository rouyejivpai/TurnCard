using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 简易界面宿主。用框架的 UIForm / UIFormLogic（即 UGuiForm）承载界面，
    /// 但绕开 UIComponent 对资源模块的强依赖——界面预制体直接从 Resources 加载。
    ///
    /// 之所以不用 UIComponent：它按资源名走的加载依赖框架的 Resource 模块，
    /// 而本版本没有「编辑器资源模式」，走官方路径就必须先打 AssetBundle。
    /// </summary>
    public sealed class FormHost : MonoBehaviour
    {
        private RectTransform m_Root;
        private int m_SerialId;
        private int m_SortingTop;

        public static FormHost Create(Transform parent)
        {
            EnsureEventSystem();

            var go = new GameObject("FormHost", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 宿主自己画的东西（关键词提示等）要盖在所有界面之上
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;

            // 像素风：对齐整像素，避免半像素采样导致的抖动
            canvas.pixelPerfect = true;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var host = go.AddComponent<FormHost>();
            host.m_Root = (RectTransform)go.transform;
            return host;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var es = new GameObject("EventSystem", typeof(EventSystem),
                typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(es);
        }

        /// <summary>从 Resources 打开一个界面，返回其界面逻辑。</summary>
        public T Open<T>(string resourcePath, object userData = null) where T : UIFormLogic
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Log.Error("界面资源不存在：{0}", resourcePath);
                return null;
            }

            return Open<T>(prefab, userData);
        }

        public T Open<T>(GameObject prefab, object userData = null) where T : UIFormLogic
        {
            GameObject instance = Instantiate(prefab, m_Root, false);

            var logic = instance.GetComponent<T>();
            if (logic == null)
            {
                // 生成的界面逻辑靠 UIBindMono 在运行时取绑定，因此可以在运行时补挂
                logic = instance.AddComponent<T>();
            }

            return Setup(instance, logic, prefab.name, userData);
        }

        /// <summary>
        /// 打开一个「代码搭建」的界面（没有预制体）。build 回调里自行创建子节点。
        /// 用于流程类的简单界面（主菜单、地图、奖励）。
        /// </summary>
        public T OpenRuntime<T>(string name, System.Action<T> build, object userData = null)
            where T : UIFormLogic
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(m_Root, false);

            var logic = go.AddComponent<T>();
            build?.Invoke(logic);

            return Setup(go, logic, name, userData);
        }

        private T Setup<T>(GameObject instance, T logic, string formName, object userData)
            where T : UIFormLogic
        {
            var rect = instance.GetOrAddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            instance.GetOrAddComponent<CanvasGroup>();
            instance.GetOrAddComponent<GraphicRaycaster>();

            var form = instance.GetOrAddComponent<UIForm>();
            form.OnInit(m_SerialId++, formName, null, false, true, userData);
            form.OnOpen(userData);

            // 后打开的界面必须盖在先前界面之上：
            // 全部是 0 时 Unity 的层级顺序并不可靠，这里显式递增排序值
            var canvas = instance.GetOrAddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10 + (m_SortingTop += 10);

            return logic;
        }

        public void Close(UIForm form)
        {
            if (form == null)
            {
                return;
            }

            form.OnClose(false, null);
            Destroy(form.gameObject);
        }
    }
}
