# 开发进度总览

> 更新于本轮：流程界面全部转预制体 + UIBind；敌人卡改成独立预制体（立绘铺满 + 意图气泡 + 血量横幅）。

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
| 敌人特性 = buff（19 个，含 6 个开幕 buff） | ✅ | `Buffs.xlsx` + `Battle/BuffContainer.cs` |
| 双生（正反面共用血量与 buff） | ✅ | `Enemies.twin` + `BattleState.OnEnemyFlipped` |

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
| 流程界面全部转预制体（主菜单/地图/奖励/商店/营地/结算/牌组与牌堆列表） | ✅ |
| UIBind 绑定脚本由生成器产出，绑定自检菜单 | ✅ |
| 敌人卡独立预制体（48×64 网格 ×4，立绘铺满 + 意图气泡 + 血量横幅） | ✅ 结构版，素材是占位图 |
| 双面卡统一接口 `ICardFlipView`（玩家卡 / 敌人卡各自实现） | ✅ |
| 敌人立绘走表（`Enemies.xlsx` 的 `frontArt` / `backArt` → Sprite） | ✅ 只填了 3 个敌人 |
| TMP 中文字体（SimHei 动态图集）+ 缺字检查菜单 | ✅ |
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
| 敌人卡美术 | 卡框 / 意图气泡 / 血量横幅三张是占位图（银框 + 红面板），等美术出 48×64 九宫格 |
| 敌人技能 | 敌人目前只会「每回合打一下」，没有技能表 |
| 卡牌描述与数值同步 | 描述是手写文本，改数值要自己改文案 |

## 三、已知技术债

1. **占位素材**：敌人卡的卡框/气泡/横幅用的是像素卡包里的通用图，样式与参考图差很远；
   换图只改预制体里的 Sprite，代码不用动。
2. **敌人立绘缺 4 个**：`Enemies.xlsx` 只填了「睡着的哥布林 / 亚哈 / 持剑贤者」，
   哥布林、舞台布景、浮士德、画眉山庄在 `Art/Cards` 里没有对应素材。
3. **意图图标缺 3 种**：`Stun` / `FlipSelf` / `ApplyBuff` 暂映射到防御/减益图标。
4. **敌人详情面板未做**：标准要求悬停/右键看名字与描述，目前只显示意图与血量。
5. **翻面动画有两份实现**：`CardView` 与 `EnemyCardView` 各一份，参数已强制对齐
   （`20-设计规则.md` 第 11 条），下一步抽 `CardFlipAnimator` 组件收口。
6. **`BuffIconStrip` 仍是运行时拼装**：敌人卡的 buff 条复用它，还在 `Attach` 里 `new GameObject`。
7. **`PilePanel` 是死代码**：牌堆列表已改用 `CardListViewScreen`，`PilePanel.cs` 与
   `Resources/UI/PilePanel.prefab` 没有调用方，可删。
8. **生成器的覆盖隐患**：`BattleFormPrefabBuilder` / `UiPrefabBuilder` 的部分入口仍是
   直接重建预制体，会覆盖手工调整（战斗界面预制体已手工加过 `EnemyArea/Slot1-3`）。
9. **卡面版式未定稿**：名称偏右、美术图是占位方块。
10. **卡面预制体的字体与运行时不一致**：`CardView/CardFace/EnemyCard` 里存的是 ArkPixel，
    运行时被 `EnsureFont` 覆盖成 SimHei → 编辑器预览不是游戏里的样子，
    改卡面排版要进 play 模式确认。（曾尝试全局改像素字，因大字号发块而撤回，见 `19`）
11. **事件总线只用了两处**（战斗结束、节点选择），大部分流程仍直接调用。
12. **地图没有存档**：关掉游戏进度就没了。
