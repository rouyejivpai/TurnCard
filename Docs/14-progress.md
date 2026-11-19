# 开发进度总览

> 更新于本轮：起始牌组已进表、首领结算与地图查看牌组已接。

## 一、已完成

### 数据层

| 项 | 状态 | 位置 |
| --- | --- | --- |
| Excel 数据表（Cards / Enemies / Keywords） | ✅ | `DataTables/*.xlsx` |
| xlsx 直接导入（无需转 CSV） | ✅ | `Editor/XlsxReader.cs` |
| 表 → ScriptableObject 生成 + 一致性检查 | ✅ | `Editor/DataTableImporter.cs` |
| 文本与效果串校验菜单 | ✅ | `Editor/CardTextValidator.cs` |
| 起始牌组配置（`starter` 列） | ✅ | `Cards.xlsx` |
| 双面卡数据模型 | ✅ | `Data/CardDefinition.cs` |
| 效果串（damage / armor / draw / energy / heal / losehp / special） | ✅ | `Battle/CardEffects.cs` |
| 特殊卡扩展（`[SpecialCard]`） | ✅ | `Battle/BloodPact.cs` |
| 机制 tag（瞬发 / 保留 / 光环） | ✅ | `Battle/KeywordMechanics.cs` |
| 光环扩展（`[AuraKeyword]`） | ✅ | `Battle/Auras/DoubleInstantAura.cs` |

### 战斗

| 项 | 状态 |
| --- | --- |
| 出牌 / 能量 / 护甲 / 抽牌 / 弃牌 | ✅ |
| 卡牌使用后**只翻面、留在手上** | ✅ |
| 敌人双形态（血归零翻面、满血） | ✅ |
| 敌方回合结算、胜负判定 | ✅ |
| 目标判定（`damage:N,random`、`all` 不需选目标） | ✅ |
| 抽牌堆 / 弃牌堆查看（遮罩 + 滚动列表） | ✅ |

### 表现

| 项 | 状态 |
| --- | --- |
| 卡牌预制体（双面、真实 3D 翻面） | ✅ |
| TMP 中文字体（动态图集） | ✅ |
| DOTween 动画（发牌 / 悬停 / 出牌 / 抖动 / 飘字 / 翻面） | ✅ |
| 关键词悬停提示（按加粗文本检索、递归展开） | ✅ |

### 流程

| 项 | 状态 |
| --- | --- |
| Procedure：Boot / 主菜单 / 地图 / 战斗 / 奖励 / 营地 / 商店 / 结算 | ✅ |
| 单场景（`Boot.unity`） | ✅ |
| 事件总线（泛型门面 + 框架 IEventManager） | ✅ |
| 一局状态 `RunState`（生命 / 金币 / 牌组 / 节点） | ✅ |
| 奖励三选一（卡牌式界面） | ✅ |
| 商店（3 张卡 + 删牌服务） | ✅ |
| 营地（回血 / 删牌） | ✅ |
| 地图查看牌组 | ✅ |
| 首领 → 通关结算 | ✅ 代码已写，**未实测** |
| 战斗失败 → 失败结算 | ✅ 代码已写，**未实测** |

## 二、未完成 / 待验证

| 项 | 说明 |
| --- | --- |
| 首领 & 失败链路实测 | 两条分支都写了，本轮只验证了普通战斗 → 奖励 |
| 地图分支路径 | 当前线性 6 节点，已确认先保持 |
| 界面全部是代码搭建 | 奖励 / 商店 / 营地 / 列表都不是预制体，等美术定稿再转 |
| 敌人技能 | 敌人目前只会「每回合打一下」，没有技能表 |
| 卡牌描述与数值同步 | 描述是手写文本，改数值要自己改文案 |

## 三、已知技术债

1. **UI 全是代码搭建**：`MenuScreen` / `CardGridScreen` / `CardListViewScreen` 在运行时拼 UI。结构稳定后应转成预制体 + UIBind 绑定。
2. **卡面版式未定稿**：名称偏右、美术图是占位方块。
3. **事件总线只用了两处**（战斗结束、节点选择），大部分流程仍直接调用。
4. **地图没有存档**：关掉游戏进度就没了。
