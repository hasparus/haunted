# Making the grasp feel natural

## Diagnosis

The reported problems have concrete causes. The initial audit was read-only; subsequent asset corrections are recorded below. Authored scene poses and cloud placement remain untouched.

### The fingers start late by design

Gameplay keeps `Grip = 0` throughout the 0.60-second descent and closes only after entering Carrying. Closing then takes 0.18 seconds. The cubic ease-out descent covers 87.5% of the distance halfway through its duration, and 98.44% after three quarters; the fingers are still idle at both points.

The editor demo does the same: it samples the open pose throughout descent, then starts closing at contact. At its default half speed, the viewer watches a 1.20-second descent before any curl begins.

Anchors: `Assets/_Game/Scripts/Claw.cs` (`Update`, `Tick`, `Duration`); `ClawHand.cs` (`Present`, `GrabTime`, `EaseDescent`); `HandGrabPreview.cs` (`Sample`).

### The preview has a genuine endpoint jump

`HandGrabPreview.Sample` samples `GrabPreview.anim` from 0.45 to 0.63 seconds while closing, then switches directly to time 1.0 while carrying. The clip's matching Holding keys do not form a flat interval: quaternion-component tangents create motion between them.

Unity sampling on a temporary, isolated model confirmed these changes across that single switch:

| Transform | Rotation jump, 0.63 → 1.0 seconds |
|---|---:|
| Thumb middle joint (`thumb_02_r`) | 12.26° |
| Index middle joint (`index_02_r`) | 7.72° |
| Mesh object (`RightHandMesh`) | 8.80° |
| Wrist joint (`hand_r`) | 2.30° |

The release switch, 1.0 → 1.4, reverses the same differences. This is a preview defect, not a reason to alter the approved Holding pose.

Gameplay uses a different evaluator: constant Standby/Holding clips blended by `SmoothStep(Grip)`. Its normal pickup has no equivalent explicit sample-time jump. The short, synchronized 0.18-second curl can still *look* abrupt. Quaternion sign reversal is not supported by the asset inspection; this is not a demonstrated 180-degree interpolation problem.

## Follow-up: latest Standby and contact clearance

A subsequent audit confirmed that the baked Standby was stale: the live rig differed by 16.46° at `index_01_r`, 14.89° at `thumb_03_r`, and 3.55° at `index_meta_r`. The live pose was backed up before resynchronizing `Standby.anim`, the preview and the prefab. A repeat audit reported no remaining pose differences; Holding and the cloud placement were unchanged.

The preview was regenerated with flat Holding tangents. Unity sampling then reported no rotational jumps above 0.1° across the former 0.63 → 1.0 → 1.4 switches. The diagnosis above records the earlier, defective version.

With the latest Standby, an isolated skinned-vertex audit found no index penetration during descent: minimum clearance at contact was 0.0394 preview units. The final grip still puts 17 index vertices just inside the sphere, to a maximum depth of 0.00808 units (reference sphere radius 0.175). These are geometric measurements, not claims that the contact silhouette is already satisfactory. Contact clearance still needs review while implementing earlier closure; do not compensate by moving the orb or introducing an attachment jump.

### Follow-up: Holding endpoint audit

The Holding clip matched the live Grabbing rig, but the regenerated preview clip's held interval was flat at the **wrong** rotation: the thumb remained 12.26° off. The previous flatness regression test caught drift but did not compare the held value to the authored endpoint. Both are now checked.

The scene demo now reads Standby and Grabbing transforms directly and blends position/scale plus quaternion Slerp, using the same eased grip weight as gameplay. It no longer samples the intermediate preview clip. Source pose edits therefore update the demo without rebaking. A fresh audit measured zero rotation, position or scale error at both endpoints; source rigs and cloud placement were unchanged.

The reusable `GrabPreview.anim` asset was also rebuilt using `AnimationUtility.SetEditorCurves` in a single batch, rather than setting quaternion components sequentially. Unity sampling then matched both authored clips at open/hold/release keys and throughout the held interval.

