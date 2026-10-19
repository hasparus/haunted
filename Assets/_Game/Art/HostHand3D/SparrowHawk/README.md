# Right hand

## Posing

Open `RightHandRigging.unity` in **Edit Mode**: **Standby** on the left, **Grabbing** on the right. Both Animators stay disabled for manual posing. Select any hand object and click **Match Game Camera** in the Inspector to reset only the Scene view.

**Animation Demo** (rightmost) loops in Edit Mode without pressing Play: ease out while descending to a stationary orb, close, carry, release and rise. Select it for **Play/Pause**, **Restart**, **Scrub**, **Playback Speed** and **Descend Height**. It defaults to half speed, uses a separate rig, and is excluded from builds (`EditorOnly`). The two editable poses are never sampled by the preview. The ink-outlined cloud sits at the original upper forearm cut, not at the wrist. It is a **demo-only mockup**, not applied to gameplay; the normal forearm length is retained.

The saved reference orb is **0.35 units** across. The gameplay prefab enlarges the hand by **1.428571×** to preserve that fit around a normal thought's **0.5-unit** diameter. The authoring scene is not rescaled.

## Gameplay

`RightHand.prefab` contains the skinned rig and `ClawHand`. Its Animator blends `Grip` from 0 (`Standby.anim`) to 1 (`Holding.anim`), including the authored joint translations. `GripSocket` uses the saved reference orb's centre; no orb is included in the prefab.

`Claw` eases out on descent to the target orb. Fingers begin shaping **0.20 seconds into the 0.60-second descent**, reach 80% of their blend progress at contact, then settle into the final grip over 0.12 seconds. Lift still waits 0.18 seconds after contact. Release reopens the hand. Its replicated phase/radius drive presentation on each peer, including late joins. Big and shelled thoughts enlarge the hand proportionally without resizing the thoughts. The hand and its held thought share the pen's foreground depth layer, ahead of desks. `Haunted/Hand` preserves Lit shading and uses skin-weight vertex channels to place the index finger behind the orb and the thumb in front, only near the orb's silhouette. The sphere returns to normal paper sorting on release. Claw-held thoughts suppress the pickup spring; demon pickups retain it.

`RightHandGameplayMesh.asset` extends only the forearm cap upward so the arm continues offscreen; the hand vertices, weights and authoring mesh are unchanged.

Office and Headquarters use the rig; `PrototypeBuilder.BuildClaw` preserves it on rebuild. `GrabPreview.anim` loops between the saved poses. Tests: `ClawHandTests` (Edit Mode), `ClawTests` (Play Mode).

CC0 model: SparrowHawk, *Hands Rigged*. See `CREDITS.txt`. Final stylization remains unfinished.
