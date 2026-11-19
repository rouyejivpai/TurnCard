#!/usr/bin/env python3
"""Aseprite -> Unity card-art pipeline.

Exports every .aseprite project under ArtSource/123 as:
  * <Name>_sheet.png  horizontal sprite sheet (all frames, Nx nearest-neighbour upscale)
  * <Name>.png        first frame as a static sprite (card face)
and copies them into Assets/Game/Art/Cards/<Entity>/, plus a manifest that the
Unity Editor importer reads to slice the sheets.

Usage:
  python export_cards.py                # export + copy into Assets
  python export_cards.py --stage-only   # export into Tools/ArtPipeline/_build only
  python export_cards.py --scale 4      # different upscale factor
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = r"D:\TurnCard"
SOURCE_ROOT = os.path.join(ROOT, "ArtSource", "123")
BUILD_ROOT = os.path.join(ROOT, "Tools", "ArtPipeline", "_build")
DEST_ROOT = os.path.join(ROOT, "Assets", "Game", "Art", "Cards")
ASEPRITE = r"D:\Steam\steamapps\common\Aseprite\Aseprite.exe"
MANIFEST_REL = "Assets/Game/Art/Cards/CardArtManifest.json"

# (source path relative to ArtSource/123, entity folder, asset name)
ENTRIES = [
    (r"purple\Farmer_WolfMan\farmer_idle.aseprite", "Farmer", "Farmer_Idle"),
    (r"purple\Farmer_WolfMan\wolfman_idle.aseprite", "Wolfman", "Wolfman_Idle"),
    (r"purple\Whale\whale_1.aseprite", "Whale", "Whale_1"),
    (r"purple\Whale\whale_2.aseprite", "Whale", "Whale_2"),
    (r"purple\贤者1\xianzhe_1.aseprite", "Sage", "Sage_1"),
    (r"purple\贤者1\xianzhe_2.aseprite", "Sage", "Sage_2"),
    (r"Common\Stick\Stick_1.aseprite", "Stick", "Stick_1"),
    (r"Common\Stick\Stick_2.aseprite", "Stick", "Stick_2"),
    (r"Common\剧作之手\The Hand_1.aseprite", "Hand", "Hand_1"),
    (r"Common\剧作之手\The Hand_2.aseprite", "Hand", "Hand_2"),
    (r"Common\Fire gbl\Fire Gbl_1.aseprite", "Fire", "Fire_1"),
    (r"Common\Fire gbl\02.aseprite", "Fire", "Fire_2"),
    (r"Common\sleep gbl\01.aseprite", "Sleep", "Sleep_1"),
    (r"Common\sleep gbl\02.aseprite", "Sleep", "Sleep_2"),
    (r"Common\face\01.aseprite", "Face", "Face_1"),
    (r"Common\face\02.aseprite", "Face", "Face_2"),
    (r"Common\剧作舞者\01.aseprite", "Dancer", "Dancer_1"),
    (r"Common\剧作舞者\02.aseprite", "Dancer", "Dancer_2"),
    (r"Special\Chest\chest_pawn.aseprite", "Chest", "Chest_Pawn"),
    (r"Special\Chest\chest_queen.aseprite", "Chest", "Chest_Queen"),
    (r"Special\Chest\chest_queen2.aseprite", "Chest", "Chest_Queen2"),
]


@dataclass
class Result:
    name: str
    entity: str
    source: str
    ok: bool
    frames: int = 0
    sheet: str = ""
    static: str = ""
    frame_w: int = 0
    frame_h: int = 0
    sheet_w: int = 0
    sheet_h: int = 0
    error: str = ""


def run_aseprite(args: list[str], timeout: int = 90) -> tuple[int, str]:
    """Run Aseprite headless. Aseprite pops a modal dialog on some errors, so never wait forever."""
    proc = subprocess.run(
        [ASEPRITE] + args,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    return proc.returncode, (proc.stdout or "") + (proc.stderr or "")


def png_size(path: str) -> tuple[int, int]:
    from PIL import Image

    with Image.open(path) as im:
        return im.size


def export_one(rel: str, entity: str, name: str, scale: int) -> Result:
    src = os.path.join(SOURCE_ROOT, rel)
    res = Result(name=name, entity=entity, source=rel.replace("\\", "/"), ok=False)
    if not os.path.isfile(src):
        res.error = f"missing source: {src}"
        return res

    out_dir = os.path.join(BUILD_ROOT, entity)
    os.makedirs(out_dir, exist_ok=True)  # must exist, otherwise Aseprite shows a modal dialog
    sheet = os.path.join(out_dir, f"{name}_sheet.png")
    data = os.path.join(out_dir, f"{name}.json")
    static = os.path.join(out_dir, f"{name}.png")

    code, log = run_aseprite([
        "-b", src,
        "--scale", str(scale),
        "--sheet", sheet,
        "--data", data,
        "--format", "json-array",
        "--sheet-type", "horizontal",
    ])
    if code != 0 or not os.path.isfile(sheet) or not os.path.isfile(data):
        res.error = f"sheet export failed (exit {code}): {log.strip()[:400]}"
        return res

    with open(data, encoding="utf-8") as fh:
        meta = json.load(fh)
    frames = meta.get("frames", [])
    if not frames:
        res.error = "metadata has no frames"
        return res

    # Static card-face image = first frame, cropped out of the sheet.
    # (Aseprite's --frame-range 1,1 --save-as yields a blank image on 1-frame sprites, so don't use it.)
    from PIL import Image

    with Image.open(sheet) as im:
        rect = frames[0]["frame"]
        im.crop((rect["x"], rect["y"], rect["x"] + rect["w"], rect["y"] + rect["h"])).save(static)

    res.frames = len(frames)
    res.sheet_w, res.sheet_h = png_size(sheet)
    res.frame_w = frames[0]["frame"]["w"]
    res.frame_h = frames[0]["frame"]["h"]
    res.sheet = sheet
    res.static = static
    res.ok = True
    res.error = ""
    return res


def build_manifest(results: list[Result], scale: int) -> dict:
    assets = []
    for r in results:
        if not r.ok:
            continue
        with open(os.path.join(BUILD_ROOT, r.entity, f"{r.name}.json"), encoding="utf-8") as fh:
            meta = json.load(fh)
        frames = []
        for i, f in enumerate(meta["frames"]):
            rect = f["frame"]
            frames.append({
                "index": i,
                "x": rect["x"], "y": rect["y"], "w": rect["w"], "h": rect["h"],
                "duration": f.get("duration", 100) / 1000.0,
            })
        assets.append({
            "name": r.name,
            "entity": r.entity,
            "source": r.source,
            "sheet": f"Assets/Game/Art/Cards/{r.entity}/{r.name}_sheet.png",
            "staticImage": f"Assets/Game/Art/Cards/{r.entity}/{r.name}.png",
            "frameCount": len(frames),
            "frameWidth": r.frame_w,
            "frameHeight": r.frame_h,
            "frames": frames,
        })
    return {
        "generatedAt": time.strftime("%Y-%m-%d %H:%M:%S"),
        "scale": scale,
        "sourceRoot": "ArtSource/123",
        "assets": assets,
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--scale", type=int, default=2)
    ap.add_argument("--stage-only", action="store_true")
    args = ap.parse_args()

    if not os.path.isfile(ASEPRITE):
        print(f"ERROR: Aseprite not found at {ASEPRITE}")
        return 2

    started = time.time()
    print(f"Aseprite: {ASEPRITE}")
    print(f"scale   : {args.scale}x")
    print(f"sources : {len(ENTRIES)}\n")

    results: list[Result] = []
    for rel, entity, name in ENTRIES:
        try:
            r = export_one(rel, entity, name, args.scale)
        except subprocess.TimeoutExpired:
            r = Result(name=name, entity=entity, source=rel, ok=False, error="aseprite timeout")
        except Exception as exc:  # noqa: BLE001 - report and keep going
            r = Result(name=name, entity=entity, source=rel, ok=False, error=repr(exc))
        results.append(r)
        if r.ok:
            print(f"  OK   {name:<14} {r.frames} frames  {r.frame_w}x{r.frame_h}  sheet {r.sheet_w}x{r.sheet_h}")
        else:
            print(f"  FAIL {name:<14} {r.error}")

    ok = [r for r in results if r.ok]
    print(f"\nexported {len(ok)}/{len(results)} in {time.time() - started:.1f}s")

    manifest = build_manifest(results, args.scale)
    os.makedirs(BUILD_ROOT, exist_ok=True)
    with open(os.path.join(BUILD_ROOT, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=2)

    if args.stage_only:
        print("stage-only: nothing copied into Assets")
        return 0 if len(ok) == len(results) else 1

    copied = 0
    for r in ok:
        dest_dir = os.path.join(DEST_ROOT, r.entity)
        os.makedirs(dest_dir, exist_ok=True)
        for ext in (f"{r.name}_sheet.png", f"{r.name}.png"):
            shutil.copy2(os.path.join(BUILD_ROOT, r.entity, ext), os.path.join(dest_dir, ext))
            copied += 1
    os.makedirs(DEST_ROOT, exist_ok=True)
    with open(os.path.join(ROOT, MANIFEST_REL), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=2)
    print(f"copied {copied} files into {DEST_ROOT}")
    print(f"manifest: {MANIFEST_REL}")
    return 0 if len(ok) == len(results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
