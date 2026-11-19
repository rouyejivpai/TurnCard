# 数据表管线

状态：已落地可用。目的：让数据在 Excel 里编辑，在 Unity 里以 SO 资产的形式被使用。

## 1. 目录约定

```
DataTables/                    CSV 源表（工程根目录，不进 Assets）
  Cards.csv
  .generated.json              生成记录（哈希 + 行数 + 时间），自动维护
Assets/Game/Scripts/Data/       数据类定义（手写）
  CardDefinition.cs
Assets/Game/Scripts/Editor/     生成器（手写）
  DataTableImporter.cs
Assets/Game/Data/Generated/     生成产物（自动生成，不手改）
  Cards/1001.asset ...
```

**CSV 是唯一数据源，`Generated/` 下的一切都是产物。** 产物需要提交进仓库，
这样克隆下来无需跑生成器就能运行。

## 2. 使用流程

1. 在 Excel 里编辑 `DataTables/Cards.csv`，另存为「CSV UTF-8」。
2. Unity 菜单 `Tools/TurnCard/数据表/生成 SO`。
3. Unity 菜单 `Tools/TurnCard/数据表/检查一致性` 复核。

第 3 步会报告三类问题：源表改过但没重新生成、某个 id 缺资产、产物里有源表中已删除的 id。

## 3. 列与字段的对应规则

- 列名直接映射到字段名：`displayName` → `m_DisplayName`，`art_path` → `m_ArtPath`。
- `id` 列映射到 `m_Id`，同时决定资产文件名（`<id>.asset`）。
- 表头有不认识的列会记一条日志并忽略，不会中断生成。

值如何解析取决于字段类型：

| 字段类型 | 支持的写法 |
| --- | --- |
| `int` / `float` | 数字；小数必须用 `.`，逗号会被当分隔符 |
| `bool` | `true/false`、`1/0`、`是/否`、`y/n` |
| `string` | 原样写入，支持字段内逗号与换行（用双引号包裹） |
| 枚举 | 枚举成员名，大小写不敏感 |
| 资源引用 | 从 `Assets/` 开始的完整路径，如 `Assets/Game/Art/Cards/1001.png` |

## 4. 加一张新表

1. 新建数据类，字段用 `m_` 前缀（见 `CardDefinition`）。
2. 在 `DataTables/` 下新建同名 CSV，表头与字段对应。
3. 在 `DataTableImporter.Specs` 里加一条 `TableSpec("表名", typeof(数据类), "子目录")`。

不需要改其他代码。

## 5. 已知的坑

- **字段不能叫 `m_Name`**：与 `MonoBehaviour` 内置的 `name` 冲突，Unity 会报
  「The same field name is serialized multiple times」。用 `m_DisplayName` 之类。
- **`id` 列不要跳过**：资产文件名来自 id，但字段 `m_Id` 也必须写入，否则文件名叫 1001、
  字段值却是 0。
- **`.xlsx` 不要提交**：二进制无法 diff 与合并。只提交导出的 CSV；`.xlsx` 若需要留存，
  放进 LFS 或干脆不进仓库。
- **Excel 导出要选「CSV UTF-8」**：默认的 ANSI 导出会把中文写坏。
- **改表后忘了生成**：一致性检查就是为这个准备的，建议提交前跑一次。
