using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI.Fx
{
    /// <summary>
    /// UI 层投射物：箭矢走弧线、炮弹走抛物线，命中时炸出冲击波与火花。
    /// 全部用 Image + DOTween，在 Screen Space Overlay 下也能正常显示。
    /// </summary>
    public static class ProjectileView
    {
        private const float ArrowDuration = 0.20f;
        private const float CannonDuration = 0.38f;
        private const float TrailInterval = 0.028f;

        /// <summary>朝目标射出一发。onImpact 在命中瞬间回调（放音效/抖动）。</summary>
        public static void Play(RectTransform layer, Vector2 from, Vector2 to, bool cannon,
            Action onImpact = null, float? durationOverride = null)
        {
            if (layer == null)
            {
                return;
            }

            // 特效层可能被后建的 UI 压在下面，每次播放前提到最上层
            layer.SetAsLastSibling();

            float duration = durationOverride ?? (cannon ? CannonDuration : ArrowDuration);

            // 弧线控制点：箭抬得低，炮弹抬得高
            float arc = cannon ? 220f : 70f;
            Vector2 control = (from + to) * 0.5f + new Vector2(0f, arc);

            RectTransform body = CreateImage(layer, cannon ? UiSprites.Ball : UiSprites.Arrow,
                cannon ? new Vector2(30f, 30f) : new Vector2(52f, 18f), from);
            var bodyImage = body.GetComponent<Image>();
            bodyImage.color = Color.white;

            MuzzleFlash(layer, from, cannon);

            float elapsed = 0f;
            float trailTimer = 0f;
            float lastAngle = 0f;

            DOTween.To(() => 0f, t =>
            {
                Vector2 pos = Bezier(from, control, to, t);
                body.anchoredPosition = pos;

                if (cannon)
                {
                    body.localEulerAngles = new Vector3(0f, 0f, -t * 720f);
                }
                else
                {
                    Vector2 next = Bezier(from, control, to, Mathf.Min(1f, t + 0.02f));
                    Vector2 dir = next - pos;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        lastAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        body.localEulerAngles = new Vector3(0f, 0f, lastAngle);
                    }
                }

                // 拖尾
                trailTimer += Time.deltaTime;
                if (trailTimer >= TrailInterval && t < 0.95f)
                {
                    trailTimer = 0f;
                    SpawnTrail(layer, pos, cannon);
                }

                elapsed = t;
            }, 1f, duration).SetEase(cannon ? Ease.InQuad : Ease.Linear).OnComplete(() =>
            {
                if (body != null) UnityEngine.Object.Destroy(body.gameObject);
                Impact(layer, to, cannon);
                onImpact?.Invoke();
            });
        }

        private static void SpawnTrail(RectTransform layer, Vector2 at, bool cannon)
        {
            RectTransform t = CreateImage(layer, UiSprites.Dot, 
                cannon ? new Vector2(16f, 16f) : new Vector2(8f, 8f), at);
            var img = t.GetComponent<Image>();
            img.color = cannon
                ? new Color(0.7f, 0.68f, 0.7f, 0.55f)
                : new Color(1f, 0.95f, 0.8f, 0.5f);

            float life = cannon ? 0.35f : 0.16f;
            t.DOScale(cannon ? 2.2f : 0.4f, life);
            img.DOFade(0f, life).OnComplete(() => UnityEngine.Object.Destroy(t.gameObject));
        }

        private static void MuzzleFlash(RectTransform layer, Vector2 at, bool cannon)
        {
            RectTransform flash = CreateImage(layer, UiSprites.Dot,
                cannon ? new Vector2(46f, 46f) : new Vector2(20f, 20f), at);
            var img = flash.GetComponent<Image>();
            img.color = cannon ? new Color(1f, 0.78f, 0.35f, 0.95f) : new Color(1f, 1f, 0.85f, 0.8f);

            flash.DOScale(cannon ? 1.6f : 1.2f, 0.14f);
            img.DOFade(0f, 0.14f).OnComplete(() => UnityEngine.Object.Destroy(flash.gameObject));
        }

        /// <summary>命中：冲击波圆环 + 若干火花。</summary>
        public static void Impact(RectTransform layer, Vector2 at, bool cannon)
        {
            RectTransform ring = CreateImage(layer, UiSprites.Ring, new Vector2(28f, 28f), at);
            var ringImg = ring.GetComponent<Image>();
            ringImg.color = new Color(1f, 0.9f, 0.6f, 0.95f);

            float ringScale = cannon ? 3.4f : 2.4f;
            ring.DOScale(ringScale, 0.28f).SetEase(Ease.OutQuad);
            ringImg.DOFade(0f, 0.28f).OnComplete(() => UnityEngine.Object.Destroy(ring.gameObject));

            int sparks = cannon ? 10 : 6;
            for (int i = 0; i < sparks; i++)
            {
                float angle = (360f / sparks) * i + UnityEngine.Random.Range(-14f, 14f);
                float rad = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float dist = UnityEngine.Random.Range(cannon ? 60f : 34f, cannon ? 130f : 70f);

                RectTransform spark = CreateImage(layer, UiSprites.Dot,
                    cannon ? new Vector2(10f, 10f) : new Vector2(6f, 6f), at);
                var sparkImg = spark.GetComponent<Image>();
                sparkImg.color = new Color(1f, UnityEngine.Random.Range(0.6f, 0.95f), 0.35f, 1f);

                spark.DOAnchorPos(at + dir * dist, 0.3f).SetEase(Ease.OutQuad);
                sparkImg.DOFade(0f, 0.3f).OnComplete(() => UnityEngine.Object.Destroy(spark.gameObject));
            }


        }

        private static RectTransform CreateImage(RectTransform parent, Sprite sprite, Vector2 size, Vector2 pos)
        {
            var go = new GameObject("Fx", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            rect.localScale = Vector3.one;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = false;

            rect.SetAsLastSibling();
            return rect;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
