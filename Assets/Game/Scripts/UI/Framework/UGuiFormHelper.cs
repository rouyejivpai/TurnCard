using GameFramework.UI;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 界面辅助器：把界面实例挂进界面组，补上 RectTransform / CanvasGroup / Raycaster，
    /// 并返回 <see cref="UGuiForm"/> 作为界面的逻辑载体。
    ///
    /// 注意：框架自带的 DefaultUIFormHelper 在 Start 里硬取 ResourceComponent，
    /// 而本工程用的是 EditorResourceComponent（不继承 ResourceComponent），
    /// 直接沿用会 fatal，因此这里独立实现，不依赖 ResourceComponent。
    /// </summary>
    public class UGuiFormHelper : UIFormHelperBase
    {
        public override object InstantiateUIForm(object uiFormAsset)
        {
            GameObject asset = uiFormAsset as GameObject;
            if (asset == null)
            {
                Log.Error("UI form asset '{0}' is invalid.", uiFormAsset);
                return null;
            }

            return Instantiate(asset);
        }

        public override IUIForm CreateUIForm(object uiFormInstance, IUIGroup uiGroup,
            object userData)
        {
            GameObject gameObject = uiFormInstance as GameObject;
            if (gameObject == null)
            {
                Log.Error("UI form instance is invalid.");
                return null;
            }

            Transform transform = gameObject.transform;
            transform.SetParent(((MonoBehaviour)uiGroup.Helper).transform, false);
            transform.localScale = Vector3.one;

            var rect = gameObject.GetOrAddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            gameObject.GetOrAddComponent<CanvasGroup>();
            gameObject.GetOrAddComponent<UnityEngine.UI.GraphicRaycaster>();

            // UIForm 实现 IUIForm，并把生命周期转发给同一物体上的 UIFormLogic。
            // 生成的窗口脚本若已继承 UGuiForm，就不再补默认逻辑。
            if (gameObject.GetComponent<UIFormLogic>() == null)
            {
                gameObject.AddComponent<UGuiForm>();
            }

            return gameObject.GetOrAddComponent<UIForm>();
        }

        public override void ReleaseUIForm(object uiFormAsset, object uiFormInstance)
        {
            if (uiFormInstance != null)
            {
                Destroy((Object)uiFormInstance);
            }

            if (uiFormAsset != null)
            {
                Resources.UnloadAsset((Object)uiFormAsset);
            }
        }
    }
}
