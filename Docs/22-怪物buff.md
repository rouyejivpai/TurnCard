# 22 怪物特性与 Buff 目录

状态：**待确认**（本文只定 buff 语义，确认后才动 `DataTables/*.xlsx`）
依据：`Docs/怪物参考设定/TurnCard怪物表格.xlsx`（54 行 / 23 个怪物）

## 0. 三条已定原则

1. **所有怪物特性由 buff 实现**，怪物表只放「初始 buff + 意图」，不写特判代码。
2. **双生**：正反面**共用一份 HP 与 buff**。落地方式是
   `Enemies.twin = 1` + `flipHpMode = inherit` + 两面 HP 写同一个值，
   翻面时**跳过** `Buffs.Clear()`（现状是无条件清空，见 `BattleState.OnEnemyFlipped`）。
3. 配置表全量建 23 个怪物，参考表里「—」的数值填占位并在 `note` 列标注「待补」。

## 1. 现有可复用的 buff（13 个）

| id | 名称 | 机制 | 用在谁身上 |
| --- | --- | --- | --- |
| 1 | 易伤 | `damage_taken:+3` | 通用 |
| 2 | 力量 | `attack:+1` | 通用 |
| 3 | 虚弱 | `damage_dealt:*0.75` | 通用 |
| 4 | 荆棘 | 受击后反伤 2 | 啸山庄 |
| 5 | 壁垒 | 拦 `armor_clear` | 画眉山庄 |
| 6 | 晕眩 | 跳过下一次行动 | 通用 |
| 7 | 受击翻面 | 累计 3 次伤害后翻面 | 哥布林 |
| 8 | 破甲翻面 | 护甲清空时翻面 | 画眉山庄 |
| 9 | 空挥翻面 | 本回合未造成伤害则翻面 | 呼啸山庄 |
| 10 | 吸收 | 本回合伤害归零 | 一棵树 |
| 11 | 激怒 | 每次受伤 +1 力量 | 惊醒的哥布林 |
| 12 | 受击惊醒 | 受伤即翻面 | 嗜睡（备用） |
| 13 | 久睡翻面 | 经过 2 回合翻面 | 嗜睡 |

## 2. 需要新增的 buff

字段含义同 `Buffs.xlsx` 列：`stackRule / consume / maxStacks / modifier / trigger / triggerAction / triggerCount / triggerEffect / ruleBlock`。

### 2.1 现有机制就能表达（只加数据行）

| id | name | displayName | trigger | action | count | triggerEffect | consume | 说明 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 14 | FatedMoon | 满月（回合） | turnStart | flipSelf | 2 | — | permanent | 满月的「2 回合」分支 |
| 15 | FatedMoonHit | 满月（受击） | damaged | flipSelf | 2 | — | permanent | 满月的「2 次伤害」分支 |
| 16 | OpeningArmor | 开幕·护甲 | battleStart | effect | 0 | `armor:N` | permanent | 开幕加护甲 |
| 17 | OpeningWeak | 开幕·虚弱 | battleStart | effect | 0 | `buff:3,1,4,player` | permanent | 给玩家 4 回合虚弱 |
| 18 | OpeningBuffs | 开幕·己方 | battleStart | effect | 0 | `buff_self:<力量/荆棘>` | permanent | 力量 + 荆棘 |
| 19 | Sleep2 | 久睡 | turnStart | flipSelf | 2 | — | permanent | 与现有 13 合并即可，此行为占位说明 |

> 16~18 只是同一触发时机的不同效果，也可以合并成「一个开幕 buff + 分号串多个效果」。

### 2.2 需要扩 buff 系统才成立

| id | name | displayName | 缺什么 | 建议实现 | 对应怪物 |
| --- | --- | --- | --- | --- | --- |
| 19 | DealtUnblockedRage | 疯狂 | 新触发 `dealtUnblocked` | 造成未格挡伤害后：`losehp:2` + `buff_self:2,1,-1` | 失控的舞者 |
| 20 | DealtUnblockedLeech | 嗜血·兽 | 新触发 + 新修饰器 `next_attack_hits` | `heal:2`，并让下一次攻击段数 +1（待定是否消耗） | 失控的剧团之兽 |
| 21 | Bond | 羁绊 | 新触发 `selfDeath` + 跨单位动作 | 死亡时让**配对的另一个单位**翻面 | 舞者 / 剧团之兽 |
| 22 | Inspiration | 灵感 | 新触发 `allyDeath` | 每有一个友方死亡 +1 层；满 N 层翻面 | 苦思的编辑 |
| 23 | Greed | 贪婪 | 玩家侧 `card_cost` 修饰 + 「每回合限一次」 | 每回合第 1 张牌费用归零 | 浮士德 |
| 24 | Price | 代价 | 记录「本局免费打出过的牌」 | 那些牌再次打出时：敌人 `heal:3`，玩家受 3 伤 | 梅菲斯特 |
| 25 | AbsorbToWeak | 吸收反噬 | 记录本回合吸收量，下回合转成临时力量损失 | `attack:-吸收量`（1 回合） | 一棵树 |
| 26 | Summon / Sacrifice / Escape | 召唤 / 献祭 / 逃跑 | 动态增删敌人、脱离战斗 | 独立系统（建议放到这批之后） | 三个概念 / 编辑 / 反派 |

