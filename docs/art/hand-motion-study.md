# Making the grasp feel natural

## Diagnosis

The reported problems have concrete causes. This study changes no poses, animation assets, or cloud placement.

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
