using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 敌人卡死亡表现：刀痕斜切 → 上下两半分离 → 溶解消散。
    ///
    /// 做法：复制卡面的 art / frame 两份（上半、下半），各自挂一个
    /// <c>TurnCard/UICardSlashDeath</c> 材质实例，用 <c>_Half</c> 区分保留哪一侧；
    /// 切割线、位移、旋转、溶解全部在着色器里完成，C# 只驱动材质参数。
    ///
    /// 所有可调参数（时长、位移、旋转、刀痕角度/颜色、刀光轨迹）都在
    /// <c>Resources/UI/Enemy/EnemyDeathSettings.asset</c> 里，改数值不用改代码。
    /// </summary>
    public static class EnemyDeathFx
    {
        private const string MaterialResource = "UI/Enemy/CardSlashDeath";

        /// <summary>整段动画时长（分离结束时收尾），供外部排时序用。</summary>
        public static float TotalDuration => EnemyDeathSettings.Current.TotalDuration;

        /// <summary>
        /// 播死亡动画。材质缺失时退化成「整体淡出」的占位表现，不会卡住流程。
        /// </summary>
        public static void Play(EnemyCardView view, Action onCompleted)
        {
            if (view == null)
            {
                onCompleted?.Invoke();
                return;
            }

            EnemyDeathSettings cfg = EnemyDeathSettings.Current;
            Material baseMaterial = Resources.Load<Material>(MaterialResource);
            RectTransform root = view.Rect;

            // 卡面上的交互与附加信息先收起来
            view.SetDeathVisualHidden(true);

            if (baseMaterial == null || view.ArtImage == null)
            {
                PlayFallback(root, cfg, onCompleted);
                return;
            }

            var topMaterial = new Material(baseMaterial);
            var bottomMaterial = new Material(baseMaterial);
            ApplyCommonProperties(topMaterial, cfg, 0f);
            ApplyCommonProperties(bottomMaterial, cfg, 1f);

            RectTransform container = CreateFullRect(root, "goDeathFx");
            Image topFrame = CloneImage(view.BodyRoot, "imgFrame", container, topMaterial);
            Image topArt = CloneImage(view.BodyRoot, "imgArt", container, topMaterial);
            Image bottomFrame = CloneImage(view.BodyRoot, "imgFrame", container, bottomMaterial);
            Image bottomArt = CloneImage(view.BodyRoot, "imgArt", container, bottomMaterial);

            // 画序：下半的框/立绘在下，上半的框/立绘在上
            bottomFrame.transform.SetAsFirstSibling();
            bottomArt.transform.SetSiblingIndex(1);
            topFrame.transform.SetSiblingIndex(2);
            topArt.transform.SetSiblingIndex(3);

            Sequence seq = DOTween.Sequence();

            // 1) 刀痕扫过：切分线从起始位置扫到卡心，同时刀光划过
            seq.Join(topMaterial.DOFloat(0f, "_CutOffset", cfg.CutDuration).SetEase(Ease.InQuad));
            seq.Join(bottomMaterial.DOFloat(0f, "_CutOffset", cfg.CutDuration).SetEase(Ease.InQuad));

            if (cfg.SlashEnabled)
            {
                RectTransform slash = CreateSlash(container, cfg);
                Image slashImage = slash.GetComponent<Image>();
                seq.Join(slash.DOAnchorPos(cfg.SlashTo, cfg.CutDuration).SetEase(Ease.InQuad));
                seq.Join(slashImage.DOFade(cfg.SlashColor.a, cfg.SlashFadeIn));
                seq.Insert(Mathf.Max(0f, cfg.CutDuration - cfg.SlashFadeOut), slashImage.DOFade(0f, cfg.SlashFadeOut));
            }

            // 2) 两半分离 + 旋转 + 淡出
            float splitStart = cfg.CutDuration;
            float distance = cfg.SplitDistance;
            float angle = cfg.SplitAngle;
            seq.Insert(splitStart, topMaterial.DOVector(new Vector3(0f, distance, 0f), "_HalfOffset", cfg.SplitDuration).SetEase(Ease.OutQuad));
            seq.Insert(splitStart, bottomMaterial.DOVector(new Vector3(0f, -distance, 0f), "_HalfOffset", cfg.SplitDuration).SetEase(Ease.OutQuad));
            seq.Insert(splitStart, topMaterial.DOFloat(angle, "_HalfRotation", cfg.SplitDuration).SetEase(Ease.OutQuad));
            seq.Insert(splitStart, bottomMaterial.DOFloat(-angle, "_HalfRotation", cfg.SplitDuration).SetEase(Ease.OutQuad));

            float fadeStart = splitStart + Mathf.Min(cfg.FadeDelay, cfg.SplitDuration * 0.5f);
            float fadeDuration = Mathf.Max(0.01f, cfg.TotalDuration - fadeStart);
            seq.Insert(fadeStart, topMaterial.DOFloat(0f, "_Alpha", fadeDuration).SetEase(Ease.InQuad));
            seq.Insert(fadeStart, bottomMaterial.DOFloat(0f, "_Alpha", fadeDuration).SetEase(Ease.InQuad));

            // 3) 溶解：在淡出后半段开始，边缘发亮
            float dissolveStart = Mathf.Max(splitStart, cfg.TotalDuration - cfg.DissolveDuration);
            seq.Insert(dissolveStart, topMaterial.DOFloat(1f, "_Dissolve", cfg.DissolveDuration));
            seq.Insert(dissolveStart, bottomMaterial.DOFloat(1f, "_Dissolve", cfg.DissolveDuration));

            seq.SetEase(Ease.Linear);
            seq.OnComplete(() =>
            {
                Cleanup(container);
                // 材质实例必须手动销毁：Unity 不会 GC 运行时 new 出来的 Material
                if (topMaterial != null) UnityEngine.Object.Destroy(topMaterial);
                if (bottomMaterial != null) UnityEngine.Object.Destroy(bottomMaterial);
                onCompleted?.Invoke();
            });
        }

        /// <summary>把配置资产里的值写进材质（材质自身只当默认值）。</summary>
        private static void ApplyCommonProperties(Material material, EnemyDeathSettings cfg, float half)
        {
            material.SetFloat("_Half", half);
            material.SetFloat("_CutOffset", cfg.CutSweepFrom);
            material.SetFloat("_CutAngle", cfg.CutAngle);
            material.SetFloat("_EdgeWidth", cfg.EdgeWidth);
            material.SetColor("_EdgeColor", cfg.EdgeColor);
            material.SetFloat("_DissolveScale", cfg.DissolveScale);
        }

        private static void PlayFallback(RectTransform root, EnemyDeathSettings cfg, Action onCompleted)
        {
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = root.gameObject.AddComponent<CanvasGroup>();
            }

            group.DOFade(0f, cfg.SplitDuration).SetEase(Ease.InQuad);
            root.DOScale(root.localScale.x * 0.92f, cfg.SplitDuration).SetEase(Ease.InQuad)
                .OnComplete(() => onCompleted?.Invoke());
        }

        private static RectTransform CreateFullRect(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        /// <summary>复制一张与源 Image 完全重合的图片，挂到死亡容器里。</summary>
        private static Image CloneImage(RectTransform sourceRoot, string childName, RectTransform parent,
            Material material)
        {
            Transform source = sourceRoot != null ? sourceRoot.Find(childName) : null;
            var sourceImage = source != null ? source.GetComponent<Image>() : null;

            RectTransform rect = CreateFullRect(parent, childName);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.material = material;

            if (sourceImage != null)
            {
                image.sprite = sourceImage.sprite;
                image.color = sourceImage.color;
                image.type = sourceImage.type;
                image.preserveAspect = sourceImage.preserveAspect;

                // 没有立绘的敌人会用占位文字，那种情况没有可复制的图，
                // 关掉这张副本，否则 Image 会渲染成一个白块盖住卡面。
                image.enabled = sourceImage.enabled && sourceImage.sprite != null;
            }
            else
            {
                image.enabled = false;
            }

            return image;
        }

        /// <summary>刀光：一条旋转的长条，从起点扫到终点。</summary>
        private static RectTransform CreateSlash(RectTransform parent, EnemyDeathSettings cfg)
        {
            var go = new GameObject("imgSlash", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = cfg.SlashSize;
            rect.anchoredPosition = cfg.SlashFrom;
            rect.localRotation = Quaternion.Euler(0f, 0f, cfg.SlashAngle);

            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(cfg.SlashColor.r, cfg.SlashColor.g, cfg.SlashColor.b, 0f);

            return rect;
        }

        private static void Cleanup(RectTransform container)
        {
            if (container != null)
            {
                UnityEngine.Object.Destroy(container.gameObject);
            }
        }
    }
}
