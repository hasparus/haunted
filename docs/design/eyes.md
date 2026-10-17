# Eye colours

The game lights its drawings warm (lamp pool, crack light). Warm light takes blue out, so cyan drifts to green and pink
to red, and the jam bans both colours. Faces get tinted after export.

| Head | Demon | Artist's eyes | In the game | Change |
|---|---|---|---|---|
| Head3 | Anxiety | `#639398` (185°, 35%, 60%) | `#6086c6` (217°, 51%, 78%) | hue +32°, saturation ×1.5, 45% toward white |
| Head2 | Self-doubt | `#f73888` (335°, 77%, 97%) | `#fa4dcf` (315°, 69%, 98%) | hue −20°, saturation ×0.9 |
| Head4 | Anger | orange | unchanged | |
| Head1 | Perfectionism | violet | unchanged | |

## New art from the artist

Export the face layers (`Head{n}Face.png`, `Head{n}SideFace.png`) into `Assets/_Game/Resources/Art/Demon`, then
run this once on the fresh files (it edits in place):

    uv run tools/tint_eyes.py

If the PSD already has the in-game colours above, delete that head from `TINTS` in `tools/tint_eyes.py` instead.

## What to ask the artist for

- No eye hue between 60° and 170° (reads green), none above 340° or below 30° (reads red).
- Cyan or blue eyes at 205–220°, pink or magenta at 305–320°.
- Bright, saturated eyes: brightness 75% or more. Dark, greyish eye colours read green or brown in the game.
