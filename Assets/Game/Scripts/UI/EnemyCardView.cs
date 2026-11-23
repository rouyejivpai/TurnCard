using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 敌人卡的视图。与玩家卡**版面完全不同**（立绘铺满 + 意图气泡 + 血量横幅），
    /// 因此不共用 <see cref="CardView"/> 的实现，只实现 <see cref="ICardFlipView"/> 这一个契约。
    ///
    /// 结构与坐标来自预制体 <c>Resources/UI/Enemy/EnemyCard</c>（48×64 网格 ×4 = 192×256）：
    /// <code>
    /// EnemyCard（根 = 卡框，也是唯一命中区）
    /// ├─ goBody      旋转节点：imgArt（立绘）+ imgFrame（卡框）
    /// ├─ goIntent    意图气泡（溢出在卡框上方）
    /// ├─ goHpBanner  血量横幅（挂在卡框下方）
    /// └─ goArmor     护甲角标（左上角内侧）
    /// </code>
    /// 标准见 <c>Docs/21-敌人卡面.md</c>。
    /// </summary>
    public sealed class EnemyCardView : MonoBehaviour, ICardFlipView
    {
        private const string PrefabResourcePath = "UI/Enemy/EnemyCard";

        // ---- 预制体绑定 ----
        [Header("旋转体（立绘 + 卡框）")]
        [SerializeField] private RectTransform m_Body;
        [SerializeField] private Image m_Art;
        [SerializeField] private TextMeshProUGUI m_Placeholder;

        [Header("意图气泡")]
        [SerializeField] private GameObject m_IntentRoot;
        [SerializeField] private Image m_IntentIcon;
        [SerializeField] private TextMeshProUGUI m_IntentValue;

        [Header("意图气泡 · 附加意图（如攻击同时加贪婪）")]
        [SerializeField] private GameObject m_ExtraIntentRoot;
        [SerializeField] private Image m_ExtraIntentIcon;
        [SerializeField] private TextMeshProUGUI m_ExtraIntentValue;

        [Header("血量横幅")]
        [SerializeField] private RectTransform m_HpClip;
        [SerializeField] private Image m_HpFill;
        [SerializeField] private TextMeshProUGUI m_HpText;

        [Header("护甲角标")]
        [SerializeField] private GameObject m_ArmorRoot;
        [SerializeField] private TextMeshProUGUI m_ArmorText;

        private static EnemyCardView s_Prefab;
        private static Sprite[] s_IntentSprites;
        private static IntentIconSet s_IntentIconSet;
        private static bool s_IntentIconSetLoaded;

        private const string IntentIconSetResource = "Config/IntentIconSet";

        private struct QueuedFlip
        {
            public CardFaceSide Side;
            public float Duration;
            public Action Done;
            public AudioSource Audio;
        }

        private readonly Queue<QueuedFlip> m_FlipQueue = new Queue<QueuedFlip>();
        private Tween m_FlipTween;
        private BuffIconStrip m_BuffStrip;
        private float m_BaseScale = 1f;
        private float m_BodyBaseScale = 1f;
        private CardFaceSide m_ShownSide = CardFaceSide.Front;

        public EnemyInstance Enemy { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        public CardFaceSide ShownSide => m_ShownSide;
        public bool IsFlipping { get; private set; }

        /// <summary>正在播死亡动画（战斗层据此跳过后续刷新）。</summary>
        public bool IsDying { get; private set; }

        internal Image ArtImage => m_Art;
        internal RectTransform BodyRoot => m_Body;

        /// <summary>
        /// 进入/退出死亡表现：收起气泡、血条、护甲和本体（立绘与卡框由死亡特效自己复制）。
        /// </summary>
        public void SetDeathVisualHidden(bool hidden)
        {
            IsDying = hidden;

            if (m_Body != null)
            {
                m_Body.gameObject.SetActive(!hidden);
            }

            if (m_IntentRoot != null)
            {
                m_IntentRoot.SetActive(!hidden);
            }

            if (m_ArmorRoot != null)
            {
                m_ArmorRoot.SetActive(!hidden);
            }

            Transform banner = transform.Find("goHpBanner");
            if (banner != null)
            {
                banner.gameObject.SetActive(!hidden);
            }
        }

        /// <summary>按预制体创建一张敌人卡（预制体缺失时返回 null 并报错，不会静默画一个空壳）。</summary>
        public static EnemyCardView Create(Transform parent, EnemyInstance enemy)
        {
            EnemyCardView prefab = LoadPrefab();
            if (prefab == null)
            {
                Debug.LogError("[敌人卡] 预制体缺失：" + PrefabResourcePath);
                return null;
            }

            EnemyCardView view = Instantiate(prefab, parent, false);
            view.name = "Enemy_" + enemy.Definition.Id;
            view.Bind(enemy);
            return view;
        }

        private static EnemyCardView LoadPrefab()
        {
            if (s_Prefab == null)
            {
                s_Prefab = Resources.Load<EnemyCardView>(PrefabResourcePath);
            }

            return s_Prefab;
        }

        private void Bind(EnemyInstance enemy)
        {
            Enemy = enemy;
            m_BaseScale = transform.localScale.x;
            m_BodyBaseScale = m_Body != null ? m_Body.localScale.x : 1f;

            // buff 图标条挂在卡框内侧左下角；它自己订阅容器事件自动刷新
            if (m_BuffStrip == null && Enemy.Buffs != null)
            {
                m_BuffStrip = BuffIconStrip.Attach(Rect, Enemy.Buffs, Vector2.zero, Vector2.zero,
                    new Vector2(14f, 10f));
            }

            RefreshVisual();
        }

        // ---------------------------------------------------------------- 刷新

        /// <summary>整体刷新（形态、血量、护甲、buff、意图）。跟随逻辑朝向。</summary>
        public void RefreshVisual()
        {
            if (Enemy == null)
            {
                return;
            }

            m_ShownSide = Enemy.Side;
            RefreshArt(m_ShownSide);
            RefreshIntent();
            RefreshHp();
            RefreshArmor();
        }

        /// <summary>
        /// 只换立绘：翻面动画在 90° 时调用，避免整块重建闪烁。
        /// 取的是**当前显示的那一面**（而不是逻辑朝向），否则翻到背面时会看到正面的立绘。
        /// </summary>
        private void RefreshArt(CardFaceSide side)
        {
            EnemyForm form = Enemy.Definition.GetForm(side);

            if (m_Art != null)
            {
                m_Art.sprite = form.Art;
                m_Art.enabled = form.Art != null;
            }

            // 没配立绘时用占位色块 + 形态名，绝不静默留白（标准 §7）
            if (m_Placeholder != null)
            {
                bool show = form.Art == null;
                m_Placeholder.gameObject.SetActive(show);
                m_Placeholder.text = show ? form.DisplayName : string.Empty;
            }
        }

        /// <summary>头顶意图：图标 + 数值，多段攻击显示 6×2。</summary>
        private void RefreshIntent()
        {
            EnemyIntentDefinition intent = Enemy.CurrentIntent;
            if (m_IntentRoot == null)
            {
                return;
            }

            if (intent == null)
            {
                m_IntentRoot.SetActive(false);
                return;
            }

            m_IntentRoot.SetActive(true);

            if (m_IntentIcon != null)
            {
                m_IntentIcon.sprite = ResolveIntentIcon(intent);
                m_IntentIcon.preserveAspect = true;
            }

            if (m_IntentValue == null)
            {
                return;
            }

            switch (intent.Intent)
            {
                case EnemyIntentType.Attack:
                    m_IntentValue.text = intent.Hits > 1
                        ? $"{intent.Value}\u00d7{intent.Hits}"
                        : intent.Value.ToString();
                    m_IntentValue.color = new Color(1f, 0.72f, 0.62f);
                    break;
                case EnemyIntentType.Defend:
                    m_IntentValue.text = intent.Value.ToString();
                    m_IntentValue.color = new Color(0.66f, 0.84f, 1f);
                    break;
                case EnemyIntentType.Buff:
                    m_IntentValue.text = "+" + intent.Value;
                    m_IntentValue.color = new Color(0.75f, 1f, 0.7f);
                    break;
                case EnemyIntentType.Debuff:
                case EnemyIntentType.ApplyBuff:
                    bool toPlayer = intent.Intent == EnemyIntentType.Debuff ||
                                    intent.BuffTarget == BuffTarget.Player;
                    m_IntentValue.text = (toPlayer ? "-" : "+") + intent.Value;
                    m_IntentValue.color = toPlayer
                        ? new Color(1f, 0.72f, 0.86f)
                        : new Color(0.75f, 1f, 0.7f);
                    if (m_IntentIcon != null)
                    {
                        m_IntentIcon.sprite = ResolveIntentIcon(intent,
                            toPlayer ? EnemyIntentType.Debuff : EnemyIntentType.Buff);
                    }

                    break;

                // 晕眩 / 强制翻面：数值没有意义，只留图标（专属图标待美术，见标准 §6）
                case EnemyIntentType.Stun:
                case EnemyIntentType.FlipSelf:
                    m_IntentValue.text = string.Empty;
                    m_IntentValue.color = new Color(1f, 0.85f, 0.55f);
                    break;

                // 回血 / 全体强化：用增益色，图标借「增益」那张
                case EnemyIntentType.Heal:
                    m_IntentValue.text = "+" + intent.Value;
                    m_IntentValue.color = new Color(0.62f, 1f, 0.72f);
                    if (m_IntentIcon != null)
                    {
                        m_IntentIcon.sprite = ResolveIntentIcon(intent, EnemyIntentType.Buff);
                    }

                    break;

                case EnemyIntentType.BuffAllies:
                    m_IntentValue.text = "+" + intent.Value;
                    m_IntentValue.color = new Color(0.75f, 1f, 0.7f);
                    if (m_IntentIcon != null)
                    {
                        m_IntentIcon.sprite = ResolveIntentIcon(intent, EnemyIntentType.Buff);
                    }

                    break;
                default:
                    m_IntentValue.text = intent.Value.ToString();
                    m_IntentValue.color = new Color(1f, 0.85f, 0.55f);
                    break;
            }

            RefreshExtraIntent(intent);
        }

        /// <summary>
        /// 附加意图：这条招式除了主效果外还附带 buff（如浮士德「攻击并 +1 贪婪」），
        /// 就在主图标右侧再显示一个「强化」图标 + 层数，形成双意图。
        /// </summary>
        private void RefreshExtraIntent(EnemyIntentDefinition intent)
        {
            if (m_ExtraIntentRoot == null)
            {
                return;
            }

            bool hasExtra = intent != null && intent.BuffId != 0 &&
                            intent.Intent != EnemyIntentType.ApplyBuff &&
                            intent.Intent != EnemyIntentType.BuffAllies;

            m_ExtraIntentRoot.SetActive(hasExtra);
            if (!hasExtra)
            {
                return;
            }

            if (m_ExtraIntentIcon != null)
            {
                m_ExtraIntentIcon.sprite = ResolveIntentIcon(intent, EnemyIntentType.Buff);
                m_ExtraIntentIcon.preserveAspect = true;
            }

            if (m_ExtraIntentValue != null)
            {
                m_ExtraIntentValue.text = "+" + intent.BuffStacks;
            }
        }

        /// <summary>
        /// 血量横幅：数字 + 用 RectMask2D 的裁剪窗口按比例露出实心条。
        /// 实心条本身尺寸不变（不会被拉扁），只是被裁掉右侧。
        /// </summary>
        private void RefreshHp()
        {
            EnemyForm form = Enemy.Form;
            float ratio = form.MaxHp <= 0 ? 0f : Mathf.Clamp01(Enemy.Hp / (float)form.MaxHp);

            if (m_HpClip != null)
            {
                // 裁剪窗口从左往右按比例伸缩：满血 1，空血 0
                Vector2 anchorMax = m_HpClip.anchorMax;
                anchorMax.x = ratio;
                m_HpClip.anchorMax = anchorMax;
            }
            else if (m_HpFill != null)
            {
                // 没有裁剪窗口时的兜底：退回 Image.Filled 的写法
                m_HpFill.fillAmount = ratio;
            }

            if (m_HpText != null)
            {
                m_HpText.text = Enemy.Hp + "/" + form.MaxHp;
            }
        }

        private void RefreshArmor()
        {
            bool hasArmor = Enemy.Armor > 0;
            if (m_ArmorRoot != null)
            {
                m_ArmorRoot.SetActive(hasArmor);
            }

            if (hasArmor && m_ArmorText != null)
            {
                m_ArmorText.text = Enemy.Armor.ToString();
            }
        }

        /// <summary>
        /// 意图图标解析，优先级从高到低：
        /// <list type="number">
        /// <item>意图行自己的 <c>icon</c>（字符串覆盖，特例用）；</item>
        /// <item><c>Resources/UI/Enemy/IntentIconSet</c> 里按枚举配的 Sprite（**主要配置入口**）；</item>
        /// <item>内置的 4 张基础图按类型回退。</item>
        /// </list>
        /// </summary>
        private static Sprite ResolveIntentIcon(EnemyIntentDefinition intent,
            EnemyIntentType? overrideType = null)
        {
            EnemyIntentType type = overrideType ?? (intent != null ? intent.Intent : EnemyIntentType.Attack);

            // 1) 行级覆盖
            if (intent != null && !string.IsNullOrEmpty(intent.Icon))
            {
                Sprite configured = LoadIntentSprite(intent.Icon);
                if (configured != null)
                {
                    return configured;
                }
            }

            // 2) 枚举 → Sprite 的配置表
            IntentIconSet set = LoadIntentIconSet();
            if (set != null)
            {
                Sprite fromSet = set.Get(type);
                if (fromSet != null)
                {
                    return fromSet;
                }
            }

            // 3) 内置回退
            return IntentSprite(type);
        }

        private static IntentIconSet LoadIntentIconSet()
        {
            if (!s_IntentIconSetLoaded)
            {
                s_IntentIconSet = Resources.Load<IntentIconSet>(IntentIconSetResource);
                s_IntentIconSetLoaded = true;
            }

            return s_IntentIconSet;
        }

        /// <summary>
        /// 意图图标：按 <c>intent_&lt;枚举名小写&gt;</c> 从 <c>Resources/Art/UI/enemy/</c> 找图，
        /// 找不到就回退到四张基础图。美术新画一张 <c>intent_stun.png</c> 丢进去即可生效，不用改代码。
        /// </summary>
        private static Sprite IntentSprite(EnemyIntentType type)
        {
            if (s_IntentSprites == null)
            {
                s_IntentSprites = new Sprite[7];
            }

            int index = (int)type;
            if (index < 0 || index >= s_IntentSprites.Length)
            {
                return null;
            }

            if (s_IntentSprites[index] != null)
            {
                return s_IntentSprites[index];
            }

            // 下标与 EnemyIntentType 顺序一致：Attack/Defend/Buff/Debuff/ApplyBuff/Stun/FlipSelf
            string[] names =
            {
                "intent_attack", "intent_defend", "intent_buff", "intent_debuff",
                "intent_applybuff", "intent_stun", "intent_flip",
            };

            Sprite sprite = LoadIntentSprite(names[index]);

            // 缺图回退：施加 buff 用减益图，其余用防御图
            if (sprite == null)
            {
                sprite = type == EnemyIntentType.ApplyBuff
                    ? LoadIntentSprite("intent_debuff")
                    : LoadIntentSprite("intent_defend");
            }

            s_IntentSprites[index] = sprite;
            return sprite;
        }

        private static Sprite LoadIntentSprite(string name)
        {
            var tex = Resources.Load<Texture2D>("Art/UI/enemy/" + name);
            if (tex == null)
            {
                return null;
            }

            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>指针悬停时的轻微放大，相对自身基准缩放。</summary>
        public void SetHighlight(bool highlighted)
        {
            transform.localScale = Vector3.one * (m_BaseScale * (highlighted ? 1.04f : 1f));
        }

        // ---------------------------------------------------------------- 翻面

        /// <summary>战斗表现层的入口：翻到敌人当前形态那一面。</summary>
        public void PlayFlip(float duration, AudioSource audio, Action onCompleted = null)
        {
            CardFaceSide target = Enemy.Side;
            PlayFlipTo(target, duration, audio, onCompleted);
        }

        public void PlayFlipTo(CardFaceSide side, float duration, AudioSource audio = null,
            Action onCompleted = null)
        {
            m_FlipQueue.Enqueue(new QueuedFlip
            {
                Side = side,
                Duration = duration,
                Done = onCompleted,
                Audio = audio,
            });

            if (!IsFlipping)
            {
                StartNextFlip();
            }
        }

        public void ApplySide(CardFaceSide side, bool animate, float duration, AudioSource audio,
            Action onCompleted)
        {
            if (!animate)
            {
                // 直接摆面：先掐掉正在跑的动画，否则两者会互相抢角度
                m_FlipTween?.Kill();
                IsFlipping = false;

                m_ShownSide = side;
                if (m_Body != null)
                {
                    m_Body.localEulerAngles = new Vector3(0f, side == CardFaceSide.Back ? 180f : 0f, 0f);
                    m_Body.localScale = new Vector3(m_BodyBaseScale, m_BodyBaseScale, 1f);
                }

                RefreshArt(side);
                onCompleted?.Invoke();
                return;
            }

            PlayFlipTo(side, duration, audio, onCompleted);
        }

        private void StartNextFlip()
        {
            if (m_FlipQueue.Count == 0)
            {
                IsFlipping = false;
                return;
            }

            QueuedFlip queued = m_FlipQueue.Dequeue();
            IsFlipping = true;

            if (m_Body == null)
            {
                m_ShownSide = queued.Side;
                IsFlipping = false;
                queued.Done?.Invoke();
                StartNextFlip();
                return;
            }

            float targetY = queued.Side == CardFaceSide.Back ? 180f : 0f;

            // 已经停在目标面（连翻两次会回到原点）：先摆到对侧再翻，
            // 否则会「角度相同」直接结束，看不见任何动画。
            if (Mathf.Approximately(NormalizeAngle(m_Body.localEulerAngles.y), targetY))
            {
                float oppositeY = queued.Side == CardFaceSide.Back ? 0f : 180f;
                m_Body.localEulerAngles = new Vector3(0f, oppositeY, 0f);
                m_ShownSide = queued.Side == CardFaceSide.Back ? CardFaceSide.Front : CardFaceSide.Back;
                RefreshArt(m_ShownSide);
            }

            if (queued.Audio != null && queued.Audio.clip != null)
            {
                queued.Audio.PlayOneShot(queued.Audio.clip);
            }

            // 与玩家卡完全一致的「三段式」：抬起 → 翻转（带 Z 轴甩动）→ 落下。
            // 参数必须和 CardView.ApplySide 保持一致，否则两种卡翻面手感不同（Docs/03 第 5 节）。
            float from = NormalizeAngle(m_Body.localEulerAngles.y);
            float to = targetY;
            float scaleBase = m_BodyBaseScale;
            float tiltSign = to > from ? -1f : 1f;

            m_FlipTween?.Kill();
            m_FlipTween = DOTween.To(() => 0f, t =>
                {
                    float y = Mathf.Lerp(from, to, t);
                    float lift = Mathf.Sin(t * Mathf.PI);   // 0 → 1 → 0

                    m_Body.localEulerAngles = new Vector3(0f, y, lift * 7f * tiltSign);
                    float s = scaleBase * (1f + 0.06f * lift);
                    m_Body.localScale = new Vector3(s, s, 1f);

                    // 转过 90° 就换面：此时卡面近乎侧对镜头，切换不可见
                    float angle = NormalizeAngle(y);
                    CardFaceSide visible = angle < 90f || angle > 270f
                        ? CardFaceSide.Front
                        : CardFaceSide.Back;
                    if (visible != m_ShownSide)
                    {
                        m_ShownSide = visible;
                        RefreshArt(visible);
                    }
                }, 1f, Mathf.Max(0.01f, queued.Duration))
                .SetEase(Ease.InOutQuad)
                .OnComplete(() =>
                {
                    m_Body.localEulerAngles = new Vector3(0f, to, 0f);
                    m_Body.localScale = new Vector3(scaleBase, scaleBase, 1f);
                    m_ShownSide = queued.Side;
                    RefreshArt(queued.Side);

                    IsFlipping = false;
                    queued.Done?.Invoke();
                    StartNextFlip();
                });
        }

        /// <summary>清空未播完的翻面队列（战斗结束 / 界面关闭时用），与 CardView 同名同义。</summary>
        public void ClearFlipQueue()
        {
            m_FlipQueue.Clear();
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle < 0f)
            {
                angle += 360f;
            }

            return angle;
        }

        private void OnDestroy()
        {
            m_FlipTween?.Kill();
            m_FlipQueue.Clear();
        }
    }
}
