# 美术生成提示词（GPT 出图）

工作流：**GPT 生图 → 纯色键色底 → 抠图 → 最近邻缩放到目标尺寸 → Unity 里 Point 过滤**。

---

## 0. 通用风格段（每条提示词都要带上）

### 正向

```
pixel art, crisp hard pixel edges, no anti-aliasing, no blur,
bright retro palette (limited to ~32 colors), strong saturation,
high contrast highlights, clean readable silhouette,
front-facing flat composition, centered, no perspective distortion,
game UI asset, isolated on solid pure magenta background (#FF00FF),
no text, no letters, no numbers, no watermark
```

### 负向

```
blurry, smooth gradient, 3d render, photorealistic, painterly,
anti-aliased edges, soft shadows, glow bloom, noise, film grain,
text, letters, numbers, watermark, signature, busy background, perspective, tilt
```

> **为什么用品红底**：品红在像素画里几乎不会被用到，抠图时一条 `Color Key` 就能干净去掉，边缘不会吃掉深色描边。

### 通用后处理

1. 出图后裁成目标**宽高比**（不要用缩放，先裁）
2. 魔棒/Color Key 去掉品红，容差 10–20
3. 缩放到目标像素尺寸：**最近邻（Nearest Neighbour）**，不要用双三次
4. Unity 导入：`Filter Mode = Point`、`Compression = None`、`Sprite (2D and UI)`

---

## 1. 卡牌模板（P0，最重要）

裁成 **380×536**（宽高比约 1:1.41，竖版卡）。

```
A single empty pixel art trading card frame, vertical portrait card, aspect ratio 5:7.

Layout, top to bottom:
- a round cost slot in the TOP-LEFT corner (empty circle socket, no number)
- a horizontal name banner across the top (empty, no text)
- a large empty rectangular illustration window in the center (plain dark panel, no picture)
- a thin horizontal type strip below the illustration window (empty)
- a large empty description panel at the bottom (plain parchment/dark panel, no text)

Fast pixel-art style frame: 4 pixel thick outer border, 2 pixel inner bevel highlight,
subtle corner ornaments (small triangles or studs), clean and readable like Slay the Spire card frame.
Bright retro palette: warm gold border, deep blue-violet card body, bright cyan rim light.
All inner areas empty and flat so text can be drawn on top later.
Isolated on solid pure magenta background.
```

**变体（正反共用同一模板，只换配色）**：

```
same card frame layout, but the body is deep crimson red with bronze border
same card frame layout, but the body is teal green with silver border
```

> 不建议让 AI 一次出 3 种配色，容易跑偏；**一次一张，只改配色关键词**最稳。

---

## 2. 类型底纹 ×3（P0）

目标 **380×536**，只画底纹、**不要边框**，用来叠在模板下面区分卡牌类型。

```
pixel art card background only, no frame, no border,
full-bleed flat panel, aspect ratio 5:7,
subtle repeating pixel pattern (diagonal stripes / small diamond lattice),
bright retro palette, low contrast so白字可读,
[攻击] deep red base with darker red diagonal stripes
[技能] deep blue base with darker blue diamond lattice
[能力] purple base with subtle arcane rune dots
isolated on solid pure magenta background
```

三条分别把方括号里的配色替换掉。

---

## 3. 按钮九宫格 ×4 态（P0）

目标 **240×80**，九宫格边距 12。

```
pixel art UI button, horizontal, aspect ratio 3:1,
rounded rectangle with 3 pixel outer border and 2 pixel inner bevel,
[状态] version, bright retro palette, gold border, deep blue body,
no text, no icon, centered empty face,
isolated on solid pure magenta background
```

状态依次替换：

| 状态 | 追加描述 |
| --- | --- |
| normal | muted saturation, normal brightness |
| hover | brighter rim light, slight glow on border |
| pressed | darker body, border looks pushed in (inverted bevel) |
| disabled | desaturated, grey-blue body, dim border |

---

## 4. 面板 / 窗口九宫格（P0）

目标 **720×480**，九宫格边距 **24**（四角必须完全对称，否则拉伸会歪）。

```
pixel art UI panel window frame, aspect ratio 3:2,
rectangular, 4 pixel thick bright gold border, 2 pixel inner dark line,
four identical decorative corners (small pixel studs), no title bar,
empty dark blue-violet interior filling the whole center,
perfectly symmetrical so it can be sliced into 9 pieces,
isolated on solid pure magenta background
```

