# Right hand

## Posing

Open `RightHandRigging.unity` in **Edit Mode**: **Standby** on the left, **Grabbing** on the right. Both Animators stay disabled for manual posing. Select any hand object and click **Match Game Camera** in the Inspector to reset only the Scene view.

The saved reference orb is **0.35 units** across. The gameplay prefab enlarges the hand by **1.428571×** to preserve that fit around a normal thought's **0.5-unit** diameter. The authoring scene is not rescaled.

## Gameplay

`RightHand.prefab` contains the skinned rig and `ClawHand`. Its Animator blends `Grip` from 0 (`Standby.anim`) to 1 (`Holding.anim`), including the authored joint translations. `GripSocket` uses the saved reference orb's centre; no orb is included in the prefab.

`Claw` descends to the target orb, closes over **0.18 seconds**, then lifts. Release reopens the hand. Its replicated phase/radius drive presentation on each peer, including late joins. Big and shelled thoughts enlarge the hand proportionally without resizing the thoughts. Claw-held thoughts use physical depth so the sphere interleaves with the 3D fingers, then return to paper sorting on release.

`RightHandGameplayMesh.asset` extends only the forearm cap upward so the arm continues offscreen; the hand vertices, weights and authoring mesh are unchanged.

Office and Headquarters use the rig; `PrototypeBuilder.BuildClaw` preserves it on rebuild. `GrabPreview.anim` loops between the saved poses. Tests: `ClawHandTests` (Edit Mode), `ClawTests` (Play Mode).

CC0 model: SparrowHawk, *Hands Rigged*. See `CREDITS.txt`. Final stylization remains unfinished.
