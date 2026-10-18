#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.12"
# dependencies = [
#     "pillow>=11,<13",
# ]
# ///
"""Fit eye photos and preview their luminance through the game's unchanged mask.

uv run tools/eye_photos.py mask --width 1536 --height 1024 --output /tmp/eye-guides
uv run tools/eye_photos.py fit source.png --day 0
uv run tools/eye_photos.py preview Assets/_Game/Resources/Eyes/day0.png --output tools/eye_previews/day0.png

The mask command centers a 2.5:1 viewport in the requested canvas. Inpainting
uses transparent=paint, opaque=preserve, with a 5%-of-eye-height margin.
"""
from pathlib import Path
import argparse

from PIL import Image, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parents[1]
EYES = ROOT / "Assets/_Game/Resources/Eyes"
MASK = EYES / "Mask.png"
RESAMPLE = Image.Resampling.LANCZOS


def save(image, path):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)
    print(path)


def luminance(image):
    return image.convert("RGB").convert("L", (0.299, 0.587, 0.114, 0))


def preview(photo, output, mask_path=MASK):
    with Image.open(mask_path) as mask, Image.open(photo) as source:
        grey = luminance(ImageOps.fit(source.convert("RGB"), mask.size, method=RESAMPLE))
        alpha = mask.getchannel("A")
        save(Image.composite(grey, Image.new("L", mask.size), alpha).convert("RGB"), output)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    fit = sub.add_parser("fit", help="Crop source to 2.5:1 and write photo plus preview")
    fit.add_argument("source", type=Path)
    fit.add_argument("--day", type=int, choices=range(7), required=True)
    fit.add_argument("--width", type=int, default=2560)
    fit.add_argument("--center-x", type=float, default=0.5)
    fit.add_argument("--center-y", type=float, default=0.5)
    fit.add_argument("--crop", type=int, nargs=4, metavar=("LEFT", "TOP", "RIGHT", "BOTTOM"))
    prev = sub.add_parser("preview")
    prev.add_argument("source", type=Path)
    prev.add_argument("--output", type=Path, required=True)
    prev.add_argument("--mask", type=Path, default=MASK)
    mask_parser = sub.add_parser("mask", help="Write guide and OpenAI-format inpainting mask")
    mask_parser.add_argument("--width", type=int, default=2000)
    mask_parser.add_argument("--height", type=int, default=800)
    mask_parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.command == "preview":
        preview(args.source, args.output, args.mask)
    elif args.command == "fit":
        if args.width < 2000 or args.width % 5:
            parser.error("--width must be at least 2000 and divisible by 5")
        if not (0 <= args.center_x <= 1 and 0 <= args.center_y <= 1):
            parser.error("centering must be between 0 and 1")
        with Image.open(args.source) as source:
            source = ImageOps.exif_transpose(source).convert("RGB")
            if args.crop:
                l, t, r, b = args.crop
                if not (0 <= l < r <= source.width and 0 <= t < b <= source.height):
                    parser.error("--crop must lie inside the source")
                source = source.crop(args.crop)
            fitted = ImageOps.fit(source, (args.width, args.width * 2 // 5), method=RESAMPLE,
                                  centering=(args.center_x, args.center_y))
        destination = EYES / f"day{args.day}.png"
        save(fitted, destination)
        preview(destination, ROOT / f"tools/eye_previews/day{args.day}.png")
    else:
        if args.width < 1 or args.height < 1:
            parser.error("dimensions must be positive")
        vw = min(args.width, round(args.height * 2.5))
        vh = min(args.height, round(vw / 2.5))
        left, top = (args.width - vw) // 2, (args.height - vh) // 2
        with Image.open(MASK) as source:
            eyes = source.getchannel("A").resize((vw, vh), RESAMPLE)
        guide = Image.new("L", (args.width, args.height), 96)
        guide.paste(eyes, (left, top))
        save(guide, args.output / "guide.png")
        paint = Image.new("L", (args.width, args.height))
        paint.paste(eyes.point(lambda x: 255 if x > 0 else 0), (left, top))
        radius = max(1, round(vh * 0.05))
        paint = paint.filter(ImageFilter.MaxFilter(2 * radius + 1))
        inpaint = Image.new("RGBA", paint.size, (128, 128, 128, 255))
        inpaint.putalpha(ImageOps.invert(paint))
        save(inpaint, args.output / "inpaint.png")
        print(f"Viewport: {left},{top},{left + vw},{top + vh}")


if __name__ == "__main__":
    main()
