# 11 GameFramework 接入

状态：**进行中**。Procedure 已接入并验证；UI 层待接入；数据层决定不接。

## 1. 接入范围（已确认）

| 层 | 决定 | 状态 |
| --- | --- | --- |
| Procedure 流程 | 接 | **已完成** |
| UI（UGuiForm） | 接，按官方形态补 UGUI 适配层 | 待做 |
| Resource 资源 | 接 | 待做 |
| DataTable 数据 | **不接**，保留现有 CSV → SO 管线 | 已决定 |

数据层不接的理由：框架的 DataTable 要求每张表手写 `DataRowBase` 子类实现文本解析，
官方生成器不在安装的包里；而现有 CSV → SO 管线已跑通、有哈希校验与稳定的 GUID，
换成 DataTable 是更多工作换来更差的可维护性。

## 2. 已完成：Procedure

启动场景：`Assets/Scenes/Boot.unity`

```
GameFramework            （框架预制体实例）
├─ BaseComponent
├─ EditorResourceComponent
└─ ProcedureComponent     （可用流程 + 入口流程）
```

流程类型以**全限定类型名**登记：

- `TurnCard.Game.Procedure.ProcedureBoot`（入口）
- `TurnCard.Game.Procedure.ProcedureBattle`

运行验证：进入播放模式后 `ProcedureComponent.CurrentProcedure` 为 `ProcedureBattle`，
说明 Boot 进入后成功切换。

流程类是**普通 C# 类**（继承 `ProcedureBase`），由框架用 `Activator.CreateInstance` 反射创建，
不是 MonoBehaviour——这一点和很多教程里「流程挂成组件」的说法不同。

## 3. 接入时踩到的三个坑

### 3.1 框架预制体是精简版

`Packages/com.jiangyin.gameframework/GameFramework.prefab` 只挂了
`BaseComponent` 与 `EditorResourceComponent`，**不是**官方仓库里那份二十多个组件的完整预制体。
用到哪个模块就要自己往这个对象上加对应组件。

### 3.2 包里没有 UGuiForm

官方仓库的 `UGuiForm`、`UGuiFormHelper`、`UGuiGroupHelper`（负责 CanvasGroup、排序、
Raycaster 注册的 UGUI 适配层）在这个包里**完全不存在**：

- `Scripts/Runtime/UI/` 只有通用 `UIForm`、`UIFormLogic`、`DefaultUIFormHelper`；
- 全包搜索 `UGuiForm` 零结果。

要用官方形态，必须自己补这三个类。

### 3.3 EditorResourceComponent 不继承 ResourceComponent

它的基类是 `MonoBehaviour`，不是 `ResourceComponent`。
而框架自带的 `DefaultUIFormHelper.Start()` 里写死了：

```csharp
m_ResourceComponent = GameEntry.GetComponent<ResourceComponent>();
if (m_ResourceComponent == null) { Log.Fatal("Resource component is invalid."); return; }
```

所以只要用的是 `EditorResourceComponent`，直接套用 `DefaultUIFormHelper` 就会 fatal。
写 `UGuiFormHelper` 时必须绕开这条路径——自己决定界面预制体怎么加载
（编辑器模式下直接走 `Resources.Load` 或 AssetDatabase 都可以）。

## 4. 待做：UI 层

计划：

1. 写 `UGuiForm`、`UGuiFormHelper`、`UGuiGroupHelper` 三个类；
2. 给 `GameEntry` 挂 `UIComponent`，并把 helper 指到上面三个类；
3. 把战斗界面做成预制体，由 `ProcedureBattle` 通过 `UIComponent.OpenUIForm` 打开；
4. 现有 `BattleScene` 的流程编排拆进界面逻辑，场景本身退化为纯容器。

这一步会替换掉当前能跑通的 `Battle.unity`，所以需要在迁移期间保留旧场景作对照。

## 5. 待做：Resource 层

目前资源都从 `Resources` 目录直接加载（`Resources.Load`）。接入 Resource 组件后改为按名加载，
需要配置 `ResourceSettings` 并决定编辑器模式与打包模式。建议放在 UI 迁移之后。

## 6. 界面绑定层：UIBind

### 6.1 来源与取舍

来源是 `D:\实习存档` 里的三份框架与本机插件副本，评估后**只采用 QuickUI**：

| 候选 | 结论 | 原因 |
| --- | --- | --- |
| `QuickUI_Standalone` | **采用，改名 UIBind** | 预制体绑定 + 代码生成，零依赖，正好补界面层最费手的一段 |
| `03_UIFramework` | 不采用 | 未编译通过，依赖 22 个程序集（URP / Spine / Coffee.UIParticle / FSNative） |
| `02_TableConfig` | 不采用 | 只有生成结果，Excel→C# 生成器不在包内；且数据层保留 CSV → SO |
| `01_FunnyBase` | 不采用 | 131 个工具类，为几个扩展方法引入不划算 |
| `PaidPlugins_Internal` | 不采用 | 席位授权的商业插件，不适合放进个人工程 |

### 6.2 改名结果

| 项 | 旧 | 新 |
| --- | --- | --- |
| 目录 | `Assets/QuickUI` | `Assets/UIBind` |
| 程序集 | `QuickUI.Runtime` / `QuickUI.Editor` | `UIBind.Runtime` / `UIBind.Editor` |
| 命名空间 | `Funny.GameEngine.U3D.Extension` | `TurnCard.UIBind` / `TurnCard.UIBind.Editor` |
| 类型前缀 | `QuickUI*` | `UIBind*`（`UIBindWindow` / `UIBindFragment` / `UIBindItem` 等） |
| 菜单 | `QuickUI/...` | `UIBind/...` |

同时把默认路径改成本工程的结构：

- 生成脚本根目录：`Assets/Game/Scripts/UI/`
- 可用预制体范围：`Assets/Game/Resources/UI/`

### 6.3 与 GameFramework 的接点

`Assets/UIBind/Runtime/UIBindShims.cs` 里三个基类现在都继承 `MonoBehaviour`，只提供
`OnCreate` / `OnShow` / `OnHide` 回调。框架的 `UIFormLogic` 同样是 MonoBehaviour，
所以把 `UIBindWindow` 的继承目标改成 `UIFormLogic`，生成的窗口脚本就直接是框架认可的界面逻辑，
而且**生成脚本一行都不用改**。

这样两块正好互补：

- UIBind 负责「预制体子节点 → 强类型字段」的绑定的代码生成；
- 自写的 `UGuiForm` 三件套负责 Canvas 管理、排序与打开关闭。

### 6.4 注意事项

- `UIBind/自检 (Self Check)` 会弹**模态对话框**，用脚本自动调用会把编辑器主线程锁死，自动化时不要调 `Run()`。
- 用 PowerShell 的 `Get-Content` 查看包内文件时中文会显示成乱码，那是控制台按 ANSI 解码所致，
  文件本身是 UTF-8，用 UTF-8 读取即正常。
