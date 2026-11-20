# 像素字体（方舟像素字体 12px）

## 文件

| 文件 | 说明 |
| --- | --- |
| `ark-pixel-12px-proportional-zh_hans.ttf` | 方舟像素字体 12px 比例宽度，简体中文子集，5.2 MB |
| `OFL.txt` | SIL Open Font License 1.1，随字体分发，**不要删** |

- 上游：https://github.com/TakWolf/ark-pixel-font
- 版本：2026.11.10
- 授权：OFL 1.1 —— 可商用、可嵌入、可修改；不能单独售卖字体本身，衍生字体必须沿用 OFL。

## 为什么是 12px

汉字在 12px 以下会缺笔画（10px 只适合做数字和英文）。这个字体的字形就是按 12×12 网格设计的，
所以**只能按 12 的整数倍使用**：12 / 24 / 36 / 48。用 13、15 这类字号会重采样，笔画糊掉。

## 在 Unity 里生成 TMP 资源

菜单：**Tools ▸ TurnCard ▸ 字体 ▸ 生成像素字体 TMP 资源**

生成到 `Assets/Game/Resources/Fonts/ArkPixel12 SDF.asset`，参数：

| 参数 | 值 | 原因 |
| --- | --- | --- |
| Render Mode | Raster（位图） | SDF 会把 1px 笔画糊成灰边 |
| Sampling Point Size | 12 | 必须等于字体原生尺寸 |
| Atlas Size | 1024×1024，Dynamic，多图集 | 中文按需烘焙，不用一次性塞满 |
| Atlas Filter | Point | 放大时保持硬边 |
| Material Shader | TextMeshPro/Bitmap | 位图模式配套着色器 |

## 用的时候注意

1. 字号只能是 12 / 24 / 36 / 48；非整数倍会破坏像素网格
2. TextMeshPro 组件的 **Font Size 与 RectTransform 的缩放不要同时改**，选一个维度缩放
3. 表情符号（⚡ ❤）字体里没有，会显示成方框——用精灵图代替
4. 字体纹理不要开 Compression，也不要开 Mipmap

## 手动生成的等效步骤（脚本不可用时）

`Window ▸ TextMeshPro ▸ Font Asset Creator`：

- Source Font File：这个 ttf
- Sampling Point Size：12，Padding：0
- Atlas Resolution：1024×1024，Character Set：Characters from File / Unicode Range
- Render Mode：**Raster**，然后 Generate
