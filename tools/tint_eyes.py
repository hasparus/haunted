# /// script
# dependencies = ["numpy", "pillow"]
# ///
"""Tints the demons' eyes so they keep their colour under the game's warm light (docs/design/eyes.md).

    uv run tools/tint_eyes.py [folder]   # default Assets/_Game/Resources/Art/Demon

Run it on the artist's untouched face layers, once: it edits the files in place.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

# File stem prefix -> (hues to take in degrees, turn by degrees, saturation scale, lift toward white 0..1).
TINTS = {
    "Head3": ((150, 230), 32, 1.5, 0.45),  # Anxiety: cyan leans blue and brighter, or it reads green
    "Head2": ((280, 360), -20, 0.9, 0.35),  # Self-doubt: pink leans purple and lighter, or it reads red
}


def tint(path: Path, hues, turn, saturation, lift):
    im = Image.open(path).convert("RGBA")
    alpha = np.asarray(im)[..., 3]
    hsv = np.asarray(im.convert("RGB").convert("HSV")).astype(float) / 255
    h, s, v = hsv[..., 0] * 360, hsv[..., 1], hsv[..., 2]
    lo, hi = hues
    # Only coloured pixels (ink and white stay put), fading in with saturation so edges don't band.
    take = ((h >= lo) & (h <= hi)) * np.clip((s - 0.06) / 0.15, 0, 1)
    h2 = (h + turn) % 360
    s2 = np.clip(s * saturation, 0, 1)
    v2 = v + (1 - v) * lift
    out = np.stack([np.where(take > 0, h2, h) / 360, s + (s2 - s) * take, v + (v2 - v) * take], -1)
    rgb = Image.fromarray((out * 255).round().astype(np.uint8), "HSV").convert("RGB")
    rgb.putalpha(Image.fromarray(alpha))
    rgb.save(path)


def main():
    folder = Path(sys.argv[1] if len(sys.argv) > 1 else "Assets/_Game/Resources/Art/Demon")
    for head, args in TINTS.items():
        for face in ("Face", "SideFace"):
            path = folder / f"{head}{face}.png"
            tint(path, *args)
            print("tinted", path)


if __name__ == "__main__":
    main()
