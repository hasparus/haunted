# /// script
# dependencies = ["numpy", "pillow", "psd-tools"]
# ///
"""Cuts the artist's demon PSD into the game's sprites (Assets/_Game/Resources/Art/Demon) and writes pivots.json next to
them: body parts pivot at the canvas feet point (256, 760); side heads (and their faces) are scaled about the chin to
their front's height; arms pivot at the shoulder; the orb's swirl is kept as ink only.

    uv run tools/demon_psd.py WoF_Character.psd some/folder

Then in Unity: Demons > Import Demon Art..., pick that folder. Check the eyes in the game (docs/design/eyes.md).
"""
import json
import sys

import numpy as np
from PIL import Image
from psd_tools import PSDImage

psd = PSDImage.open(sys.argv[1])
O = sys.argv[2]
# Every layer on the full canvas, named group/layer (a repeated name gets #1, #2: the PSD has two Head_02_Front).
seen, info, layers = {}, {}, {}
for g in psd:
    if not g.is_group() or g.name == 'BG': continue
    for l in g:
        if l.bbox[2] == 0: continue
        name = f"{g.name}/{l.name}"
        k = seen.get(name, 0); seen[name] = k + 1
        if k: name += f"#{k}"
        canvas = Image.new('RGBA', psd.size, (0, 0, 0, 0))
        canvas.alpha_composite(l.topil().convert('RGBA'), (l.bbox[0], l.bbox[1]))
        a = np.asarray(canvas)[..., 3]
        ys, xs = np.nonzero(a > 40)
        box = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
        layers[name] = canvas
        info[name] = {"box": box}

FEET = (256, 760)
pivots, boxes = {}, {}

def layer(name): return layers[name].copy()

def bbox(im):
    a = np.asarray(im)[..., 3]; ys, xs = np.nonzero(a > 40)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1

def save(im, out, pivot=FEET):
    x0, y0, x1, y1 = bbox(im)
    im.crop((x0, y0, x1, y1)).save(f"{O}/{out}.png")
    pivots[out] = [round((pivot[0] - x0) / (x1 - x0), 5), round((y1 - pivot[1]) / (y1 - y0), 5)]
    boxes[out] = [x0, y0, x1, y1]

