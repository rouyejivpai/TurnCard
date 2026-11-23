using UnityEngine;

namespace TurnCard.Game.Data
{
    /// <summary>运行时读取的敌人一个形态的数据。</summary>
    /// <summary>翻面后血量怎么算。</summary>
    public enum EnemyFlipHpMode
    {
        /// <summary>背面用自己的满血。</summary>
        Own,

        /// <summary>继承翻面时的当前血量。</summary>
        Inherit,

        /// <summary>按背面满血的固定比例。</summary>
        Ratio,
    }

    /// <summary>意图抽取方式。</summary>
    public enum EnemyIntentMode
    {
        /// <summary>按表里顺序循环，可预测。</summary>
        Sequence,

        /// <summary>洗牌随机，一轮内不重复。</summary>
        Shuffle,
    }

    public readonly struct EnemyForm
    {
        public EnemyForm(string displayName, int maxHp, int attack, string description,
            Sprite art = null)
        {
            DisplayName = displayName;
            MaxHp = maxHp;
            Attack = attack;
            Description = description;
            Art = art;
        }

        public string DisplayName { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public string Description { get; }

        /// <summary>
        /// 这一形态的立绘（48×64 网格的像素画，仓库里是 96×128 的 2× 导出）。
        /// 为空时由视图画占位色块，不允许静默留白。
        /// </summary>
        public Sprite Art { get; }
    }

    /// <summary>
    /// 敌人定义。敌人是「角色卡」：带攻击力与生命的卡牌（Q14），
    /// 正面与背面即两种形态，血量与攻击力各自独立配置（Q18、Q21）。
    /// 由 DataTables/Enemies.csv 生成。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Enemy Definition", fileName = "Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private int m_Id;

        [Tooltip("所属池子：normal / elite / boss")]
        [SerializeField] private EnemyPool m_Pool = EnemyPool.Normal;

        [Tooltip("翻面时血量：own=背面满血 inherit=继承当前 ratio=按比例")]
        [SerializeField] private EnemyFlipHpMode m_FlipHpMode = EnemyFlipHpMode.Own;

        [Tooltip("ratio 模式下的继承比例，0~1")]
        [SerializeField] private float m_FlipHpRatio = 1f;

        [Tooltip("意图顺序：sequence=按表顺序循环 shuffle=随机不重复")]
        [SerializeField] private EnemyIntentMode m_IntentMode = EnemyIntentMode.Shuffle;

        [Tooltip("正面初始 buff，写法 buffId:层数:回合，多条用分号")]
        [SerializeField] private string m_FrontBuffs = string.Empty;

        [Tooltip("反面初始 buff，写法同上")]
        [SerializeField] private string m_BackBuffs = string.Empty;

        [Tooltip("正面起始护甲")]
        [SerializeField] private int m_FrontArmor;

        [Tooltip("反面起始护甲")]
        [SerializeField] private int m_BackArmor;

        [SerializeField] private string m_FrontName = string.Empty;
        [SerializeField] private int m_FrontHp;
        [SerializeField] private int m_FrontAttack;
        [TextArea] [SerializeField] private string m_FrontDescription = string.Empty;

        [Tooltip("正面立绘（48×64 网格；空值走占位）")]
        [SerializeField] private Sprite m_FrontArt;

        [Tooltip("双生：正反面共用一份血量与 buff（翻面时不清 buff、不加载反面初始 buff）")]
        [SerializeField] private int m_Twin;

        [Tooltip("羁绊配对：同 pairId 的敌人互为羁绊对象（一方死亡时翻面另一方）")]
        [SerializeField] private int m_PairId;

        [Tooltip("备注（占位说明、待补数值等），不参与逻辑")]
        [TextArea] [SerializeField] private string m_Note = string.Empty;

        [Tooltip("1 = 预留/占位，不参与随机抽取（编队表仍可手动引用）")]
        [SerializeField] private int m_Disabled;

        [SerializeField] private string m_BackName = string.Empty;
        [SerializeField] private int m_BackHp;
        [SerializeField] private int m_BackAttack;
        [TextArea] [SerializeField] private string m_BackDescription = string.Empty;

        [Tooltip("背面立绘（同上）")]
        [SerializeField] private Sprite m_BackArt;

        public int Id => m_Id;

        /// <summary>所属池子。</summary>
        public EnemyPool Pool => m_Pool;

        public EnemyFlipHpMode FlipHpMode => m_FlipHpMode;
        public float FlipHpRatio => m_FlipHpRatio;
        public EnemyIntentMode IntentMode => m_IntentMode;
        public string FrontBuffs => m_FrontBuffs;
        public string BackBuffs => m_BackBuffs;
        public int FrontArmor => m_FrontArmor;
        public int BackArmor => m_BackArmor;

        /// <summary>双生：正反面共用血量与 buff。表里 twin=1。</summary>
        public bool Twin => m_Twin != 0;

        /// <summary>羁绊配对 id，0 表示没有配对。</summary>
        public int PairId => m_PairId;

        public string Note => m_Note;

        /// <summary>占位怪：不参与随机抽取，避免 HP=1 的半成品进战斗。</summary>
        public bool Disabled => m_Disabled != 0;

        public EnemyForm Front => new EnemyForm(m_FrontName, m_FrontHp, m_FrontAttack,
            m_FrontDescription, m_FrontArt);

        public EnemyForm Back => new EnemyForm(m_BackName, m_BackHp, m_BackAttack,
            m_BackDescription, m_BackArt);

        public EnemyForm GetForm(CardFaceSide side)
        {
            return side == CardFaceSide.Front ? Front : Back;
        }
    }
}
