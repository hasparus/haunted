# Right hand

Open `RightHandRigging.unity` in **Edit Mode**.

- **Standby** (left): saved manual pose.
- **Grabbing** (right): same scale and base pose, with thumb–index grip. Middle, ring and pinky retain their standby transforms.
- **ThoughtOrb - game size**: the sphere mesh from `Assets/_Game/Prefabs/Pebble.prefab`, diameter **0.5 world units** (`Pebble.SmallRadius = 0.25`). An opaque Lit preview material keeps physical occlusion with the hand.
- Select any hand object and click **Match Game Camera** at the top of the Inspector to reset the Scene view. It never moves the hand or game camera.
- Both scene Animators are disabled for manual posing. Rotate joints in **Local** space; save the scene.

`RightHand.prefab` enables its Animator: `RightHand.controller` blends `Grip` from 0 (standby) to 1 (holding). `GrabPreview.anim` loops between the captured poses. `GripSocket` marks the held orb centre; the comparison orb is not included in the runtime prefab.

CC0 model: SparrowHawk, *Hands Rigged*. See `CREDITS.txt`. The mesh is manifold, outward-facing and fully weighted, with positive object scales. Gameplay attachment and final stylization remain unfinished.