def scaled(im, f, about):
    # Scale the canvas content by f about a point, keeping the canvas frame (and so the feet point).
    w, h = im.size
    big = im.resize((round(w * f), round(h * f)), Image.LANCZOS)
    canvas = Image.new('RGBA', (w * 2, h * 2), (0, 0, 0, 0))
    canvas.paste(big, (round(about[0] * (1 - f)) + w // 2, round(about[1] * (1 - f)) + h // 2), big)
    return canvas.crop((w // 2, h // 2, w // 2 + w, h // 2 + h))

# Profile legs; front ones and the body haven't changed since the first PSD.
for n in range(1, 5):
    save(layer(f"Legs_Walking_Side/Legs_Walking_Side_0{n}"), f"LegsSideWalk{n}")
save(layer("Legs_Standing/Legs_Standing_Side"), "LegsSideStand")

# Heads: the side drawings are a little shorter than the fronts, so they're scaled about the chin to match.
fronts = {1: "Heads/Head_01_Front", 2: "Heads/Head_02_Front", 3: "Heads/Head_02_Front#1", 4: "Heads/Head_04_Front"}
sideFaces = {1: "Heads/Layer 3", 2: "Heads/Head_02_Side_Face", 3: "Heads/Head_03_Side_Face", 4: "Heads/Head_04_Side_Face"}
for n in range(1, 5):
    front = info[fronts[n]]["box"]; side = layer(f"Heads/Head_0{n}_Side")
    sb = bbox(side)
    f = (front[3] - front[1]) / (sb[3] - sb[1])
    chin = ((sb[0] + sb[2]) / 2, sb[3])
    save(scaled(side, f, chin), f"Head{n}Side")
    save(scaled(layer(sideFaces[n]), f, chin), f"Head{n}SideFace")
    print(f"Head{n}Side x{f:.3f}")
backs = {1: "Heads/Head_01_Back", 2: "Heads/Head_02_Back", 3: "Heads/Head_03_Back", 4: "Heads/Head_04_Back"}
faces = {1: "Heads/Head_01_Front_Face", 2: "Heads/Head_02_Front_Face", 3: "Heads/Head_03_Front_Face", 4: "Heads/Head_04_Front_Face"}
for n in range(1, 5):
    save(layer(fronts[n]), f"Head{n}Front")
    save(layer(backs[n]), f"Head{n}Back")
    save(layer(faces[n]), f"Head{n}Face")

# Arms: idle ones pivot at the shoulder (their top) so the game can swing them; a layer holding two is split down
# the middle. Gripping hands pivot at their middle. Where each pivot sits is recorded in metres from the feet.
hands = {}
def cut(layerName, out, pivotAtTop):
    im = layer(layerName); x0, y0, x1, y1 = bbox(im); arr = np.asarray(im)
    a = arr[..., 3] > 40
    cols = a.sum(0); mid = x0 + int(np.argmin(cols[x0 + (x1 - x0) // 3: x1 - (x1 - x0) // 3])) + (x1 - x0) // 3
    two = cols[mid] < 3 and (x1 - x0) > 90
    for side, (lo, hi) in (("L", (0, mid)), ("R", (mid, arr.shape[1]))) if two else (("", (0, arr.shape[1])),):
        part = arr.copy(); part[:, :lo] = 0; part[:, hi:] = 0
        part = Image.fromarray(part); px0, py0, px1, py1 = bbox(part)
        if pivotAtTop:
            top = np.nonzero(np.asarray(part)[py0:py0 + 6, :, 3].max(0) > 40)[0]
            c = (float(top.mean()), py0)
        else:
            c = ((px0 + px1) / 2, (py0 + py1) / 2)
        save(part, out + side, c)
        hands[out + side] = [round((c[0] - FEET[0]) / 320, 3), round((FEET[1] - c[1]) / 320, 3)]
    print(layerName, "split" if two else "one")
cut("Hands_Idle/Hands_Idle_Front", "ArmFront", True)
cut("Hands_Idle/Hands_Idle_Back", "ArmBack", True)
cut("Hands_Idle/Hands_Idle_Side", "ArmSide", True)
cut("Hands Holding/Hands_Holding_Orb_Front", "GripFront", False)
cut("Hands Holding/Hands_Holding_Orb_Back", "GripBack", False)
cut("Hands Holding/Hands_Holding_Orb_Side", "GripSide", False)

# The orb's swirl, ink only and inside the rim (our sphere draws its own outline), and the wings.
orb = layer("Orb/Orb"); ob = bbox(orb); oc = ((ob[0] + ob[2]) / 2, (ob[1] + ob[3]) / 2); r = (ob[2] - ob[0]) / 2
arr = np.asarray(orb).astype(float)
lum = arr[..., :3].mean(-1) / 255
yy, xx = np.mgrid[0:arr.shape[0], 0:arr.shape[1]]
inside = np.hypot(xx - oc[0], yy - oc[1]) < r * 0.86
ink = np.clip((0.6 - lum) / 0.4, 0, 1) * (arr[..., 3] / 255) * inside
swirl = np.zeros_like(arr); swirl[..., :3] = 20; swirl[..., 3] = ink * 255
# The swirl spans the whole orb, so the shader can map the ball's facing straight onto it.
Image.fromarray(swirl.astype(np.uint8)).crop((ob[0], ob[1], ob[2], ob[3])).save(f"{O}/OrbSwirl.png")
pivots["OrbSwirl"] = [0.5, 0.5]
for side in ("L", "R"):
    w = layer(f"Orb/Orb_Wing_{side}"); wb = bbox(w)
    root = (wb[2] if side == "L" else wb[0], (wb[1] + wb[3]) / 2)
    save(w, f"OrbWing{side}", root)
    hands[f"OrbWing{side}"] = [round((root[0] - oc[0]) / (2 * r), 3), round((oc[1] - root[1]) / (2 * r), 3)]
hands["OrbDiameterPx"] = 2 * r
json.dump({"pivots": pivots, "boxes": boxes, "places": hands}, open(f"{O}/pivots.json", "w"), indent=1)
print(json.dumps(hands))