### Earlier-closure pass

Finger shaping now begins 0.20 seconds into the 0.60-second descent, using a shared approach curve in gameplay and the demo. Blend progress reaches 0.8 at contact and finishes smoothly over the next 0.12 seconds. The lift delay, authored endpoints and cloud placement are unchanged. Gameplay uses a replicated phase-start timestamp so remote presentation does not restart the approach on receipt.

An isolated check of this earlier path found a small geometric index overlap during approach (maximum sampled depth 0.0116 preview units). Foreground finger/orb sorting preserves the intended visible order, but this remains a geometric contact refinement to address with a near-contact breakdown rather than hiding it in timing claims.

## What the references support

- **Shape the grip during the reach.** Jeannerod's filmed prehension study reports finger closure anticipating contact and coordinated with the arm's final, slower approach. Closing after the arm has already stopped separates actions that normally overlap. [Jeannerod, 1984](https://doi.org/10.1080/00222895.1984.10735319).
- **Coordinate the joints, rather than make a finger wave.** Recorded grasping shows strong coupling between finger MCP and PIP rotations. Small overlaps can improve animation, but arbitrary large joint delays are not justified. [Mason et al., 2002](https://doi.org/10.1523/JNEUROSCI.22-04-01426.2002).
- **Use overlap, arcs and a controlled finish.** The animation principles reference recommends overlapping actions and carefully timed easing rather than abrupt starts/stops. [UC Irvine animation principles](https://ics.uci.edu/~fowlkes/class/ics162/exercises/animation_principles.html).
- **Verify the evaluated rotation, not only the curve's appearance.** Unity documents linked quaternion curves and shortest-path interpolation. Continuity processing is useful but is not proof that a sampled hold remains constant. [Unity animation curves](https://docs.unity3d.com/Manual/animeditor-AnimationCurves.html), [EnsureQuaternionContinuity](https://docs.unity3d.com/ScriptReference/AnimationClip.EnsureQuaternionContinuity.html).

The two scientific papers' abstracts were retrieved through Europe PMC; this is not a claim to have read their complete paywalled text. Timing recommendations below are animation starting points, not universal human timing constants.

## Recommended next pass

1. **Fix the preview jump first.** Give preview and gameplay the same pose evaluator and progress curve. A held pose must remain exactly held; do not jump to another part of a drifting clip.
2. **Start visible shaping before arrival.** With our existing ease-out descent, try beginning thumb opposition/index curl around 0.20–0.25 seconds into the 0.60-second descent. Adjust by fingertip clearance and time-to-contact, not by copying a biological percentage onto a different speed curve.
3. **Separate approach, contact and grip.** Keep the approved Standby and Holding endpoints. Add a near-contact breakdown with enough space for the orb; close most of the gap during approach, then ease the last small amount into contact. Initially budget roughly 0.08–0.12 seconds for that finish rather than spending the entire curl after arrival.
4. **Make the early curl readable.** Avoid an extended near-zero start followed by a compressed final motion. Use a short acceleration, a clear initial curl, and a longer deceleration near the surface. Do not simply shorten the whole action.
5. **Let the thumb oppose the index on its own arc.** Use small timing differences only where they help contact. Keep middle, ring and pinky curled as requested. Review the keyed mesh/wrist motion separately so it does not carry the gesture in place of finger articulation.
6. **Contact before lift.** Keep the orb stationary until contact, preserve its size and sorting, and lift only once the grip reads as secure. No orb pulse, elastic scale, or large wrist bounce.

## Acceptance checks

- Preview and gameplay match at the same normalized grasp progress.
- No discontinuity at grab→carry or carry→release; the held pose does not drift.
- Finger motion is visible during descent at normal speed, not just in slow motion.
- Review both normal and quarter speed, with close-ups of the contact pads.
- Fingertips do not penetrate or visibly slide across the orb during contact/lift.
- Existing endpoint poses, cloud placement, foreground sorting and no-pulse behavior remain intact.
