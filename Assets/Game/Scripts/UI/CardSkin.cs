using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 卡牌外观：由「组件精灵表」切出来的五个部件拼成。
    ///
    /// 部件来自一张 AI 生成的精灵表（ArtSource/card/像素卡牌组件精灵表.png），
    /// 已切好放在 Resources/Art/UI/card_parts 下：
    /// 外框、顶部横幅、费用角标、类型小牌、描述底板。
    ///
    /// 结构仍由代码按精确坐标摆放，美术只提供皮肤，所以换皮肤不用改布局。
    /// </summary>
    public static class CardSkin
    {
        /// <summary>总开关：关掉后退回纯色卡（应急用）。</summary>
        public static bool UseParts = true;

        private const string Root = "Art/UI/card_parts/";

        /// <summary>外框在贴图里的角部尺寸（像素），九宫格切分用。</summary>
        private const float FrameBorderPixels = 54f;

        /// <summary>希望边框在屏上显示多宽（占卡宽比例）。</summary>
        private const float FrameBorderRatio = 0.14f;

        private static Sprite s_Frame;
        private static Sprite s_FrameSliced;
        private static Sprite s_Banner;
        private static Sprite s_Badge;
        private static Sprite s_Chip;
        private static Sprite s_Panel;

        public static Sprite Banner => Load("card_banner", ref s_Banner);
        public static Sprite Badge => Load("card_cost_badge", ref s_Badge);
        public static Sprite Chip => Load("card_type_chip", ref s_Chip);
        public static Sprite Panel => Load("card_desc_panel", ref s_Panel);

        private static Sprite Load(string name, ref Sprite cache)
        {
            if (!UseParts)
            {
                return null;
            }

            if (cache == null)
            {
                var tex = Resources.Load<Texture2D>(Root + name);
                if (tex != null)
                {
                    cache = Sprite.Create(
                        tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            return cache;
        }

        /// <summary>外框专用：带九宫格边界的精灵，四角保持原样，四边拉伸。</summary>
        public static Sprite FrameSliced()
        {
            if (!UseParts)
            {
                return null;
            }

            if (s_FrameSliced == null)
            {
                var tex = Resources.Load<Texture2D>(Root + "card_frame");
                if (tex != null)
                {
                    s_FrameSliced = Sprite.Create(
                        tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0,
                        SpriteMeshType.FullRect,
                        new Vector4(FrameBorderPixels, FrameBorderPixels,
                            FrameBorderPixels, FrameBorderPixels));
                }
            }

            return s_FrameSliced;
        }

        /// <summary>把一张 Image 配成九宫格外框。</summary>
        public static void ApplyFrame(Image image)
        {
            if (image == null)
            {
                return;
            }

            Sprite sprite = FrameSliced();
            if (sprite == null)
            {
                image.enabled = false;
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.color = Color.white;

            // uGUI 的 pixelsPerUnitMultiplier 越大，边框显示越细
            float target = CardView.CardWidth * FrameBorderRatio;
            image.pixelsPerUnitMultiplier = Mathf.Clamp(
                FrameBorderPixels / Mathf.Max(1f, target), 0.05f, 10f);
        }

        /// <summary>创建一张部件图（Simple 拉伸，不切片）。</summary>
        public static Image CreatePart(RectTransform parent, string name, Sprite sprite,
            Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }
    }
}
