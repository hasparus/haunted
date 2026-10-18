# 3D hand rigging

Open `HostHandRigging.unity` in **Edit Mode**.

- Your edited pose is preserved, mirrored horizontally, and oriented toward the 48° player-camera view.
- Use `HostHandPlayerFacing.prefab` for that orientation.
- Animator is disabled so it cannot overwrite manual posing.
- Expand `PlayerFacing - mirrored X` → hand → `Orientation/Model/HostHandRig/hand_r`.
- Rotate `thumb_*` and `index_*` joints in **Local** space. Keep `middle_*`, `ring_*`, `pinky_*` curled inside the opening.
- Move the reference orb independently. Save the scene; do not reset transforms.
- Labels are removed. Select joints through the Hierarchy.

`HostHand.prefab`, clips, controller, and `HostHandRigLab.unity` are earlier animation experiments. Gameplay wiring and final stylization remain unfinished; no gameplay C# changes are included.

Model: CC0, Innerscene / MakeHuman. Details in `CREDITS.txt`.
