# 第三方像素卡牌素材（原始包）

Unity **不导入**这个目录（见上级 `ArtSource/README.md`）。这里只放下载下来的原始包，
整理后的版本在 `Assets/Game/Art/UI/PixelCardKit/`。

| 目录 | 内容 | 来源 | 授权 |
| --- | --- | --- | --- |
| `PixelCardAssets/` | 720×613 精灵表、卡背表、抽卡撕裂特效帧；`sliced/` 是按连通域切好的单图 | [Pixel Card Assets](https://opengameart.org/content/pixel-card-assets) | CC0 |
| `CardStatsOrbs/` | 80×32 数字条蓝/绿/橙三色 + .ase 源文件；`orbs16/` 是切好的 16×16 单图 | [Pixel Art Card Stats Orbs](https://opengameart.org/content/pixel-art-card-stats-orbs) | CC0 |
| `KenneyPixelUIPack/` | 750 件：9-Slice 面板、精灵表、箭头与图标 | [Kenney Pixel UI Pack](https://kenney.nl/assets/pixel-ui-pack) | CC0 |
| `KenneyUIPackPixelAdventure/` | 519 个 9×9 拼块（Large/Small Tiles），用于拼装自适应面板 | [Kenney UI Pack - Pixel Adventure](https://kenney.nl/assets/ui-pack-pixel-adventure) | CC0 |

原始 zip 在 `Docs/pixel-card-refs/downloads/`。

## 为什么 Adventure 包没进 Unity

它是 9×9 拼块体系：一张面板要由 9 个 24×24 的 tile 拼出来（四角 + 四边 + 中心），
直接在 UGUI 里用很别扭。需要它的那种"厚描边 + 木纹"质感时，再单独做一个 9-slice
拼装工具，或者只用它的边框条做 HUD 装饰。
