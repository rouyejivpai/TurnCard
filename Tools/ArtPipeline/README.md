# Aseprite → Unity 卡图管线

把 `ArtSource/123/**/*.aseprite` 导出成 Unity 可直接使用的卡面图集与动画。

```
ArtSource/123/<分组>/<工程>.aseprite
        │  export_cards.py（调用 Aseprite CLI，2 倍最近邻放大）
        ▼
Assets/Game/Art/Cards/<Entity>/<Name>_sheet.png     ← 横向图集，Multiple 切片
Assets/Game/Art/Cards/<Entity>/<Name>.png           ← 首帧静态图，卡面用
Assets/Game/Art/Cards/CardArtManifest.json          ← 帧矩形/时长清单，Editor 脚本读它
Assets/Game/Art/Cards/<Entity>/<Name>_Loop.anim     ← 循环动画（由 Unity 菜单生成）
```

## 用法

```powershell
# 1) 导出（默认 2x），同时写入 Assets
python Tools/ArtPipeline/export_cards.py

# 只看导出结果、不碰 Assets
python Tools/ArtPipeline/export_cards.py --stage-only

# 换放大倍数
python Tools/ArtPipeline/export_cards.py --scale 4
```

```text
# 2) Unity 里执行菜单
Tools/TurnCard/卡牌美术/应用导入设置并生成动画
```

菜单做三件事：把图集设为 `Sprite / Multiple / Point / 无压缩 / 无 Mipmap / PPU 100 / FullRect`，
按清单切片命名 `<Name>_<帧号>`，并按帧时长生成循环 AnimationClip（同时绑定 `SpriteRenderer.m_Sprite`
和 `Image.m_Sprite`，UI 与场景物体都能直接用）。

改了工程或用新图后，**重跑第 1 步 + 第 2 步**即可；已有 clip 走 `CopySerialized` 覆盖，GUID 不变。

## 命名对照

| 源工程 | 资源名 |
| --- | --- |
| `purple/Farmer_WolfMan/farmer_idle` | `Farmer/Farmer_Idle` |
| `purple/Farmer_WolfMan/wolfman_idle` | `Wolfman/Wolfman_Idle` |
| `purple/Whale/whale_1` · `whale_2` | `Whale_{1,2}` |
| `purple/贤者1/xianzhe_1` · `xianzhe_2` | `Sage/{1,2}` |
| `Common/Stick/Stick_1` · `Stick_2` | `Stick/{1,2}` |
| `Common/剧作之手/The Hand_1` · `_2` | `Hand/{1,2}` |
| `Common/Fire gbl/Fire Gbl_1` · `02` | `Fire/{1,2}` |
| `Common/sleep gbl/01` · `02` | `Sleep/{1,2}` |
| `Common/face/01` · `02` | `Face/{1,2}` |
| `Common/剧作舞者/01` · `02` | `Dancer/{1,2}` |
| `Special/Chest/chest_pawn` · `chest_queen` · `chest_queen2` | `Chest/Chest_*` |

对照表在 `export_cards.py` 的 `ENTRIES` 里，加新工程时改这一处即可。

## 注意事项

- **Aseprite 是 GUI 程序**：导出目录不存在时会弹模态框，进程会永久挂起。脚本已预建目录并带 90s 超时。
- `--script` 必须放在工程路径**之后**，否则 `app.activeSprite` 为 nil。
- 没有 `sprite:save()`，改工程只能 `saveAs(原路径)`；`--layer/--ignore-layer` 在 1.3.18 会**静默失效**，层的显隐要用 Lua。
- 首帧静态图是从图集里裁的（PIL），不要用 `--frame-range 1,1 --save-as`——单帧工程会导出空白图。
- 导出的工程本身是「整幅小场景」且完全不透明（含背景），没有透明区域；要透明角色图需要在 Lua 里关掉背景层再导。
