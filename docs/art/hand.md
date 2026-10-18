# Host hand sprites

![Opening and closing the same pose](hand-animation.gif)

![Frames at the same pixel scale](hand-contact-sheet.png)

Art only; no scene, prefab, or C# changes.

All three frames use the approved loosely curled hand as one master drawing. Only the thumb and index move slightly. The fingertips stay apart even in `HandClosed.png`; the hand never becomes a fist. Exactly two fingernails are visible, on the thumb and index. The other three fingers remain unchanged.

- `HandOpen.png`: slightly wider thumb/index gap, not a different spread-hand pose.
- `HandGrab.png`: the approved reference pose, with two fingernails.
- `HandClosed.png`: a tighter pinch that still leaves a gap and a transparent opening for the game's orb. No orb is baked in.
- `HandArm.png`: unchanged pale-blue office sleeve, cuff, and bare wrist. Stretch the plain sleeve vertically; the cuff is not a repeating tile.

The hand frames use the same 1024 × 1024 registration canvas, physical scale, and wrist anchor at approximately (512, 8) measured from the top-left. Transparent padding is intentional. The arm is tightly cropped to 342 × 1024, with a bottom-centre pivot. All import at 320 pixels per unit; hand pivots are (0.5, 0.45), alignment 9. Existing GUIDs are unchanged.

Place the arm's bottom wrist at the hand's top-centre wrist, not at the palm pivot. In local Unity units the wrist anchor is approximately (0, 1.7344) relative to the hand pivot. A few pixels of overlap cover the sketchy join.

The linework follows the game's Demon art, but the anatomy and neutral warm-beige skin are human. Fills are flat; skin contains no red or green tint. See `Assets/_Game/Resources/Art/Hand/CREDITS.txt`.

## Rebuild the animation

```sh
uv run --with pillow --with numpy tools/hand_frames.py
```

The default master is the committed `HandGrab.png`. The script applies localized thumb/index deformations to that same image, retaining the wrist, palm, other fingers, and nails. It rebuilds all three registered transparent PNGs, the contact sheet, and a looping 24-frame GIF preview. It leaves the sleeve and Unity metadata untouched. `--add-nails` is only for preparing the original nail-free master, not for normal rebuilds.
