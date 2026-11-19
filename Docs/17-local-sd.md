# 本地 AI 出图环境（ComfyUI）

## 目录

```
D:\SD\
├─ ComfyUI_windows_portable\      ComfyUI v0.45.0 便携版（自带 Python 3.13 + torch 2.14 cu130）
│  └─ ComfyUI\models\
│     ├─ checkpoints\             底模放这里
│     └─ loras\                   像素 LoRA 放这里
├─ downloads\                     下载暂存
├─ comfyui.err.log                ComfyUI 运行日志（看这个）
└─ gen.py                         出图脚本（HTTP API）
```

## 启动

```powershell
cmd /c "cd /d D:\SD\ComfyUI_windows_portable && start "" /B python_embeded\python.exe -s ComfyUI\main.py --windows-standalone-build > D:\SD\comfyui.log 2> D:\SD\comfyui.err.log"
```

启动后浏览器打开 **http://127.0.0.1:8188** 就是图形界面。

> ⚠️ 不要用 `Start-Process` 启动：本机 `NO_PROXY` 和 `no_proxy` 两个环境变量重复，PowerShell 会报「已添加项」错误。用 `cmd /c start` 绕开。

## 出图（命令行）

```powershell
python D:\SD\gen.py --prompt "your prompt here" --width 190 --height 268 --out D:\TurnCard\Assets\RetroImages\test.png
```

参数：

| 参数 | 默认 | 说明 |
| --- | --- | --- |
| `--prompt` | 必填 | 正向提示词 |
| `--neg` | 内置 | 负向词（已含 blurry/text/3d 等） |
| `--width` / `--height` | 190 / 268 | 输出尺寸 |
| `--steps` | 28 | 采样步数 |
| `--cfg` | 7.0 | 提示词强度 |
| `--seed` | 12345 | 随机种子 |
| `--lora` | 0.85 | LoRA 权重 |
| `--lora-name` | `PixelArtRedmond.safetensors` | LoRA 文件名 |
| `--ckpt` | `v1-5.safetensors` | 底模文件名 |
| `--out` | `Assets\RetroImages\gen.png` | 输出路径 |

## 配置文件位置

- 底模：`ComfyUI_windows_portable\ComfyUI\models\checkpoints\v1-5.safetensors`
- LoRA：`ComfyUI_windows_portable\ComfyUI\models\loras\PixelArtRedmond.safetensors`

换模型只需把文件放进去，`--ckpt` 传新文件名即可。

## 硬件

- RTX 4080 Laptop，12 GB 显存（ComfyUI 报告可用 11.6 GB）
- 190×268 出图约 1–3 秒
- 笔记本有功耗墙，连续批量出图会降频，建议一次 4 张