## 3. 现有 7 个怪物的特性映射（本批先做这些）

| 怪物 | 参考表特性 | 落到哪个 buff | 现状 |
| --- | --- | --- | --- |
| 哥布林 / 火焰精灵 | 累计受 3 次伤害后翻面 | 7 受击翻面 | ✅ 已有 |
| 嗜睡哥布林 / 惊醒 | 双生 + 睡眠（受伤或 2 回合翻面，继承 HP） | twin 标记 + 13 久睡 + 12 受击惊醒 + `flipHpMode=inherit` | ⚠️ 需 twin 开关（现在翻面会清 buff） |
| 惊醒的哥布林 | 每次受伤 +1 力量 | 11 激怒 | ✅ 已有 |
| 亚哈 / 白鲸 | 白鲸开幕：给玩家 2 回合虚弱 | 17 开幕·虚弱 | ❌ 需 `battleStart` 触发 |
| 持剑贤者 / 一棵树 | 吸收伤害，一回合后按吸收量临时失去力量 | 10 吸收 + 25 吸收反噬 | ⚠️ 25 需新机制 |
| 舞台布景 / 堡垒巨人 | 极少伤害 / 双方各 +1 力量 / 自身超高层易伤 / 意图执行完翻面 | 意图即可表达（1、2 号 buff + flipSelf 意图） | ✅ 已有 |
| 浮士德 / 梅菲斯特 | 贪婪 / 代价 | 23 贪婪 + 24 代价 | ❌ 两个都要新机制 |
| 画眉山庄 / 呼啸山庄 | 开幕壁垒+护甲+自身虚弱 4；护甲消失→失血并翻面 | 5 壁垒 + 16 开幕护甲 + 3 虚弱 + 8 破甲翻面 | ⚠️ 需 `battleStart` + 「翻面时失血」 |
| 呼啸山庄 | 开幕力量+荆棘、易伤 4；未造成伤害→下回合晕眩并翻面 | 18 开幕 + 4 荆棘 + 1 易伤 + 9 空挥翻面（+6 晕眩） | ⚠️ 「晕眩并翻面」需要一次触发做两件事 |

## 4. 已确认的 buff 语义

| buff | 结论 |
| --- | --- |
| 自愈（狼人） | **层数 = 回血量**，执行后清零（2 层 = 回 2 点） |
| 嗜血·兽 | **累积**，下一次攻击把存量段数全部用掉 |
| 羁绊 | 只翻面**配对的另一个单位**，不连锁 |
| 灵感 | **3 层**触发翻面 |

另：双生的血量必须两面相同；参考表里「农夫 25 / 狼人 36」视为待改（或不算双生）。

### 4.0 层数衰减（数据驱动）

buff 的"掉层"不写在回合流程里，而是**由 buff 自己监听触发时机**：

| 表列 | 含义 |
| --- | --- |
| `decayTrigger` | 在哪个时机掉层（`none` / `turnStart` / `turnEnd` / …，取 `BuffTrigger` 枚举） |
| `decayStacks` | 每次掉几层（默认 1） |

实现：`BattleState.RunTriggers(container, trigger, …)` 跑完该时机的触发效果后，
统一调用 `container.DecayByTrigger(trigger)`，把 `decayTrigger == trigger` 的 buff 减层、减到 0 移除。

所以：

- 虚弱 / 易伤 / 晕眩：`decayTrigger = turnStart`、`decayStacks = 1` → 每回合开始掉 1 层；
- 力量 / 壁垒这类永久 buff：`decayTrigger = none` → 永不掉层；
- 以后要"回合结束掉层"或"受击后掉层"，**只改表**，不用碰回合流程代码。

> 层数即持续时间：`buff:3,2,2`（虚弱 2 层）＝ 持续 2 回合。

**刚挂上的那一层先豁免一次**：`BuffInstance.Fresh` 在新建/叠加时置 true，
`DecayByTrigger` 遇到它只清标记、不掉层。否则「回合开始掉层」会把本回合刚施加的
虚弱/易伤（尤其是开幕效果）立刻抹掉。

### 4.0.1 开幕效果（原「初始 buff」）

`Enemies.xlsx` 的 `frontBuffs / backBuffs` 里，`trigger = battleStart` 的 buff 现在按
**一次性开幕效果**执行，不再作为常驻 buff 挂到敌人身上：

| | 之前 | 现在 |
| --- | --- | --- |
| 存储 | 常驻 BuffInstance | 不存，执行完即弃 |
| 双生共享 | 会跟着翻面被共享 ❌ | 不共享 ✅ |
| buff 栏显示 | 占一个图标 | 不显示（另加 `hidden` 列兜底） |
| 翻转时 | 随容器迁移 | 新形态的开幕效果**重新执行**一次 |

实现：`BattleState.ApplyFormBuffs` 解析到 `battleStart` 的 buff 就调 `ExecuteBuffTrigger()`
执行它的「触发动作 + 触发效果」，其余（受击翻面、羁绊、疯狂…）仍作为常驻 buff 挂上。



