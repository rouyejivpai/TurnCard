# 美术素材授权（PixelCardKit）

本目录是第三方开源素材的**整理版**（已切分 / 已按统一规则设置导入参数），原始包留在
`ArtSource/PixelCard/`，不要在这里直接改像素。

| 子目录 / 文件 | 来源 | 授权 |
| --- | --- | --- |
| `Cards/card_back_*.png`<br>`Cards/card_face_empty.png`<br>`Cards/card_frame_{bronze,silver,gold}.png` | [Pixel Card Assets](https://opengameart.org/content/pixel-card-assets)（storiesstrauss） | **CC0** |
| `Orbs/digit_{blue,green,orange}_0-9.png` | [Pixel Art Card Stats Orbs](https://opengameart.org/content/pixel-art-card-stats-orbs)（DustDFG） | **CC0** |
| `Panels/**` | [Kenney Pixel UI Pack](https://kenney.nl/assets/pixel-ui-pack) 的 `9-Slice/` 子集 | **CC0** |

## 处理说明

- `Cards/*` 从 720×613 的精灵表按连通域切成单图，卡框保留 100×128 原始像素尺寸。
- `Cards/card_frame_*.png` 为**镂空卡框**：中间透明，卡面底图要放在它下一层。
- `Orbs/*` 从 80×32 的 5×2 数字条切成 30 张 16×16 单图（0–9 三色）。
- `Panels/Ancient` 是木/石/白三套描边最重的面板，`Colored` / `Outline` 是平涂与纯描边两种。

> CC0 表示可自由商用、无需署名。仍在此记录来源，方便追溯。
