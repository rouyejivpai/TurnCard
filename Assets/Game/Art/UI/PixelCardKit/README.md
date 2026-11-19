# PixelCardKit — 像素卡牌 UI 素材包

为 TurnCard 整理的第三方像素卡牌 UI 素材（全部 CC0）。来源与授权见 [LICENSE.md](LICENSE.md)。

## 目录

```
PixelCardKit/
├─ Cards/     100×128 卡背 7 张、镂空卡框 3 张（铜/银/金）、空卡面 1 张
├─ Panels/    48×48 九宫格面板 33 张（Ancient 描边 / Colored 平涂 / Outline 纯描边）
├─ Orbs/      16×16 数字徽章 30 张（蓝/绿/橙 三色 × 0–9）
└─ PixelCardKit.json   导入参数 + 每张图的九宫格边界
```

## 导入预设

菜单：**Tools ▸ TurnCard ▸ 像素卡牌 ▸ 应用 PixelCardKit 导入预设**

做四件事，只作用于本目录：

1. `Sprite` + `Point (no filter)` + 无压缩 + 无 Mipmap + `Clamp`
2. PPU 100（与 `CardArtImporter` 的卡牌立绘一致）
3. `spriteMeshType = FullRect`，九宫格拉伸时顶点稳定
4. 按 `PixelCardKit.json` 的 `entries` 写回 `spriteBorder`

新增图片后，把它加进 json 的 `entries` 再跑一次即可，没必要手点 Inspector。

## 怎么拼一张卡

```
Card (Image, 九宫格 = Cards/card_frame_gold, 无 Type=Simple)
├─ Body      Image = Cards/card_back_purple，铺满并留出 12px 边
├─ Portrait  Image，放在 Body 上方的画框区
├─ Title     TextMeshPro
├─ Cost      Image = Orbs/digit_blue_3（16×16，×3 放大）
└─ Hp/Atk    Image = Orbs/digit_orange_7
```

- 卡框是**镂空**的：`card_frame_*` 中心透明，底色由下层 `card_back_*` 或自绘面板给。
- 数字徽章是 0–9 的单张图，换数值就是换 sprite，不需要字体渲染；要显示两位数就并排放两个。
- 面板 `Panels/Ancient/*` 自带 12px 描边，做卡牌下方的卡座 / 详情浮层比 `Colored` 更有层次。

## 已知限制

- 卡背/卡面是固定 100×128，**不是**可变尺寸；只有 `card_frame_*` 和 `Panels/*` 适合九宫格拉伸。
- Kenney `UI Pack - Pixel Adventure` 是 9×9 拼块（tile）体系，没有可单独使用的面板图，因此**没有**
  放进本目录，只保留在 `ArtSource/PixelCard/KenneyUIPackPixelAdventure/` 备查。
- 素材分辨率都很小（16–128px），UI 里请用整数倍放大（×2/×3/×4），非整数倍会出现像素抖动。
