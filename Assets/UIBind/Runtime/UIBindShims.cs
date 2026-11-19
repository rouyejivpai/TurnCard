using TurnCard.Game.UI;
using UnityEngine;

namespace TurnCard.UIBind
{
    /// <summary>
    /// UIBind 内部日志垫片，避免依赖工程的日志框架。想接自己的日志，只改这一个类。
    /// </summary>
    public static class UIBindLog
    {
        public static void Info(object message) { Debug.Log("[UIBind] " + message); }
        public static void Warning(object message) { Debug.LogWarning("[UIBind] " + message); }
        public static void Error(object message) { Debug.LogError("[UIBind] " + message); }
    }

    /// <summary>
    /// 生成的窗口脚本基类。直接继承框架的 UGuiForm，因此生成的窗口脚本本身就是
    /// GameFramework 认可的界面逻辑（UIFormLogic），不需要额外适配。
    ///
    /// 生命周期映射：OnInit → OnCreate，OnOpen → OnShow，OnClose → OnHide。
    /// </summary>
    public abstract class UIBindWindow : UGuiForm
    {
        protected RectTransform RectTransform => transform as RectTransform;

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            OnCreate();
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            OnShow();
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            OnHide();
            base.OnClose(isShutdown, userData);
        }

        protected virtual void OnCreate() { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
    }

    /// <summary>生成的 Fragment 脚本基类。</summary>
    public abstract class UIBindFragment : MonoBehaviour
    {
        protected RectTransform RectTransform => transform as RectTransform;

        protected virtual void Awake() { OnCreate(); }
        protected virtual void OnEnable() { OnShow(); }
        protected virtual void OnDisable() { OnHide(); }

        protected virtual void OnCreate() { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
    }

    /// <summary>生成的 Item / Cell 脚本基类。</summary>
    public abstract class UIBindItem : MonoBehaviour
    {
        protected RectTransform RectTransform => transform as RectTransform;

        protected virtual void Awake() { OnCreate(); }
        protected virtual void OnCreate() { }
    }
}
