# 卡牌模板美术拆解与出图提示词

面向 GPT 图像生成。**每件单独生成、单独交付**，结构由代码拼装，避免 AI 画不准布局的问题。

坐标基准：卡牌 **380×536**（实际显示 190×268，等比 0.5）。

---

## 一、拆解总表

| # | 部件 | 尺寸 | 是否平铺 | 用途 |
| --- | --- | --- | --- | --- |
| 1 | 边框角块 | 32×32 | 否（镜像复用四角） | 卡牌四个角 |
| 2 | 边框边条 | 32×32 | **是**（横竖各一） | 卡牌四条边 |
| 3 | 费用槽徽章 | 72×72 | 否 | 左上角费用 |
| 4 | 名称条 | 260×56 | 否（横向拉伸） | 顶部卡名 |
| 5 | 类型条 | 150×40 | 否（横向拉伸） | 卡牌类型 |
| 6 | 描述区底板 | 340×160 | 否（九宫格拉伸） | 底部描述 |
| 7 | 卡面底纹 | 64×64 | **是**（四方连续） | 卡牌底色 |
| 8 | 类型图标 ×3 | 48×48 | 否 | 攻击 / 技能 / 能力 |
| 9 | 卡背徽记 | 160×160 | 否 | 背面中央图案（可选） |

**不需要 AI 生成**：文字（用 TMP）、费用数字、分隔线、边框几何（代码绘制）。

---

## 二、通用风格段（每条提示词都带上）

### 正向（放在提示词开头）

```
pixel art game UI asset, bright retro palette, hard pixel edges, no anti-aliasing,
limited palette around 24 colors, top-left light source, flat front view,
isolated on solid pure magenta background (#FF00FF), no text, no letters, no numbers
```

### 负向（每条都带上）

```
blurry, smooth gradient, 3d render, photorealistic, photograph, film grain, noise,
text, letters, numbers, watermark, signature, drop shadow, perspective, tilted,
busy background, transparent background
```

> **为什么用品红底**：像素画几乎不会用到品红，抠图一条 Color Key 就干净去掉，不会吃掉深色描边。

---

## 三、逐件提示词（可直接复制）

### 1. 边框角块

```
[通用正向] A single right-angle corner ornament for a trading card frame,
occupying the bottom-left corner of the image, an L-shaped piece with a
decorative gold boss and two small pixel studs, deep blue inner face with a
bright gold rim, 2 pixel dark outline, the top and right edges of the L are
clean straight lines that continue off the image edge, symmetrical and
simple, readable at small size.
```

> 出了之后代码会**镜像**出另外三个角，所以只需要一个角。要求「上边和右边是直线通到画面外」，这样拼接不会有断层。

### 2. 边框边条（横向，平铺用）

```
[通用正向] A single horizontal border strip segment for a trading card frame,
a long rectangle 32 pixels tall, horizontal gold trim line with a subtle
inner highlight and a 1 pixel dark shadow line beneath it, deep blue below the
trim, the left and right ends must line up seamlessly so the strip tiles
horizontally, no ornament in the middle, very simple and flat.
```

> 纵向边条用同一张旋转 90°，不必单独生成。

### 3. 费用槽徽章

```
[通用正向] A round cost badge for a card game, octagonal pixel medallion,
bright gold ring with a dark blue hollow center, small circular gem at the
top, thick 2 pixel outline, centered in the image, symmetrical.
```

### 4. 名称条

```
[通用正向] A horizontal name banner for a card game, long flat ribbon
with angled ends, deep blue-violet fill, gold top and bottom trim, a thin
1 pixel highlight along the top edge, empty in the middle with no text,
symmetrical left and right.
```

### 5. 类型条

```
[通用正向] A small horizontal type strip for a card game, short flat bar
with slightly tapered ends, muted gold fill, 1 pixel dark outline, empty
center, symmetrical.
```

### 6. 描述区底板

```
[通用正向] A rectangular description panel for a card game, wide and short,
dark navy semi-transparent looking fill with a subtle 1 pixel lighter inner
border, four identical small corner ticks, no ornament in the middle,
perfectly symmetrical so it can be sliced into 9 pieces.
```

### 7. 卡面底纹（四方连续）

```
[通用正向] A seamless tiling background texture for a card game,
64x64 tile, very subtle diagonal pixel weave on a deep blue-violet base,
low contrast so white text stays readable, perfectly seamless on all four
edges, no ornament, no border.
```

### 8. 类型图标 ×3

**攻击**

```
[通用正向] A single small icon of a crossed sword and dagger, bold readable
silhouette, bright red and gold, thick 2 pixel outline, centered, facing
straight forward.
```

**技能**

```
[通用正向] A single small icon of an open spellbook with a small spark,
bold readable silhouette, bright cyan and silver, thick 2 pixel outline,
centered, facing straight forward.
```

**能力**

```
[通用正向] A single small icon of a glowing rune stone, bold readable
silhouette, bright violet and gold, thick 2 pixel outline, centered,
facing straight forward.
```

### 9. 卡背徽记（可选）

```
[通用正向] A circular emblem for the back of a trading card, an ornate
compass rose inside a gold ring, deep crimson background, symmetrical,
centered, no text.
```

---

## 四、生成顺序与一致性

分开生成的部件容易「不像一套」。按这个顺序能大幅提升一致性：

1. **先出底纹（#7）** —— 它决定整体色调
2. **再出边框角块 + 边条（#1、#2）** —— 用 #7 作为参考图（image-to-image），让它沿用同一套颜色
3. **然后是名称条、类型条、描述底板（#4、#5、#6）** —— 同样带上 #7 或角块作参考
4. **最后出图标（#8）**

**关键**：每一步都把上一步的成品作为参考图喂给模型，并在提示词末尾追加一句：

```
match the exact palette and outline weight of the reference image
```

---

## 五、交付验收标准

| 项 | 要求 |
| --- | --- |
| 背景 | 纯 **#FF00FF** 品红，不能有渐变或阴影 |
| 文字 | **不能有任何文字/数字**（会被误识别成内容） |
| 边缘 | 硬像素边，不能有模糊过渡 |
| 构图 | 正俯视、居中、对称（角块除外） |
| 格式 | PNG，不压缩 |
| 尺寸 | 按上表出图，或出 4 倍再让我缩 |

---

## 六、我这边接收后做的处理

1. 抠掉品红（容差 10–20）
2. 最近邻缩放到目标尺寸，开启 Point 过滤、关闭压缩
3. 角块镜像拼四个角，边条平铺四条边 → 合成完整边框
4. 按九宫格切名称条 / 描述底板
5. 把部件挂进 `CardView`，文字与数值仍由 TMP 绘制
