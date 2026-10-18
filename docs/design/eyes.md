# Eye colours

The game lights its drawings warm (lamp pool, crack light). Warm light takes blue out, so cyan drifts to green and pink
to red, and the jam bans both colours.

Eyes as the artist draws them now (second PSD), measured in the files and in the game (HQ wardrobe):

| Head | Demon | In her PSD | In the game | Change |
|---|---|---|---|---|
| Head3 | Anxiety | `#4f7fac` (209°, 54%, 67%) | `#5980c1` (217°, 54%, 76%) | hue +8°, 25% toward white |
| Head2 | Self-doubt | `#d238f5` (288°, 77%, 96%) | renders 289°, purple-pink | none |
| Head4 | Anger | `#d96f2c` (21°, 80%, 85%) | renders 23°, orange | none |
| Head1 | Perfectionism | `#8446ee` (270°, 67%, 93%) | renders 263°, violet | none |

## New art from the artist

    uv run tools/demon_psd.py WoF_Character.psd some/folder

then Demons > Import Demon Art... in Unity, pick that folder, and run `uv run tools/tint_eyes.py` once on the fresh
faces. If her Anxiety blue already sits at 215° or more, take Head3 out of `TINTS` in `tools/tint_eyes.py` instead.

## What to ask the artist for

- No eye hue between 60° and 170° (reads green), none above 340° or below 30° (reads red).
- Cyan or blue eyes at 215–220°, pink or magenta at 285–320°.
- Bright, saturated eyes: brightness 75% or more. Dark, greyish eye colours read green or brown in the game.
