"""Animate the approved hand pose without redrawing its anatomy.

Usage: uv run --with pillow --with numpy tools/hand_frames.py [master.png]
Add --add-nails only when preparing the original nail-free master. The default
master is HandGrab.png. Only the distal thumb/index move; the wrist, palm and
other three fingers stay registered. The fingertips never meet. Rebuilds the
three transparent PNGs, contact sheet and looping animation preview.
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/_Game/Resources/Art/Hand"
DOCS = ROOT / "docs/art"
INK = (20, 18, 26, 255)
NAIL = (232, 226, 215, 255)
SIZE = 1024
POSES = {"HandOpen": -1.0, "HandGrab": 0.0, "HandClosed": 1.0}


def curve(points):
    result = []
    for i in range(0, len(points) - 1, 3):
        p = np.array(points[i:i + 4], dtype=float)
        for t in np.linspace(0, 1, 24, endpoint=False):
            q = (1-t)**3*p[0] + 3*(1-t)**2*t*p[1] + 3*(1-t)*t*t*p[2] + t**3*p[3]
            result.append(tuple(q))
    return result


def add_nails(master):
    paths = [
        [(300, 789), (300, 782), (314, 772), (322, 775),
         (333, 778), (348, 800), (353, 815),
         (355, 822), (340, 833), (328, 836),
         (317, 834), (302, 808), (300, 789)],
        [(460, 873), (467, 861), (483, 843), (491, 841),
         (503, 842), (523, 851), (527, 860),
         (520, 873), (495, 893), (482, 894),
         (469, 894), (458, 886), (460, 873)],
    ]
    overlay = Image.new("RGBA", (SIZE * 3, SIZE * 3))
    draw = ImageDraw.Draw(overlay)
    for path in paths:
        shape = [(x * 3, y * 3) for x, y in curve(path)]
        draw.polygon(shape, fill=NAIL)
        draw.line(shape + [shape[0]], fill=INK, width=12, joint="curve")
    master = master.copy()
    master.alpha_composite(overlay.resize(master.size, Image.Resampling.LANCZOS))
    return master


def smooth(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def displacement(x, y, pose):
    thumb = smooth((y - 530) / 290) * (1 - smooth((x - 385) / 45))
    index = smooth((y - 650) / 230) * smooth((x - 410) / 50) * (1 - smooth((x - 635) / 65))
    if pose < 0:
        tx, ix, ty, iy = 30 * pose, -20 * pose, 2 * pose, -8 * pose
    else:
        tx, ix, ty, iy = 18 * pose, -12 * pose, 5 * pose, -10 * pose
    return thumb * tx + index * ix, thumb * ty + index * iy


def animate(master, pose):
    if pose == 0:
        return master.copy()
    y, x = np.mgrid[:SIZE, :SIZE].astype(np.float32)
    sx, sy = x.copy(), y.copy()
    for _ in range(6):
        dx, dy = displacement(sx, sy, pose)
        sx, sy = x - dx, y - dy
    xi, yi = np.floor(sx).astype(int), np.floor(sy).astype(int)
    fx, fy = sx - xi, sy - yi
    a = np.asarray(master, dtype=np.float32) / 255
    a[..., :3] *= a[..., 3:4]
    result = np.zeros_like(a)
    for ox, oy, weight in [(0, 0, (1-fx)*(1-fy)), (1, 0, fx*(1-fy)),
                           (0, 1, (1-fx)*fy), (1, 1, fx*fy)]:
        xx, yy = xi + ox, yi + oy
        valid = (xx >= 0) & (xx < SIZE) & (yy >= 0) & (yy < SIZE)
        result[valid] += a[yy[valid], xx[valid]] * weight[valid, None]
    result[..., :3] /= np.maximum(result[..., 3:4], 1e-8)
    output = np.clip(result * 255, 0, 255).round().astype(np.uint8)
    stationary = (np.abs(sx - x) + np.abs(sy - y)) < 1e-6
    output[stationary] = np.asarray(master)[stationary]
    return Image.fromarray(output)


def checker(size):
    image = Image.new("RGB", size, "#b9b6bf")
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 20):
        for x in range(0, size[0], 20):
            if (x // 20 + y // 20) % 2:
                draw.rectangle((x, y, x + 19, y + 19), fill="#cfccd3")
    return image


def previews(master, frames):
    board = Image.new("RGB", (1400, 440), "#b9b6bf")
    draw = ImageDraw.Draw(board)
    for i, (name, image) in enumerate(frames.items()):
        x = i * 350
        board.paste(checker((330, 340)), (x + 10, 50))
        thumb = image.resize((round(image.width * 320 / SIZE), 320), Image.Resampling.LANCZOS)
        board.paste(thumb, (x + (350 - thumb.width) // 2, 65), thumb)
        draw.text((x + 18, 18), name, fill="#14121a")
        draw.text((x + 18, 410), f"{image.width} x {image.height} / 320 PPU", fill="#14121a")
    board.save(DOCS / "hand-contact-sheet.png")
    sequence = list(np.linspace(-1, 1, 13)) + list(np.linspace(1, -1, 13))[1:-1]
    gif = []
    for pose in sequence:
        frame = checker((512, 512))
        sprite = animate(master, pose).resize((512, 512), Image.Resampling.LANCZOS)
        frame.paste(sprite, (0, 0), sprite)
        gif.append(frame)
    palette = gif[0].quantize(colors=96)
    gif = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in gif]
    gif[0].save(DOCS / "hand-animation.gif", save_all=True, append_images=gif[1:],
                duration=75, loop=0, disposal=2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("master", nargs="?", type=Path, default=OUT / "HandGrab.png")
    parser.add_argument("--add-nails", action="store_true")
    args = parser.parse_args()
    master = Image.open(args.master).convert("RGBA")
    if master.size != (SIZE, SIZE):
        raise ValueError("The registered master must be 1024 x 1024")
    if args.add_nails:
        master = add_nails(master)
    frames = {}
    for name, pose in POSES.items():
        frame = animate(master, pose)
        frame.save(OUT / f"{name}.png")
        frames[name] = frame
    frames["HandArm"] = Image.open(OUT / "HandArm.png").convert("RGBA")
    previews(master, frames)
    print("Three registered frames, exactly two visible nails, fingertips remain separated.")


if __name__ == "__main__":
    main()
