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
    /// 总时长与 <c>BattleForm.ShowResultDelayed</c> 的 0.6s 对齐，
    /// 保证「胜利」字样不会盖在还没消散的卡上。
    /// </summary>
    public static class EnemyDeathFx
    {
        private const string MaterialResource = "UI/Enemy/CardSlashDeath";

        /// <summary>刀痕扫过的时间（此时两半还贴在一起）。</summary>
        private const float CutDuration = 0.10f;

        /// <summary>两半分离 + 淡出的时间。</summary>
        private const float SplitDuration = 0.35f;

        /// <summary>溶解时间（与分离重叠，收尾更干脆）。</summary>
        private const float DissolveDuration = 0.25f;

        /// <summary>整段动画时长。</summary>
        public const float TotalDuration = 0.60f;

        private const float SplitDistance = 36f;
        private const float SplitAngle = 6f;

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

            Material baseMaterial = Resources.Load<Material>(MaterialResource);
            RectTransform root = view.Rect;

            // 卡面上的交互与附加信息先收起来
            view.SetDeathVisualHidden(true);

            if (baseMaterial == null || view.ArtImage == null)
            {
                PlayFallback(root, onCompleted);
                return;
            }

            var topMaterial = new Material(baseMaterial);
            var bottomMaterial = new Material(baseMaterial);
            topMaterial.SetFloat("_Half", 0f);
            bottomMaterial.SetFloat("_Half", 1f);
            topMaterial.SetFloat("_CutOffset", -0.9f);
            bottomMaterial.SetFloat("_CutOffset", -0.9f);

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

            RectTransform slash = CreateSlash(container, topArt.sprite);

            Sequence seq = DOTween.Sequence();

            // 1) 刀痕扫过：切分线从上方扫到卡心，同时刀光划过
            seq.Join(topMaterial.DOFloat(0f, "_CutOffset", CutDuration).SetEase(Ease.InQuad));
            seq.Join(bottomMaterial.DOFloat(0f, "_CutOffset", CutDuration).SetEase(Ease.InQuad));
            seq.Join(slash.DOAnchorPos(new Vector2(110f, -70f), CutDuration).SetEase(Ease.InQuad));
            seq.Join(slash.GetComponent<Image>().DOFade(0.95f, 0.03f));
            seq.Insert(0.06f, slash.GetComponent<Image>().DOFade(0f, 0.09f));

            // 2) 两半分离 + 旋转 + 淡出
            float splitStart = CutDuration;
            seq.Insert(splitStart, topMaterial.DOVector(new Vector3(0f, SplitDistance, 0f), "_HalfOffset", SplitDuration).SetEase(Ease.OutQuad));
            seq.Insert(splitStart, bottomMaterial.DOVector(new Vector3(0f, -SplitDistance, 0f), "_HalfOffset", SplitDuration).SetEase(Ease.OutQuad));
            seq.Insert(splitStart, topMaterial.DOFloat(SplitAngle, "_HalfRotation", SplitDuration).SetEase(Ease.OutQuad));
            seq.Insert(splitStart, bottomMaterial.DOFloat(-SplitAngle, "_HalfRotation", SplitDuration).SetEase(Ease.OutQuad));
            seq.Insert(splitStart + 0.06f, topMaterial.DOFloat(0f, "_Alpha", SplitDuration).SetEase(Ease.InQuad));
            seq.Insert(splitStart + 0.06f, bottomMaterial.DOFloat(0f, "_Alpha", SplitDuration).SetEase(Ease.InQuad));

            // 3) 溶解：在淡出后半段开始，边缘发亮
            float dissolveStart = CutDuration + SplitDuration - DissolveDuration * 0.5f;
            seq.Insert(dissolveStart, topMaterial.DOFloat(1f, "_Dissolve", DissolveDuration));
            seq.Insert(dissolveStart, bottomMaterial.DOFloat(1f, "_Dissolve", DissolveDuration));

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

        private static void PlayFallback(RectTransform root, Action onCompleted)
        {
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = root.gameObject.AddComponent<CanvasGroup>();
            }

            group.DOFade(0f, SplitDuration).SetEase(Ease.InQuad);
            root.DOScale(root.localScale.x * 0.92f, SplitDuration).SetEase(Ease.InQuad)
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

        /// <summary>刀光：一条旋转的白色长条，从左上扫到右下。</summary>
        private static RectTransform CreateSlash(RectTransform parent, Sprite sprite)
        {
            var go = new GameObject("imgSlash", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(300f, 10f);
            rect.anchoredPosition = new Vector2(-110f, 70f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 28f);

            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 0.9f, 0f);
            if (sprite == null)
            {
                // 没有贴图也能画：Image 不给 sprite 时渲染为纯色块
                image.color = new Color(1f, 1f, 0.9f, 0f);
            }

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