---

## 5. 地图节点图标 ×5（P0）

目标 **96×96**，方形，主体居中，占画面 80%。

```
pixel art game map node icon, square, centered, bold silhouette,
[图标描述],
bright retro palette, thick dark outline, 2 pixel rim light,
flat front view, no background, isolated on solid pure magenta background
```

| 节点 | 图标描述 |
| --- | --- |
| 战斗 | a crossed swords emblem |
| 精英 | a skull with a small crown |
| 营地 | a campfire with two logs |
| 商店 | a coin purse with a gold coin |
| 首领 | a horned demon head, larger and menacing |

---

## 6. 抽牌堆 / 弃牌堆（P1）

目标 **120×120**。

```
pixel art card pile icon, square, [正面朝下的牌堆 / 散乱的牌堆],
drawn as a small stack of cards with visible thickness,
bright retro palette, thick outline, centered,
isolated on solid pure magenta background
```

- 抽牌堆：`a neat stack of face-down cards`
- 弃牌堆：`a messy scattered pile of cards`

---

## 7. 能量球（P1）

目标 **96×96**。

```
pixel art energy orb icon, circular, glowing blue-white核心,
bright cyan rim, small sparkles, bright retro palette, thick dark outline,
centered, isolated on solid pure magenta background
```

---

## 8. 图标集（P1）

目标 **48×48**，每条一次出**一个**图标（AI 一次出多个容易糊）。

```
pixel art game UI icon, 48x48, single centered symbol, bold readable silhouette,
[图标描述], bright retro palette, thick dark outline,
no text, isolated on solid pure magenta background
```

| 图标 | 描述 |
| --- | --- |
| 金币 | a stack of three gold coins |
| 生命 | a red heart with a small cross |
| 护甲 | a kite shield |
| 能量 | a small lightning bolt |
| 攻击 | a single sword pointing up |
| 抽牌 | two cards with an upward arrow |
| 关闭 | a thick X |
| 返回 | a left-pointing arrow with a small curve |

---

## 9. 背景（P1）

目标 **960×540**（游戏内放大 2 倍铺满 1920×1080）。

### 战斗背景

```
pixel art game background, wide 16:9,
dark castle dungeon hall, stone pillars on both sides,
faint torch light, deep shadows in the corners,
BRIGHT retro palette accents, empty clear center stage for characters,
no characters, no text, no UI,
pixel art, hard edges, no anti-aliasing
```

> 注意：背景**不要**要求品红底、也不抠图，直接出图后缩放。

### 地图背景

```
pixel art game background, wide 16:9,
vertical mountain path seen from afar, distant castle at the top,
muted but saturated colors, empty mid-space for a path overlay,
no path lines drawn, no characters, no text, no UI
```

### 主菜单背景

```
pixel art game background, wide 16:9,
a single floating card silhouette glowing in the dark,
magical particles, dramatic vignette,
empty center for the game title, no text, no characters
```

---

## 10. 特效（P2，可选）

### 翻面闪光（8 帧）

```
pixel art effect sprite sheet, 8 frames in a 4x2 grid,
a bright white-blue card flip flash, expanding ring then fading sparks,
each frame isolated on solid pure magenta background, no text
```

### 命中火花

```
pixel art hit spark effect, single frame, 32x32,
star-shaped burst, bright yellow-white center with orange rim,
isolated on solid pure magenta background
```

---

## 11. 实际生成时的小技巧

1. **一次只出一个资源**，不要试图让 AI 一图多物
2. 尺寸说「aspect ratio」而不是像素值，AI 对比例更敏感
3. 结果不满意时**只改一个关键词**（比如把 `gold border` 换成 `bronze border`），一次改太多会失控
4. 出图偏「顺滑」时，在提示词里加强：`8-bit style, pixel grid visible, dithering`
5. 出图偏「脏」时，减细节：`minimal detail, flat colors, only 3 shades per material`
6. 需要**完全对称**的资源（面板、按钮），可在提示词里加 `perfectly symmetrical, mirrored left and right`

---

## 12. 到手之后我这边要做的

1. 抠图 → 缩放到目标尺寸 → Point 过滤
2. 卡牌模板分区域切出费用槽/名称条/插画窗/描述区的锚点
3. 按钮和面板设九宫格边距
4. 图标打进 Sprite Atlas
5. 按新模板重排卡面文字（现在名称偏右、美术区是灰块）
