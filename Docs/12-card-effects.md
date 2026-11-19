# 卡牌配置与效果系统

这份文档说明两件事：**你改表**、**我加功能**，以及两者之间的边界在哪。

## 1. 表在哪

| 表 | 路径 | 说明 |
| --- | --- | --- |
| 卡牌 | `DataTables/Cards.xlsx` | 每行一张卡，正反两面各一组列 |
| 敌人 | `DataTables/Enemies.xlsx` | 每行一个敌人，两面即两形态 |

用 Excel 直接打开即可，**不需要另存 CSV**（早期版本用的是 CSV，已废弃）。
表里有第二个工作表「填写说明」，列含义和效果写法都写在那里。

### Cards 表的列

| 列 | 含义 |
| --- | --- |
| `id` | 唯一编号，不要重复，也不要随便改（资产按 id 命名，改了会重新生成） |
| `frontName` / `backName` | 两面卡名 |
| `frontCost` / `backCost` | 费用 |
| `frontType` / `backType` | `Attack` / `Skill` / `Power` |
| `frontEffects` / `backEffects` | **效果串**，见下节；留空则回退到 `Damage` / `Armor` 两列 |
| `frontDamage` / `backDamage` | 伤害（效果列为空时生效） |
| `frontArmor` / `backArmor` | 护甲（效果列为空时生效） |
| `frontDescription` / `backDescription` | 卡面文字，**需要你自己跟着数值改**，程序不会自动同步 |

## 2. 改完表怎么生效

Unity 菜单：

1. `Tools/TurnCard/数据表/生成 SO` —— 重新生成所有资产
2. `Tools/TurnCard/数据表/检查一致性` —— 检查「表改了但没重新生成」

产物位置（不要手改，会被覆盖）：

- `Assets/Game/Data/Generated/Cards/<id>.asset`
- `Assets/Game/Data/Generated/Enemies/<id>.asset`
- `Assets/Resources/BattleContent.asset`（运行时只读这一个索引）

## 3. 效果串怎么写

多个效果用 `;` 分隔，参数用 `:`，多参数用 `,`：

```
damage:6            造成 6 点伤害
damage:9,all        对所有敌人造成 9 点伤害
damage:3;damage:3   对同一目标打两段 3（连射）
armor:5             获得 5 点护甲
draw:2              抽 2 张牌
energy:1            获得 1 点能量
heal:6              回复 6 点生命
losehp:3            失去 3 点生命（可致死）
special:key         调用特殊卡脚本，见第 5 节
```

按顺序依次执行，所以 `losehp:3;damage:12` 与 `damage:12;losehp:3` 的结果可能不同（自伤致死会中断）。

## 4. 加一种通用效果

改两个地方，**不用碰卡牌数据**：

1. 在 `Assets/Game/Scripts/Battle/CardEffects.cs` 里新增一个 `ICardEffect` 实现；
2. 在 `Builtins` 字典里注册一个 key。

例如加「本回合每打一张攻击牌抽 1 张」，需要先有状态支撑，再注册 key。

## 5. 加一张特殊卡

特殊卡是**代码 + 表格**各写一半：

1. 新建类，实现 `ICardEffect`，打上特性：

```csharp
[SpecialCard("blood_pact")]
public sealed class BloodPact : ICardEffect
{
    public void Apply(CardEffectContext context, string[] args)
    {
        context.State.LosePlayerHp(3);
        context.State.DamageEnemy(context.Target, 12);
    }
}
```

2. 表里该面的效果列填 `special:blood_pact`。

`CardEffects` 会在第一次用到时反射扫描所有带 `[SpecialCard]` 的类，**不需要注册、不需要改分发代码**。
参数可以直接从表里传：`special:blood_pact,4,15` → `args = ["4","15"]`。

特殊卡可以调用的状态操作（`BattleState` 上的公开方法）：

| 方法 | 作用 |
| --- | --- |
| `DamageEnemy(enemy, amount)` | 造成伤害，返回是否因此翻面 |
| `GainArmor(amount)` | 加护甲 |
| `DrawCards(count)` | 抽牌 |
| `HealPlayer(amount)` | 回血（不超上限） |
| `LosePlayerHp(amount)` | 自伤 |
| `GainEnergy(amount)` | 加能量 |

## 6. 规则前提

- 卡牌使用后**只翻面，留在手上**（Q1、Q8）；同一张牌一回合内可以反复使用，只受能量限制
- 手牌在**回合结束时**才全部进弃牌堆（Q2）
- 抽牌堆空了会把弃牌堆洗回来

## 7. 已知限制

- 卡面描述是**手写文本**，改了数值要自己改描述
- 敌人目前只有「每回合造成攻击力点伤害」一种行为，没有敌人技能表
