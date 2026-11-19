# 技术栈

状态：记录 + 待决。本篇只记录已经落地的事实，以及尚未决定的技术选型。

## 1. 已落地的事实

| 项 | 现状 |
| --- | --- |
| Unity | 2022.3.62f2c1（2D 模板，Built-in 渲染管线） |
| 语言 | C# |
| 框架 | Unity GameFramework `com.jiangyin.gameframework` 2021.5.31（本地 UPM 包） |
| 框架位置 | `Packages/com.jiangyin.gameframework` |
| 框架构成 | 核心 `GameFramework.dll`（预编译）+ 23 个 Unity 层模块源码 + SharpZipLib |
| 程序集 | `UnityGameFramework.Runtime`、`UnityGameFramework.Editor`，已编译通过 |
| 开发桥接 | MCP for Unity 10.2.0，位于 `Packages/com.coplaydev.unity-mcp`（已 gitignore） |
| 版本控制 | Git + LFS，远端 `github.com/rouyejivpai/TurnCard` |

### 框架包的两处本地修正

引入时遇到两个必须处理的问题，已在本地包内修正，重新安装时需重复：

1. **版本号不合 SemVer**：原 `package.json` 为 `"version": "2021.05.31"`，`05` 带前导零，
   被 Unity 2022 包管理器判定为无效包。已改为 `"2021.5.31"`。
2. **资产数据库不导入**：修正版本号后包虽被登记，但其中的脚本从未进入 AssetDatabase，
   因而不会编译。需要对包根目录执行一次强制导入
   （`AssetDatabase.ImportAsset(path, ImportRecursive | ForceUpdate)`）或重启编辑器。

## 2. GameFramework 的模块

框架内含 19 个模块：Config、DataNode、DataTable、Debugger、Download、Entity、Event、
FileSystem、Fsm、Localization、Network、ObjectPool、Procedure、ReferencePool、Resource、
Scene、Setting、Sound、UI、Utility、Variable、WebRequest。

模块随 DLL 一并存在，取舍在于「用不用」，不在于「装没装」。

## 3. 待决选型

| 议题 | 结论 / 备选 | 状态 |
| --- | --- | --- |
| UI 方案 | UGUI + GameFramework 的 `UGuiForm` | 已定 |
| 配表方案 | CSV（Excel 编辑）→ 生成 SO，见 `06-data-pipeline.md` | 已定 |
| 资源与热更 | 不热更，资源直接打进包 | 已定 |
| 渲染管线 | 维持 Built-in / 升级 URP 2D | 待定 |
| 存档 | GameFramework Setting / 自定义 JSON | 待定 |
| 输入 | 旧 Input Manager / Input System | 待定 |
| 框架模块取舍 | 19 个模块中用哪些（Procedure、UI、Event、Resource…） | 待定 |

## 4. 目录约定（待确认）

```
Assets/
  Scripts/          项目脚本
  Game/             玩法、数据、界面
  Scenes/           场景
Packages/
  com.jiangyin.gameframework/
Docs/               设计文档
```

框架自带 `GameFramework.prefab`（GameEntry 预制体），通常由启动场景实例化，
是运行时所有模块的入口。
