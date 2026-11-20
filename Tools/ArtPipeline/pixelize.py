#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把 AI 生成的“伪像素图”还原成真像素图：先测伪像素网格，再按网格中心抽点。

为什么不直接 resize：
  AI 图虽然看起来是像素块，但块边长通常不是整数倍、也不是从 (0,0) 开始，
  直接插值缩放会产生半像素错位，边缘长出一圈中间色。本脚本先求网格周期和相位，
  再按“块中心”取样，一块只留一个像素。

用法：
    python pixelize.py 图片.png
    python pixelize.py 图片.png --factors 2,3,4,5,6,8 --out-dir ../out
    python pixelize.py 图片.png --factor 5 --palette 32 --despeckle

输出：
    <原名>_px{f}x_{w}x{h}.png      每个倍率一张，等比缩放
    <原名>_pxgrid_report.txt       网格检测与各倍率的质量报告（--report 时生成）
"""

from __future__ import annotations

import argparse
import os
import sys

import numpy as np
from PIL import Image


# ---------------------------------------------------------------- 网格检测

def _edge_profile(gray: np.ndarray):
    """返回 (列边缘能量, 行边缘能量)，索引 x 表示 x 与 x+1 之间的边界。"""
    gx = np.abs(np.diff(gray, axis=1)).mean(axis=0)  # 长度 w-1
    gy = np.abs(np.diff(gray, axis=0)).mean(axis=1)  # 长度 h-1
    return gx, gy


def _phase_concentration(profile: np.ndarray, factor: int) -> tuple[float, int]:
    """
    把边缘能量按 factor 取模分组：真像素图的边缘几乎全部落在同一个相位上。
    返回 (归一化集中度, 最佳相位)。1.0 表示所有边缘都在同一相位，1/factor 表示随机。
    """
    total = float(profile.sum())
    if total <= 1e-9:
        return 0.0, 0
    bins = np.zeros(factor)
    np.add.at(bins, np.arange(profile.size) % factor, profile)
    phase = int(np.argmax(bins))
    # 乘 factor 做归一化：随机分布时结果约为 1，理想网格远大于 1
    return float(bins[phase] / total * factor), phase


def detect_grid(image: Image.Image, max_factor: int = 16) -> dict:
    """
    用梯度自相关估计伪像素块边长。

    AI 图往往没有严格整数网格（块内还有渐变和抗锯齿），所以这里给的是“最可信的近似周期”：
    取自相关在 2..max_factor 内的第一个显著峰，并优先选小周期——
    period=5 的整数倍（10、15）同样会出现峰，若不做这层约束会误判成 15。
    """
    gray = np.array(image.convert("L"), dtype=np.float32)
    gx, gy = _edge_profile(gray)

    def autocorr_peak(profile: np.ndarray) -> tuple[float, int]:
        p = profile - profile.mean()
        if not np.any(p):
            return 0.0, 1
        ac = np.correlate(p, p, mode="full")[profile.size - 1:]
        ac = ac / ac[0]
        peaks = []
        for lag in range(2, min(max_factor, ac.size - 1) + 1):
            if ac[lag] > ac[lag - 1] and ac[lag] >= ac[lag + 1]:
                peaks.append((float(ac[lag]), lag))
        if not peaks:
            return 0.0, 1
        best_value = max(v for v, _ in peaks)
        candidates = [lag for v, lag in peaks if v >= best_value * 0.8]
        return best_value, min(candidates)

    vx, fx = autocorr_peak(gx)
    vy, fy = autocorr_peak(gy)
    factor = min(fx, fy) if min(vx, vy) > 0.15 else 1
    phase_x = _phase_concentration(gx, factor)[1] if factor > 1 else 0
    phase_y = _phase_concentration(gy, factor)[1] if factor > 1 else 0

    return {"factor": factor, "score": min(vx, vy), "phase_x": phase_x, "phase_y": phase_y,
            "cx": 1.0, "cy": 1.0}


# ---------------------------------------------------------------- 抽点

def decimate(image: Image.Image, factor: int, phase_x: int = 0, phase_y: int = 0) -> Image.Image:
    """按 factor 取块中心像素。相位用于对齐非 (0,0) 起点的网格。"""
    a = np.array(image.convert("RGBA"))
    h, w = a.shape[:2]
    if factor <= 1:
        return Image.fromarray(a, "RGBA")

    half = factor // 2
    ys = range(phase_y + half, h, factor)
    xs = range(phase_x + half, w, factor)
    out = a[np.ix_(list(ys), list(xs))]
    return Image.fromarray(np.ascontiguousarray(out), "RGBA")


def proportional_size(w: int, h: int, factor: int) -> tuple[int, int]:
    """等比缩放：先定宽，再按原图宽高比推高，避免两个方向各自取整导致比例漂移。"""
    tw = max(1, round(w / factor))
    th = max(1, round(tw * h / w))
    return tw, th


# ---------------------------------------------------------------- 清理

def despeckle(image: Image.Image, passes: int = 1, neighbour_min: int = 6) -> Image.Image:
    """
    3×3 众数滤波：只有当一个像素与四周完全异色、且周围有 >= neighbour_min 个同色邻居时才替换。
    阈值设得高，避免把眼睛、铆钉这类单像素细节抹掉。
    """
    arr = np.array(image.convert("RGBA"))
    h, w = arr.shape[:2]
    for _ in range(passes):
        pad = np.pad(arr, ((1, 1), (1, 1), (0, 0)), mode="edge")
        stack = np.stack([pad[dy:dy + h, dx:dx + w] for dy in range(3) for dx in range(3)])
        out = arr.copy()
        for y in range(h):
            for x in range(w):
                colours, counts = np.unique(stack[:, y, x], axis=0, return_counts=True)
                if counts.max() >= neighbour_min and counts[counts.argmax()] > counts[0] + 1:
                    out[y, x] = colours[counts.argmax()]
        arr = out
    return Image.fromarray(arr, "RGBA")


def limit_palette(image: Image.Image, colours: int) -> Image.Image:
    """中位切分量化，关抖动。透明像素不参与取色。"""
    rgba = image.convert("RGBA")
    alpha = np.array(rgba)[:, :, 3]
    rgb = rgba.convert("RGB").quantize(colors=colours, method=Image.MEDIANCUT,
                                       dither=Image.Dither.NONE).convert("RGBA")
    rgb.putalpha(Image.fromarray(alpha))
    return rgb


# ---------------------------------------------------------------- 质量指标

def measure(image: Image.Image) -> dict:
    a = np.array(image.convert("RGBA"))
    opaque = a[:, :, 3] > 8
    rgb = a[:, :, :3]

    colours = len(np.unique(rgb[opaque].reshape(-1, 3), axis=0)) if opaque.any() else 0

    # 4 邻域异色率：反映“边缘清晰度”。真像素图通常 0.15~0.35；
    # 太小 = 糊成一片，太大 = 噪点多。
    diff_h = np.any(rgb[:, 1:] != rgb[:, :-1], axis=2)
    diff_v = np.any(rgb[1:] != rgb[:-1], axis=2)
    edge = (diff_h.mean() + diff_v.mean()) / 2

    # 孤立像素率：与 8 邻域全部异色的像素占比
    h, w = rgb.shape[:2]
    if h > 2 and w > 2:
        pad = np.pad(rgb, ((1, 1), (1, 1), (0, 0)), mode="edge")
        same = np.zeros((h, w), dtype=int)
        for dy in range(3):
            for dx in range(3):
                if dy == 1 and dx == 1:
                    continue
                same += np.all(pad[dy:dy + h, dx:dx + w] == rgb, axis=2)
        orphan = float((same == 0).mean())
    else:
        orphan = 0.0

    return {"colors": colours, "edge": float(edge), "orphan": orphan}


def reconstruction_error(source: Image.Image, candidate: Image.Image) -> float:
    """把候选图用最近邻放回原尺寸，与源图比较的平均绝对误差（0-255）。"""
    src = np.array(source.convert("RGBA"), dtype=np.float32)
    up = np.array(candidate.convert("RGBA").resize(source.size, Image.NEAREST), dtype=np.float32)
    return float(np.abs(src - up).mean())


# ---------------------------------------------------------------- 主流程

def run(src: str, factors: list[int], out_dir: str, palette: int | None,
        do_despeckle: bool, make_report: bool) -> list[dict]:
    image = Image.open(src).convert("RGBA")
    w, h = image.size
    stem = os.path.splitext(os.path.basename(src))[0]
    out_dir = out_dir or os.path.dirname(os.path.abspath(src))
    os.makedirs(out_dir, exist_ok=True)

    grid = detect_grid(image)
    print(f"源图 {os.path.basename(src)}  {w}×{h}")
    print(f"检测到伪像素网格: {grid['factor']}px  相位=({grid['phase_x']},{grid['phase_y']})  "
          f"集中度=({grid['cx']:.2f},{grid['cy']:.2f})")

    results = []
    for factor in sorted(set(factors)):
        # 用检测到的相位；若倍率与检测值相同才是精确对齐，其余倍率按 0 相位取样
        px = grid["phase_x"] % factor
        py = grid["phase_y"] % factor
        small = decimate(image, factor, px, py)

        if do_despeckle:
            small = despeckle(small, passes=1)
        if palette:
            small = limit_palette(small, palette)

        tw, th = proportional_size(w, h, factor)
        if small.size != (tw, th):
            small = small.resize((tw, th), Image.NEAREST)

        name = f"{stem}_px{factor}x_{tw}x{th}.png"
        path = os.path.join(out_dir, name)
        small.save(path)

        m = measure(small)
        err = reconstruction_error(image, small)
        results.append({"factor": factor, "size": f"{tw}x{th}", "path": path,
                        "colors": m["colors"], "edge": m["edge"],
                        "orphan": m["orphan"], "error": err,
                        "grid_ok": factor == grid["factor"]})
        print(f"  ÷{factor:<3} {tw:>4}×{th:<4} 色数 {m['colors']:>6}  "
              f"边缘率 {m['edge']:.3f}  孤点率 {m['orphan']:.4f}  重建误差 {err:6.2f}")

    if make_report:
        report = os.path.join(out_dir, f"{stem}_pxgrid_report.txt")
        with open(report, "w", encoding="utf-8") as fh:
            fh.write(f"source\t{src}\nsize\t{w}x{h}\n")
            fh.write(f"grid\t{grid['factor']}\tphase\t{grid['phase_x']},{grid['phase_y']}\n")
            fh.write("factor\tsize\tcolors\tedge\torphan\terror\tgrid_aligned\n")
            for r in results:
                fh.write(f"{r['factor']}\t{r['size']}\t{r['colors']}\t{r['edge']:.4f}\t"
                         f"{r['orphan']:.5f}\t{r['error']:.3f}\t{r['grid_ok']}\n")
        print(f"报告 -> {report}")

    return results


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="AI 伪像素图 -> 真像素图（网格检测 + 中心抽点）")
    ap.add_argument("src", help="源图路径")
    ap.add_argument("--factors", default="2,3,4,5,6,8",
                    help="要输出的缩放倍率，逗号分隔（默认 2,3,4,5,6,8）")
    ap.add_argument("--factor", type=int, default=None, help="只输出一个倍率")
    ap.add_argument("--out-dir", default=None, help="输出目录（默认与源图同目录）")
    ap.add_argument("--palette", type=int, default=None, help="限制颜色数，不填则不限制")
    ap.add_argument("--despeckle", action="store_true", help="开启 3×3 众数滤波去孤点")
    ap.add_argument("--report", action="store_true", help="输出质量报告 txt")
    args = ap.parse_args(argv)

    if not os.path.isfile(args.src):
        print(f"找不到文件: {args.src}", file=sys.stderr)
        return 2

    factors = [args.factor] if args.factor else [int(x) for x in args.factors.split(",") if x.strip()]
    run(args.src, factors, args.out_dir, args.palette, args.despeckle, args.report)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
