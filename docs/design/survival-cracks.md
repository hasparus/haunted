# Survival loss: cracks of light

## Problem with today's rule

Survival ends when `influence < max(survivalFloor, peak − survivalDrop)` = `max(−5, peak − 12)`.

- Players can't see it. "fired below 7" is a moving number they have to subtract in their head mid-chaos.
- It's inconsistent: before your peak passes 7 you get 5 misses of slack, after that you get 12.
- Under the hood it's already a leaky bucket: each miss drains 1, each influence point refills 1 up to the peak. The structure is fine. It's just invisible.

## Rule: 5 cracks, influence seals them

- The demons' hold on him has **5 cracks** of slack (`maxCracks = 5`).
- A thought that reaches him **unhaunted** → +1 crack. A thought that is **forgotten** (flies away) → +1 crack. Either way he coped with it.
- Every **3 influence** delivered **seals** the newest crack (`influencePerSeal = 3`). Big thoughts and co-op stamps seal faster, since they give more influence.
- **5 open cracks → downsized.** Peak influence stays the score.

Why 5: you can count it at a glance (3 is too harsh with four people tripping over each other, 10 is a bar, not a count). Why 3 per seal: the ratio comes out of it, so players never have to see it:

| avg influence per haunt | misses you can sustain forever |
|---|---|
| 1.0 (solo stamps) | 25% |
| 1.3 (some co-op, some big) | 30% |
| 1.8 (lots of co-op stamps) | 37% |

So the real rule is "haunt about 3 in 4 thoughts". The 5 cracks just absorb streaks. The belt ramp (0.75× +1× per 150s) eventually beats any team. Rough estimate: a team that haunts 0.5 thoughts/s falls behind at about 3 min, 0.7/s at about 5 min, 0.9/s at about 7 min, then dies 30 to 60 s later. That fits `survivalHardAt = 360`.

Week mode: show the cracks, but no game over. Optional later: 5 cracks restarts the day.

## Visuals: light breaks in, not glass breaks

Careful with the theme. In this game a cracking mind is **good for the demons**. If the screen cracked like broken glass, players would read it as "he's losing it" and feel like they were winning. So flip it: the screen is the demons' darkness (the Mood vignette and gloom), and each miss lets in a **crack of warm daylight**. He's coping. Light is the enemy.

- **Crack appears:** a jagged emissive line (`#FFE9B0`, soft bloom) shoots from where the thought reached him (end of the belt / his head) to the next free **corner slot** of the screen. It grows along its length over about 120 ms. Fixed slots (4 corners, then the top center) mean the screen itself counts for you. Sound: a glass tick plus a relieved exhale from him.
- **Crack seals:** demon ink (black tendrils, in the colors of the demons who delivered) creeps along the newest crack as influence builds toward the next seal: 1/3, 2/3, then a squelch and it's gone. This is the heal meter, drawn on the crack itself.
- **HUD:** 5 crack pips by the influence counter replace "fired below X".
- **Escalation is tied to open cracks, not to lifetime misses**, so recovering calms everything down again:
  - 3 open: the light flickers, the gloom vignette eases off a little (the room gets brighter, which is bad).
  - 4 open: heartbeat, low-pass on the music, the last pip pulses, he hums happily.
  - 5: the darkness **shatters**. White-out to sunlight, 0.5 s visual slow-mo (render only, never `Time.timeScale`, the networked clock depends on it), then the DOWNSIZED card.

## Screen shake: yes, but small, scaled by danger, with a hard cap

Shaking a bit more each time is right, with three adjustments:

1. **Scale by open cracks, not by total pebbles lost.** If shake only ever grows, the late game, where the belt is fastest and aim matters most, is also the shakiest. That punishes skill at its peak and on a shared couch screen it makes everyone sick. Scaled by open cracks, sealing a crack makes the screen calm down, which also feels good.
2. **Keep it short and below the big events.** `CameraRig.Shake` adds trauma and the offset grows with trauma², so steps feel bigger than they are. Pill = 0.5, claw = 0.4, and forgotten thought = 0.25 already exist. Suggested: `Shake(0.15 + 0.06 × openCracks)`, which gives 0.21 → 0.39 over cracks 1 to 4, never above the pill. The final shatter gets `Shake(1)`.
3. **Shake decorates, never informs.** It has a settings toggle (`ShakeEnabled`), so the crack, the pip, and the sound must carry the message on their own. Add a short hit-flash or chromatic punch on the crack's origin for the same impact, with less motion sickness.

## Juice: what the talks say, applied here

Sources: Eiserloh, *Juicing Your Cameras With Math*; Nijman, *The Art of Screenshake*; Jonasson & Purho, *Juice It or Lose It*; GMTK, *Secrets of Game Feel and Juice*.

- **Trauma model (Eiserloh).** `CameraRig` already follows it: trauma 0 to 1, offset grows with trauma², linear decay, Perlin noise instead of random jitter, unscaled time. Two upgrades from the talk:
  - Add a small **roll** (about ±1.5° × trauma²). Rotation reads as stronger shake than the same amount of translation, so the translation can shrink.
  - Give each crack's shake a **direction**: a kick away from where the thought reached him, then Perlin noise on top. Directional kick says where the hit came from; pure noise doesn't (Nijman's "kickback").
- **Permanence (Nijman).** Leave evidence of what happened on screen. The cracks are exactly that: the miss doesn't flash and vanish, it stays until you earn it back. That's why the cracks should stay on screen, not be a HUD pip alone.
- **Hit-stop (Nijman).** Freeze for a few frames on impact. In this networked game it must be **local and visual only**: hold sprite animation and particles for about 60 ms when a crack lands, never pause the simulation or touch `Time.timeScale`.
- **Layer the response (Juice It or Lose It).** One event, several channels at once: crack line, flash at its origin, glass tick, his exhale, pip change, small directional shake. Each one is subtle; together they hit hard.
- **Restraint (GMTK).** If everything shakes and flashes, nothing stands out. Keep the loud response for things that cost you: a miss, a forgotten thought, the pill, the claw. A delivered thought gets a satisfying pop, not a shake. The seal (squelch plus the pip coming back) is the one positive event that earns extra juice, because it is the comeback moment.

## Tunables (Balance → Survival)

```
maxCracks = 5
influencePerSeal = 3
crackShakeBase = 0.15, crackShakeStep = 0.06
```

These replace `survivalFloor` and `survivalDrop`. `survivalMissPenalty` and `forgottenPenalty` can stay as influence penalties or go; cracks now carry the threat.
