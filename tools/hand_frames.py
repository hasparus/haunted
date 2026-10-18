"""Prepare the host's human hand sprites from a 2x2 image-generation sheet.

Usage: uv run --with pillow --with numpy tools/hand_frames.py /path/to/generated-sheet.png

Cells: open, grabbing, closed thumb/index pinch, sleeved forearm. Removes the
magenta key (including the pinch opening), neutralizes skin, flattens fills,
and preserves the scratchy ink. Hand frames retain a shared registration canvas
instead of independently tight-cropping, which would shift their Unity pivots.
The arm is tightly cropped and stretches vertically; it is not a repeating cuff.
"""
from pathlib import Path
import argparse
import re
import uuid

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/_Game/Resources/Art/Hand"
INK = np.array([20, 18, 26], dtype=float)
SKIN = np.array([217, 210, 197], dtype=float)
SLEEVE = np.array([190, 207, 229], dtype=float)
CUFF = np.array([212, 223, 240], dtype=float)
SIZE = 1024
SCALE = 1.78
FRAMES = [
    ("HandOpen", (0, 0, 512, 768), (273, 85)),
    ("HandGrab", (512, 0, 1024, 768), (267, 85)),
    ("HandClosed", (0, 768, 512, 1536), (274, 72)),
]


def clean(cell, arm=False):
    rgb = np.asarray(cell.convert("RGB"), dtype=float)
    chroma = np.minimum(rgb[..., 0], rgb[..., 2]) - rgb[..., 1]
    alpha = np.clip((65 - chroma) / 55, 0, 1)
    alpha[alpha < 0.04] = 0
    fill = np.broadcast_to(SKIN, rgb.shape).copy()
    if arm:
        fill[:] = SLEEVE
        fill[475:565] = CUFF
        fill[565:] = SKIN
    coverage = np.clip((rgb[..., 1] - 28) / 130, 0, 1)[..., None]
    color = INK + coverage * (fill - INK)
    color[alpha == 0] = 0
    rgba = np.dstack((color, alpha * 255)).round().astype(np.uint8)
    return Image.fromarray(rgba)


def meta(name, image, arm=False):
    path = OUT / f"{name}.png.meta"
    text = (ROOT / "Assets/_Game/Resources/Art/Demon/GripFrontL.png.meta").read_text()
    old = path.read_text() if path.exists() else ""
    match = re.search(r"^guid: ([0-9a-f]{32})$", old, re.M)
    guid = match.group(1) if match else uuid.uuid4().hex
    text = re.sub(r"^guid: .*", f"guid: {guid}", text, count=1, flags=re.M)
    text = text.replace("GripFrontL_0", f"{name}_0")
    text = text.replace("spritePivot: {x: 0.5, y: 0.5}",
                        "spritePivot: {x: 0.5, y: " + ("0" if arm else "0.45") + "}")
    text = re.sub(r"spritePixelsToUnits: \d+", "spritePixelsToUnits: 320", text)
    text = re.sub(r"      alignment: 0\n      pivot: \{x: 0, y: 0\}",
                  "      alignment: 9\n      pivot: {x: 0.5, y: " + ("0" if arm else "0.45") + "}", text)
    text = re.sub(r"        width: \d+", f"        width: {image.width}", text)
    text = re.sub(r"        height: \d+", f"        height: {image.height}", text)
    path.write_text("\n".join(line.rstrip() for line in text.splitlines()) + "\n")


def contact_sheet(images):
    board = Image.new("RGB", (1400, 440), "#b9b6bf")
    draw = ImageDraw.Draw(board)
    for i, (name, image) in enumerate(images):
        x = i * 350
        draw.text((x + 18, 18), name, fill="#14121a")
        for y in range(50, 390, 20):
            for xx in range(x + 10, x + 340, 20):
                if ((xx - x - 10) // 20 + y // 20) % 2:
                    draw.rectangle((xx, y, min(xx + 19, x + 339), y + 19), fill="#cfccd3")
        thumb = image.copy()
        thumb = thumb.resize((round(image.width * 320 / SIZE), 320), Image.Resampling.LANCZOS)
        board.paste(thumb, (x + (350 - thumb.width) // 2, 65), thumb)
        draw.text((x + 18, 410), f"{image.width} x {image.height} / 320 PPU", fill="#14121a")
    path = ROOT / "docs/art/hand-contact-sheet.png"
    path.parent.mkdir(parents=True, exist_ok=True)
    board.save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    args = parser.parse_args()
    source = Image.open(args.source)
    if source.size != (1024, 1536):
        raise ValueError("Expected a 1024 x 1536 2x2 source sheet")
    OUT.mkdir(parents=True, exist_ok=True)
    folder_meta = OUT.with_suffix(".meta")
    if not folder_meta.exists():
        folder_meta.write_text(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
    images = []
    for name, rect, (wx, wy) in FRAMES:
        cell = clean(source.crop(rect))
        frame = cell.transform((SIZE, SIZE), Image.Transform.AFFINE,
                               (1 / SCALE, 0, wx - 512 / SCALE,
                                0, 1 / SCALE, wy - 8 / SCALE),
                               Image.Resampling.BICUBIC)
        frame.save(OUT / f"{name}.png")
        meta(name, frame)
        images.append((name, frame))
    arm = clean(source.crop((512, 768, 1024, 1536)), arm=True)
    arm = arm.crop(arm.getbbox())
    arm = arm.resize((round(arm.width * SCALE), SIZE), Image.Resampling.LANCZOS)
    arm.save(OUT / "HandArm.png")
    meta("HandArm", arm, arm=True)
    images.append(("HandArm", arm))
    contact_sheet(images)
    for name, image in images:
        print(name, image.size, "alpha bounds:", image.getbbox())


if __name__ == "__main__":
    main()