### 4.1 贪婪 / 代价（浮士德 ↔ 梅菲斯特）

| 项 | 结论 |
| --- | --- |
| 实现形式 | **buff 驱动**：贪婪（27）在玩家回合开始时给玩家挂「费用归零」（31），不是代码硬判 |
| 免费张数 | = 贪婪层数（27 的 `stackRule = stack`，可叠到 5） |
| 计数器 | `BattleState.m_PlaysThisTurn`，**每回合清零**；出牌后自增，`>= 贪婪层数` 就摘掉费用归零 buff |
| 卡牌标记 | 出牌时检查**实际支付的费用**，为 0 就给这张 `CardInstance.FreePlayed` 打标记（翻面/再打出都带着） |
| 代价（28） | 带着标记的牌**再次打出**时：梅菲斯特回 3 血、玩家受 3 伤（先扣护甲）；标记按卡牌实例存，**单场战斗**内有效 |
| 费用计算 | `EffectiveCost(face) = max(0, 面值 + 玩家 cost 修饰之和)`，所以「加成/减费」类 buff 也能接进来 |
| 翻面血量 | 浮士德 `flipHpMode = own` → **反面满血**（28 → 66/66） |

## 5. 本轮落地情况

**已实现**

| 项 | 位置 |
| --- | --- |
| `twin` 列（双生：翻面不清 buff、不加载反面初始 buff） | `Enemies.xlsx` + `EnemyDefinition.Twin` + `BattleState.OnEnemyFlipped` |
| `battleStart` 触发时机 | `BuffTrigger.BattleStart`，在 `Setup` 与「翻面换形态」两处各跑一次 |
| 开幕 buff 14~19 | `Buffs.xlsx`（虚弱/壁垒/自虚弱/力量/荆棘/自易伤，全部可组合） |
| 现有 7 个怪物的特性映射 | `Enemies.xlsx` 的 `frontBuffs`/`backBuffs` + 新增 `note` 列 |

实测（直接构造 `BattleState`）：

| 场景 | 结果 |
| --- | --- |
| 画眉山庄开局 | 护甲 10、壁垒 ✓、自虚弱 ✓、破甲翻面 ✓、不误伤玩家 ✓ |
| 画眉→呼啸 翻面 | 力量/荆棘/自易伤/空挥翻面 ✓，旧壁垒被清 ✓ |
| 亚哈→白鲸 翻面 | 玩家获得虚弱 2 回合 ✓ |
| 嗜睡 翻面（双生） | 久睡/受击惊醒**保留** ✓，反面初始的激怒**不加载** ✓，HP 24/24 继承 ✓ |

**第二轮（机制 + 新怪物）**

| 项 | 位置 |
| --- | --- |
| 新触发：`DealtUnblocked` / `SelfDeath` / `AllyDeath` | `BuffTrigger` + `BattleState` 钩子 |
| 新动作：`StunAndFlip`（晕眩并翻面） | `BuffTriggerAction` |
| 新效果：`self_damage` / `self_heal` / `next_hits` / `flip_pair` | `CardEffects` 内置表 |
| 嗜血的「下次攻击 +1 段」 | `EnemyInstance.BonusHits`，攻击时消耗 |
| 羁绊配对 | `Enemies.pairId` + `BattleState.FlipPairedAllies` |
| 敌人死亡统一入口 | `BattleState.HandleEnemyDeath`（自死/友死/玩家死亡触发） |
| buff 20~24 | 疯狂 / 嗜血·兽 / 羁绊 / 灵感 / 空挥晕眩翻面 |
| 新怪物 8 剧团舞者、9 剧团之兽 | 双生 + 羁绊 + pairId=1 + 兽的 3 条意图 |
| 占位怪物 14 个 + 农夫/狼人 | 参考表「—」的条目，HP=1、`note=占位` |

实测：

| 场景 | 结果 |
| --- | --- |
| 舞者死亡（双生 + 羁绊） | 兽从 Front 翻到 Back ✓，pairId 生效 ✓ |
| 兽 造成未格挡伤害 | 自身回 2 血（26→28）✓，`next_hits` +1 留到下次攻击 ✓，冷却期不立即生效 ✓ |

**已建全 24 个敌人**（9 个有数据 + 14 个占位 + 农夫/狼人待确认），24 个 buff，38 条意图。

**下一轮**

1. **浮士德「贪婪 / 代价」**：需要玩家侧「卡牌费用」修饰 + 记录「本局免费打出过的牌」；
2. **贤者「吸收反噬」**：记录本回合吸收量，下回合转成临时力量损失；
3. **编辑的召唤爪牙 / 献祭**、**反派的逃跑**：动态增删敌人、脱离战斗，属独立系统；
4. **农夫/狼人的双生确认**（血量 25/36 → 需定为同一值）；
5. **意图校正**：亚哈「回复 12 生命」等需要新的敌人意图类型（治疗 / 给友方加 buff / 召唤）；
6. **编队表**：`Encounters.xlsx` 目前仍指向旧敌人（boss 池指向了 elite 的亚哈），要按新怪物重排。
