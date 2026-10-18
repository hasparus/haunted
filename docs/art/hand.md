# Host hand sprites

![Frames at the same pixel scale](hand-contact-sheet.png)

Art only; no scene, prefab, or C# changes.

- `HandOpen.png`: five spread human fingers, reaching down.
- `HandGrab.png`: thumb and index closing, other fingers curling back.
- `HandClosed.png`: thumb-and-index pinch with a transparent opening for the game's orb. The orb is not baked into the image.
- `HandArm.png`: pale-blue office sleeve, cuff, and bare wrist. Stretch the plain sleeve vertically; the cuff is not a repeating tile.

The three hand frames use the same 1024 × 1024 registration canvas, shared physical scale, and wrist anchor at approximately (512, 8) measured from the top-left. Transparent padding is intentional: independently tight-cropping each pose would change its registration. The arm is tightly cropped to 342 × 1024, with a bottom-centre pivot. All import at 320 pixels per unit; hand pivots are (0.5, 0.45), alignment 9.

Place the arm's bottom wrist at the hand's top-centre wrist, not at the hand's palm pivot. In local Unity units the wrist anchor is approximately (0, 1.7344) relative to each hand's pivot. The slight ink wobble at the join can be covered with a few pixels of overlap.

The linework follows the game's Demon art, but the anatomy and neutral warm-beige skin are human. Fills are flat; skin contains no red or green tint. See `Assets/_Game/Resources/Art/Hand/CREDITS.txt`.

## Cleanup

Run on the original 1024 × 1536 generated 2×2 magenta-key sheet (open, grab, closed, arm):

```sh
uv run --with pillow --with numpy tools/hand_frames.py /path/to/generated-sheet.png
```

The script removes the background and edge contamination, preserves rough ink, flattens the palette, aligns the wrists, writes import metadata, and rebuilds the contact sheet. Existing asset GUIDs are preserved on reruns.
