using System;
using TMPro;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 敌人的视图。敌人是「角色卡」，因此卡面直接复用 <see cref="CardView"/> 的预制体，
    /// 只在上面叠加意图、左下攻击力、右下血量这几项敌人特有的信息。
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private static readonly Color FrontTint = new Color(0.30f, 0.22f, 0.26f);
        private static readonly Color BackTint = new Color(0.42f, 0.16f, 0.18f);
        private static readonly Color PillColor = new Color(0f, 0f, 0f, 0.55f);

        private RectTransform m_Rect;
        private CardView m_Face;
        private TextMeshProUGUI m_IntentText;
        private Image m_IntentIcon;
        private Image m_HpFill;
        private TextMeshProUGUI m_HpText;
        private TextMeshProUGUI m_ArmorText;
        private Coroutine m_FlipRoutine;
        private float m_BaseScale = 1f;

        public EnemyInstance Enemy { get; private set; }
        public RectTransform Rect => m_Rect;

        public static EnemyView Create(Transform parent, EnemyInstance enemy, float scale,
            Vector2 anchor, Vector2 position)
        {
            var go = new GameObject("Enemy_" + enemy.Definition.Id, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(CardView.CardWidth, CardView.CardHeight);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * scale;

            var view = go.AddComponent<EnemyView>();
            view.Build(rect, enemy, scale);
            return view;
        }

        private void Build(RectTransform rect, EnemyInstance enemy, float scale)
        {
            m_Rect = rect;
            m_BaseScale = scale;
            Enemy = enemy;

            // 卡面：直接复用卡牌预制体，保持与手牌同一套视觉
            m_Face = CardView.CreateDisplay(rect, 1f);

            float halfHeight = CardView.CardHeight * 0.5f;
            float halfWidth = CardView.CardWidth * 0.5f;

            // ---- 头顶：意图（图标 + 数值）----
            RectTransform intentRow = UiFactory.CreateRect(rect, "IntentRow");
            intentRow.anchorMin = new Vector2(0.5f, 0.5f);
            intentRow.anchorMax = new Vector2(0.5f, 0.5f);
            intentRow.pivot = new Vector2(0.5f, 0.5f);
            intentRow.sizeDelta = new Vector2(180f, 40f);
            intentRow.anchoredPosition = new Vector2(0f, halfHeight + 34f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(intentRow, false);
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(32f, 32f);
            iconRect.anchoredPosition = new Vector2(24f, 0f);
            m_IntentIcon = iconGo.GetComponent<Image>();
            m_IntentIcon.raycastTarget = false;
            m_IntentIcon.enabled = false;

            m_IntentText = UiFactory.CreateLabel(intentRow, "Intent", string.Empty, 26,
                new Color(1f, 0.88f, 0.55f), new Vector2(120f, 36f),
                new Vector2(0f, 0.5f), new Vector2(62f, 0f));
            m_IntentText.raycastTarget = false;

            // ---- 下方：血条 ----
            RectTransform barRoot = UiFactory.CreateRect(rect, "HpBar");
            barRoot.anchorMin = new Vector2(0.5f, 0.5f);
            barRoot.anchorMax = new Vector2(0.5f, 0.5f);
            barRoot.pivot = new Vector2(0.5f, 1f);
            barRoot.sizeDelta = new Vector2(152f, 20f);
            barRoot.anchoredPosition = new Vector2(0f, -halfHeight - 14f);

            UiFactory.CreateBox(barRoot, "Back", new Color(0.06f, 0.06f, 0.08f, 0.92f),
                new Vector2(152f, 20f), new Vector2(0.5f, 0.5f), Vector2.zero);

            RectTransform fill = UiFactory.CreateRect(barRoot, "Fill");
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(2f, -2f);
            m_HpFill = fill.gameObject.AddComponent<Image>();
            m_HpFill.color = new Color(0.45f, 0.85f, 0.42f);
            m_HpFill.raycastTarget = false;

            m_HpText = UiFactory.CreateLabel(barRoot, "HpText", string.Empty, 14,
                Color.white, new Vector2(152f, 18f), new Vector2(0.5f, 0.5f), Vector2.zero);
            m_HpText.raycastTarget = false;

            // 敌人身上的 buff 图标条（血条下方）
            BuffIconStrip.Attach(rect, enemy.Buffs,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, -halfHeight - 44f));

            m_ArmorText = UiFactory.CreateLabel(rect, "Armor", string.Empty, 18,
                new Color(0.62f, 0.80f, 1f), new Vector2(120f, 24f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -halfHeight - 40f));
            m_ArmorText.raycastTarget = false;

            RefreshVisual();
        }

        private static TextMeshProUGUI CreatePill(RectTransform parent, string name, Vector2 position,
            Vector2 size, int fontSize, Color color)
        {
            RectTransform box = UiFactory.CreateBox(parent, name + "Pill", PillColor, size,
                new Vector2(0.5f, 0.5f), position);
            TextMeshProUGUI label = UiFactory.CreateLabel(box, name, string.Empty, fontSize, color,
                size, new Vector2(0.5f, 0.5f), Vector2.zero);
            label.raycastTarget = false;
            return label;
        }

        public void RefreshVisual()
        {
            EnemyForm front = Enemy.Definition.Front;
            EnemyForm back = Enemy.Definition.Back;
            bool isBack = Enemy.Side == CardFaceSide.Back;

            // 敌人的两种形态各有一套卡面，翻面时由 CardView 真实旋转展示背面
            // 传入 CardFace 让标记（[[关键词]] / *风味*）也能在敌人卡上生效
            CardFace frontFace = new CardFace(front.DisplayName, -1, CardType.Skill, front.Attack, 0,
                front.Description, string.Empty, null, null);
            CardFace backFace = new CardFace(back.DisplayName, -1, CardType.Skill, back.Attack, 0,
                back.Description, string.Empty, null, null);

            m_Face.ShowPair(front.DisplayName, front.Description, -1, null, FrontTint,
                back.DisplayName, back.Description, -1, null, BackTint, frontFace, backFace);
            m_Face.ApplySide(Enemy.Side, false, 0f, null, null);

            EnemyForm form = Enemy.Form;
            RefreshIntent();
            RefreshHp(form);
        }

        /// <summary>头顶意图：图标 + 数值，多段攻击显示 6×2。</summary>
        private void RefreshIntent()
        {
            EnemyIntentDefinition intent = Enemy.CurrentIntent;
            if (intent == null)
            {
                m_IntentIcon.enabled = false;
                m_IntentText.text = string.Empty;
                return;
            }

            m_IntentIcon.enabled = true;
            m_IntentIcon.sprite = IntentSprite(intent.Intent);
            m_IntentIcon.preserveAspect = true;

            switch (intent.Intent)
            {
                case EnemyIntentType.Attack:
                    m_IntentText.text = intent.Hits > 1
                        ? $"{intent.Value}\u00d7{intent.Hits}"
                        : intent.Value.ToString();
                    m_IntentText.color = new Color(1f, 0.72f, 0.62f);
                    break;
                case EnemyIntentType.Defend:
                    m_IntentText.text = intent.Value.ToString();
                    m_IntentText.color = new Color(0.66f, 0.84f, 1f);
                    break;
                case EnemyIntentType.Buff:
                case EnemyIntentType.ApplyBuff:
                    m_IntentText.text = (intent.Intent == EnemyIntentType.ApplyBuff &&
                                         intent.BuffTarget == BuffTarget.Player ? "-" : "+") + intent.Value;
                    m_IntentText.color = intent.Intent == EnemyIntentType.ApplyBuff &&
                                         intent.BuffTarget == BuffTarget.Player
                        ? new Color(1f, 0.72f, 0.86f)
                        : new Color(0.75f, 1f, 0.7f);
                    m_IntentIcon.sprite = intent.Intent == EnemyIntentType.ApplyBuff &&
                                          intent.BuffTarget == BuffTarget.Player
                        ? IntentSprite(EnemyIntentType.Debuff)
                        : IntentSprite(EnemyIntentType.Buff);
                    break;
                default:
                    m_IntentText.text = "-" + intent.Value;
                    m_IntentText.color = new Color(1f, 0.72f, 0.86f);
                    break;
            }
        }

        private static Sprite IntentSprite(EnemyIntentType type)
        {
            if (s_IntentSprites == null)
            {
                s_IntentSprites = new Sprite[4];
            }

            int index = (int)type;
            if (index < 0 || index >= s_IntentSprites.Length)
            {
                return null;
            }

            if (s_IntentSprites[index] == null)
            {
                string name = type == EnemyIntentType.Attack ? "intent_attack"
                    : type == EnemyIntentType.Defend ? "intent_defend"
                    : type == EnemyIntentType.Buff ? "intent_buff"
                    : "intent_debuff";
                var tex = Resources.Load<Texture2D>("Art/UI/enemy/" + name);
                if (tex != null)
                {
                    s_IntentSprites[index] = Sprite.Create(tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), 100f);
                }
            }

            return s_IntentSprites[index];
        }

        /// <summary>下方血条：长度按当前血量占比，旁边显示护甲。</summary>
        private void RefreshHp(EnemyForm form)
        {
            float ratio = form.MaxHp <= 0 ? 0f : Mathf.Clamp01(Enemy.Hp / (float)form.MaxHp);
            m_HpFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            m_HpText.text = $"{Enemy.Hp}/{form.MaxHp}";
            m_ArmorText.text = Enemy.Armor > 0 ? $"甲 {Enemy.Armor}" : string.Empty;
        }

        private static Sprite[] s_IntentSprites;

        /// <summary>指针悬停时的轻微放大，相对自身基准缩放。</summary>
        public void SetHighlight(bool highlighted)
        {
            m_Rect.localScale = Vector3.one * (m_BaseScale * (highlighted ? 1.04f : 1f));
        }

        /// <summary>翻到另一形态：交给卡面自己做真实翻面动画，同时刷新属性数值。</summary>
        public void PlayFlip(float duration, AudioSource audio, Action onCompleted = null)
        {
            // 形态数据在翻面那一刻就已经变了，属性立刻更新，动画只是表现
            RefreshVisual();

            m_Face.ApplySide(Enemy.Side, true, duration, audio, () =>
            {
                RefreshVisual();
                onCompleted?.Invoke();
            });
        }
    }
}
