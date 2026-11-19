using UnityEngine;

namespace TurnCard.Game.UI.Fx
{
    /// <summary>
    /// 程序化生成的小图，避免为特效单独准备美术资源。
    /// 全部按像素风画：硬边缘、无渐变。
    /// </summary>
    public static class UiSprites
    {
        private static Sprite s_Arrow;
        private static Sprite s_Ball;
        private static Sprite s_Ring;
        private static Sprite s_Dot;
        private static Sprite s_Square;

        /// <summary>箭头：朝右，长度 26，宽 10。</summary>
        public static Sprite Arrow
        {
            get
            {
                if (s_Arrow == null)
                {
                    const int w = 40, h = 12;
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            px[y * w + x] = new Color32(0, 0, 0, 0);
                        }
                    }

                    // 杆（中线在 y=5.5，占 y=5..6）
                    for (int x = 4; x <= 26; x++)
                    {
                        px[5 * w + x] = new Color32(214, 198, 168, 255);
                        px[6 * w + x] = new Color32(178, 160, 132, 255);
                    }

                    // 箭头三角：从 x=26 到 x=39，半高从 5 收到 0
                    for (int x = 26; x < w; x++)
                    {
                        int half = (int)Mathf.Round((w - 1 - x) * (5f / (w - 1 - 26)));
                        for (int dy = -half; dy <= half; dy++)
                        {
                            int yy = 5 + dy;
                            if (yy >= 0 && yy < h) px[yy * w + x] = new Color32(244, 238, 220, 255);
                        }
                    }

                    // 尾羽
                    for (int x = 0; x <= 3; x++)
                    {
                        for (int y = 3; y <= 8; y++)
                        {
                            px[y * w + x] = x <= 1
                                ? new Color32(198, 96, 82, 255)
                                : new Color32(232, 128, 104, 255);
                        }
                    }

                    s_Arrow = Make(px, w, h, new Vector2(0.5f, 0.5f));
                }

                return s_Arrow;
            }
        }

        /// <summary>炮弹：16×16 的圆球，带一点中空高光。</summary>
        public static Sprite Ball
        {
            get
            {
                if (s_Ball == null)
                {
                    const int s = 16;
                    var px = new Color32[s * s];
                    float r = s * 0.5f - 0.5f;
                    for (int y = 0; y < s; y++)
                    {
                        for (int x = 0; x < s; x++)
                        {
                            float dx = x - r, dy = y - r;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d > r) { px[y * s + x] = new Color32(0, 0, 0, 0); continue; }
                            bool edge = d > r - 1.5f;
                            bool highlight = (x - (r - 2f)) * (x - (r - 2f)) + (y - (r + 2f)) * (y - (r + 2f)) < 5f;
                            px[y * s + x] = edge
                                ? new Color32(30, 28, 34, 255)
                                : highlight
                                    ? new Color32(150, 150, 160, 255)
                                    : new Color32(74, 72, 82, 255);
                        }
                    }

                    s_Ball = Make(px, s, s, new Vector2(0.5f, 0.5f));
                }

                return s_Ball;
            }
        }

        /// <summary>空心圆环，用于命中时的扩散冲击波。</summary>
        public static Sprite Ring
        {
            get
            {
                if (s_Ring == null)
                {
                    const int s = 64;
                    var px = new Color32[s * s];
                    float c = s * 0.5f - 0.5f;
                    float rOuter = s * 0.5f - 1f;
                    float rInner = rOuter - 4f;
                    for (int y = 0; y < s; y++)
                    {
                        for (int x = 0; x < s; x++)
                        {
                            float dx = x - c, dy = y - c;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            byte a = (byte)(d <= rOuter && d >= rInner ? 255 : 0);
                            px[y * s + x] = new Color32(255, 255, 255, a);
                        }
                    }

                    s_Ring = Make(px, s, s, new Vector2(0.5f, 0.5f));
                }

                return s_Ring;
            }
        }

        /// <summary>4×4 的小方点，做火花和残影。</summary>
        public static Sprite Dot
        {
            get
            {
                if (s_Dot == null)
                {
                    const int s = 4;
                    var px = new Color32[s * s];
                    for (int i = 0; i < px.Length; i++)
                    {
                        px[i] = new Color32(255, 255, 255, 255);
                    }

                    s_Dot = Make(px, s, s, new Vector2(0.5f, 0.5f));
                }

                return s_Dot;
            }
        }

        /// <summary>纯白方块，做枪口闪光。</summary>
        public static Sprite Square
        {
            get
            {
                if (s_Square == null)
                {
                    const int s = 8;
                    var px = new Color32[s * s];
                    for (int i = 0; i < px.Length; i++)
                    {
                        px[i] = new Color32(255, 255, 255, 255);
                    }

                    s_Square = Make(px, s, s, new Vector2(0.5f, 0.5f));
                }

                return s_Square;
            }
        }

        private static Sprite Make(Color32[] pixels, int w, int h, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 100f);
        }
    }
}
