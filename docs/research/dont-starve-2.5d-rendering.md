# Don't Starve–style 2.5D rendering: research and a pipeline for `haunted`

Status: research note, October 2026. Target: this repo (Unity 6000.6, URP 17.6, Universal Renderer; PC tier = Forward+, **WebGL tier = Mobile_RPAsset → plain Forward, no depth texture, no additional-light shadows**). Every recommendation below is meant to also work in the WebGL build.

How to read the evidence: Klei has never published a rendering postmortem, so most specifics come from **DST's shipped Lua scripts** (mirrored on GitHub) and **the GLSL source embedded in Don't Starve's `.ksh` shader files** (also mirrored). Facts taken from that code are marked **[code]**. Things I reasoned out from the engine API or from looking at the game are marked **[inferred]**.

---

## 0. What the repo already does (starting point)

| Area | Current state | Where |
|---|---|---|
| Camera | Perspective, FOV 34, about 50° pitch (`(0,15,-12.5)` looking at the origin), fixed heading, auto-fits to demons at distance 28–100, trauma shake | `Assets/_Game/Scripts/CameraRig.cs`, `Editor/PrototypeBuilder.cs` |
| Billboards | Demon `SpriteRenderer` copies `camera.rotation` (fully view-aligned, as in DS). Front/side/back chosen from facing relative to the view, side flipped with `flipX` | `Scripts/Demon.cs` `LateUpdate` |
| Shadows | Invisible quad with the sprite's texture turned toward the sun, alpha-clipped `URP/Lit`, `ShadowsOnly`. Casts the drawing's silhouette | `Demon.cs`, `Resources/SpriteShadow.mat` |
| Art import | `Resources/Art/*` → Sprite, 300 PPU, bottom-centre pivot, FullRect mesh, mipmaps | `Editor/ArtImport.cs` |
| Grade | Global Volume built at runtime: Vignette + ColorAdjustments. Gloom 0→1 over Monday→Sunday, 0.6 in Survival | `Scripts/Mood.cs` |
| "Mental" signals | `GameState.Influence`, the per-day goal and fire line, the pill inversion window `Progression.InvertedUntil`, haunted thoughts trembling | `Progression.cs`, `Pebble.cs` |

The camera is already close to DST's numbers (DST uses FOV 35 and 30–60° pitch, see §1). Demons are already view-aligned billboards with three facings. What's missing is DS's **layered draw order and contact shadows**, its **world-space lightmap with radial lights on black ambient**, **noise-broken ground**, and the **sanity post stack** (LUT layering, edge wobble, shadow creatures).

---

## 1. Camera and projection

### What DST does [code: `scripts/cameras/followcamera.lua`, `components/playercontroller.lua`]
- **Perspective camera, `fov = 35`** (vertical).
- **Distance and pitch are coupled.** `mindist = 15`, `maxdist = 50`, default `distancetarget = 30`. Each update sets `pitch = lerp(mindistpitch = 30°, maxdistpitch = 60°, (distance - mindist)/(maxdist - mindist))`, so zooming out also tilts toward top-down. Caves use 15–35 distance and 25–40° pitch, which keeps the camera lower and more claustrophobic.
- **Heading.** The default `headingtarget = 45`. `RotLeft/RotRight` add or subtract `rotamount = 45` (a comment shows 90 and 22.5 were tried). The heading eases toward the target with `headinggain = 20`.
- **Camera basis.** `dir = (-cos p·cos h, -sin p, -cos p·sin h)`, `right = (cos(h+90°), 0, sin(h+90°))`, `up = dir × right`. The camera never rolls, and `right` always lies in the ground plane. That's why billboards can use the camera's right vector directly.
- The audio listener sits slightly in front of the focus point, along the view direction.

### How sprites stay upright [code: `constants.lua` `ANIM_ORIENTATION`, `anim.ksh`]
- `ANIM_ORIENTATION = { BillBoard = 0, OnGround = 1, OnGroundFixed = 2 }`, and the default is `BillBoard`. Characters, trees and items are billboards. Rugs, scorch marks and turf decals are `OnGround` (lying flat on the XZ plane, turning with the camera heading) or `OnGroundFixed` (fixed in world space).
- [inferred] A billboard here is **view-plane aligned**: the quad's right is the camera's right and its up is the camera's up. It is *not* cylindrical. Because the camera never rolls and `right` stays horizontal, sprites look upright and undistorted at any pitch. A cylindrical (Y-only) billboard would look squashed by `cos(pitch)`, about 0.64× at 50°. The shader uniform list includes `CAMERARIGHT` [code: shader uniform dump], which fits this construction.
- The pivot is the sprite's foot on the ground. The quad tilts back with the camera, so its top leans *away* from the viewer and never dips below y = 0.

### For `haunted`
1. Keep the view-aligned billboard (`Demon.cs` already does `rotation = view.rotation`). Move it into the vertex shader (§7.3) so every sprite gets it for free and can apply the pivot-depth trick from §3.
2. Add a `heading` to `CameraRig` with optional 45° steps and DST's ease (`heading += (target - heading) * (1 - exp(-20·dt))`). Keep DST's distance→pitch coupling: `pitch = lerp(40°, 60°, inverseLerp(MinDistance, MaxDistance, distance))` works with the current 28–100 range. When the rig pulls out to fit four demons it also tilts toward top-down, which helps readability.
3. If you rotate, the three-facing logic in `Demon.cs` already works per frame because it reads `view.right` and `view.forward`.
4. When the heading changes, the sort axis has to follow (§3).

---

## 2. Sprite pipeline

### Klei's format [code: ktools README; DST `AnimState` API]
- An animation ZIP contains **`anim.bin`** (the *bank*: named animations, each a list of frames, each frame a list of elements = symbol hash + symbol frame + layer + **full 2×3 affine matrix** + z order), **`build.bin`** (the *build*: named **symbols**, each with frames that reference regions in atlas pages), and **`atlas-N.tex`** (KTEX textures).
- KTEX defaults to **DXT5, premultiplied alpha, and Lanczos-filtered mip chains** (ktech defaults). Premultiplied alpha is what keeps thick ink lines from getting white or black fringes when mipped.
- Spriter can't hold everything: "shearing isn't supported" (krane `--check-animation-fidelity`). The native format has per-element shear because the art originated in **Flash**, where Klei drew and animated Wilson and friends [forum: modding tool post; Klei dev mentions a Flash→Spriter exporter]. For mods, Klei shipped **Spriter + an autocompiler** that compiles `exported/*.scml` into these ZIPs at game start.
- **Bank/build separation is the key idea.** One skeleton (bank) can play with any build, and individual body parts can be swapped at runtime (`AnimState:OverrideSymbol("swap_object", build, symbol)`, `HideSymbol`, `Hide(layer)`). That is how every hat, tool and DST skin works, with no new animation needed.
- **Facings.** `Transform:SetFourFaced()` (players), `SetSixFaced`, `SetEightFaced`, `SetTwoFaced`, `SetNoFaced`. Each animation exists per facing (exported as `_down`, `_side`, `_up` variants). The engine picks a variant from the entity's rotation relative to the camera heading, and mirrors `side` for left [code: `player_common.lua` `SetFourFaced`; the facing suffix convention comes from the mod tools].
- **Anim shader** [code: `anim.ksh`]: two atlas pages selected per vertex by `TEXCOORD0.z < 0.5`, **alpha test then `discard`** (`PARAMS.x`), `TINT_MULT` and `TINT_ADD` (hit flashes, ghosting, `SetMultColour`), lighting sampled from a **world-XZ lightmap** with a `LIGHT_OVERRIDE` floor (glowing things ignore darkness), and an optional **`FADE_OUT` erosion** (dissolve against an erosion texture in a camera-projected space, driven by `EROSION_PARAMS`).

### Art authoring (Klei look)
- The look is hand-drawn: heavy **variable-width black ink contours**, flat or limited colour fills with a little dry-brush texture, and Gorey/Burton/early-20th-century-advertising references per Klei's Kevin Forbes [Game Developer, Game-Wisdom interviews]. Line weight carries the style. It survives downscaling because the lines are thick relative to the shape.
- Characters are made of **many small rigid parts** (head, torso, arms, hands, legs, plus swap slots), animated by affine transforms with a few drawn frames for faces and hands. That's cutout animation, not mesh deformation.

### For `haunted` (Unity)
- **Animation tool.** Pick one: Unity **2D Animation (bones + Sprite Library/Resolver for swaps)** or **Spine (spine-unity)**. Both support Klei's "bank vs build" idea. 2D Animation is free and in-package. Spine is the closest to Klei's workflow and has a proper runtime for skin swaps. Unity 2D Animation's **Sprite Library Asset + Sprite Resolver** is the direct analogue of `OverrideSymbol`. Use it for the per-player colour variants and the "devil sprites / desk drawing" swaps.
- **Facings.** Keep three authored facings (front/side/back) and mirror the side. Add 3/4 facings later only for hero characters.
- **Texel density, computed for this camera.** At distance *d* and FOV 34°, the visible height at the focus is `2·d·tan 17° ≈ 0.61·d`. At the rig's `MinDistance = 28`, that's 17.1 units, or about **63 px/unit at 1080p** and 126 px/unit at 4K. `ArtImport.PixelsPerUnit = 300` is therefore oversampled 2.4–4.8×. That's fine for the EyeView close-up but wastes memory. Recommendation: author at 2× of the closest on-screen size, **≈150–200 PPU**, keep mipmaps on, and use trilinear filtering with **mip bias ≈ −0.5** so ink lines don't go soft. The mip chain should be generated from premultiplied colour (or with `alphaIsTransparency`, which the importer already sets) to avoid dark halos.
- **Atlases.** Use **Sprite Atlas v2** per character or set (all facings and parts of one demon in one atlas, or at most 2 pages like Klei) so a character draws in one batch. Use 2–4 px padding and extrude borders. Use **Tight** mesh type for large sprites with lots of empty space; `ArtImport` currently forces FullRect, which costs overdraw on transparent sprites. Compression: ASTC 6×6 or BC7 on desktop, ASTC/ETC2 on WebGL. Note that BC7 isn't available on every WebGL device. Unity's WebGL build can ship both DXT and ASTC.
- **Line boil (optional).** Klei's lines are static. For a haunted feel you can re-draw 2–3 alternates per symbol, or apply a ±0.5 px UV jitter at 8–12 Hz. Tie the amplitude to the sanity scalar (§5).

---

## 3. Sorting, depth, shadows, decals

### What DS does
- **Draw layers** [code: `constants.lua`]: `LAYER_BACKDROP 0, BELOW_OCEAN 1, BELOW_GROUND 2, GROUND 3, BACKGROUND 4, WORLD_BACKGROUND 5, WORLD 6, WORLD_DEBUG 7, FRONTEND 8`. Within a layer, `AnimState:SetSortOrder(n)` sets coarse order (there are named tables such as `ANIM_SORT_ORDER.OCEAN_*`), and `AnimState:SetFinalOffset(n)` makes a small per-entity nudge so attached FX or child parts render in front of or behind their parent. For example, grass tufts use `SetFinalOffset(1)` [code: `prefabs/grass.lua`].
- [inferred] Inside `LAYER_WORLD`, billboards are painter-sorted back to front by their **pivot's depth along the camera's flattened forward vector**, alpha-tested, and they don't rely on a depth buffer between sprites. Ground layers are drawn first, so nothing "clips into" the ground. The ground is never in front of a sprite.
- **Blob shadows** [code: `player_common.lua`]: `entity:AddDynamicShadow(); DynamicShadow:SetSize(1.3, .6)` gives an ellipse decal on the ground, 1.3 wide by 0.6 deep, drawn in a ground layer. It doesn't depend on any light, so it reads even at night.
- **Ground decals** are just anims with `OnGround` orientation in `LAYER_GROUND` or `BACKGROUND`.

### For `haunted`
Two workable strategies. Use (A) by default, (B) for crowds of clutter.

**(A) Sorted transparent billboards (DS-faithful, soft edges)**
- In URP's Universal Renderer, transparent `SpriteRenderer`s sort by sorting layer → order in layer → distance. Set **`camera.transparencySortMode = TransparencySortMode.CustomAxis`** and **`camera.transparencySortAxis = flattened camera forward`** (or the project-wide `GraphicsSettings` equivalents; check in the Frame Debugger that URP 17 honours it for your shader's queue) (`Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized`). Update it whenever the heading changes. Use **Sprite Sort Point = Pivot**; the importer already pivots at the bottom centre.
- Sorting layers that mirror Klei: `Ground` (tile mesh, if drawn as sprites), `GroundDecal` (blob shadows, rugs, spotlight rings), `World`, `WorldFX`, `Overlay`. Use a **`SortingGroup`** on each multi-part character so its parts sort as one unit (the equivalent of `SetFinalOffset`).
- Keep ZTest on (so 3D props like desks, walls and the belt still occlude sprites) and ZWrite off.
- **Pivot-depth trick** so a tilted billboard's top doesn't clip into a wall or desk behind it. In the vertex shader, compute the clip position of the *pivot* and give every vertex of the quad the pivot's depth. The sprite then occludes and is occluded as a flat card standing at its feet. This is the 2.5D equivalent of DS's "pivot sorts the whole sprite". Shader in §7.3.

**(B) Alpha-clipped, depth-writing sprites (for props in bulk)**
- `AlphaTest` queue, `clip(a - 0.5)`, ZWrite on, plus **alpha-to-coverage** on the PC tier (MSAA). No sorting cost, works with SSAO and shadows, and the edges get crunchier. That's acceptable for thick ink outlines, which is why Klei can alpha-test. Use it for plants, clutter and pebbles.

**Shadows (keep and extend what exists)**
1. **Contact blob** (new, DS-style): one shared quad per entity in `GroundDecal`, using a soft ellipse texture with multiply blend, sized about `(1.3, 0.6)` × sprite width. Squash it with height when jumping or thrown (scale it by `1/(1 + y)` and fade alpha). This is what grounds a billboard. Without it, characters look like they float, even with real shadows.
2. **Silhouette caster** (existing, better than DS): keep the sun-facing `ShadowsOnly` card from `Demon.cs`. Use it only on the PC tier and for the main directional light. In WebGL Mobile quality, additional-light shadows are off, so the spotlight and campfire-style lights can't cast it. Fall back to a projected-silhouette decal there (the same sprite drawn flattened onto the ground with a skew matrix away from the light, multiply blend).
3. **SSAO and sprites.** The PC renderer has SSAO enabled. Transparent sprites write neither depth nor normals, so SSAO ignores them, which is fine. Alpha-clipped (B) sprites will receive and cast AO, which can look dirty on flat-shaded art. Give them their own layer excluded from SSAO, or drop SSAO and rely on blobs.

---

## 4. Ground

### What DS does
- **Tiles are 4×4 world units** (`TILE_SCALE = 4`) [code]. Each tile type has an **atlas** (`levels/tiles/<name>.tex/.xml`) and a **noise texture** (`levels/textures/<noise>.tex`, e.g. `Ground_noise` for forest and grass) [code: `tiledefs.lua`, `tilemanager.lua`]. Tile types draw in an explicit **render order** (`ChangeRenderOrder`), so higher-priority turf overlaps lower turf.
- [inferred, from extracted assets] Edges aren't masks computed in a shader. The tile atlas holds **hand-painted edge and corner pieces with ragged alpha**, and the map layer builds a mesh that puts the right piece on each tile by comparing it with its neighbours. Each tile type's edges **bleed over** neighbours of lower render order. That's what gives the organic, painted borders.
- **Ground shader** [code: `ground.ksh`]: `colour = tile_atlas(uv) * noise(worldXZ / NOISE_REPEAT_SIZE)`. The world-space noise multiply breaks up tiling across tiles. An `ENABLE_OVERLAY` variant reads a **3-channel multilayer texture** in the same noise space and lerps toward three tint colours `GROUND_COL0..2` with weights `BLEND_FACTOR` (used for seasonal and overlay tints). The result is premultiplied by alpha and multiplied by the **same lightmap** as the sprites.
- **Falloff** [code: `worldtiledefs.lua`, `tilemanager.lua` `AddFalloffTexture`]: a separate falloff set renders the **edge of land where it drops into void or ocean** (the "cliff lip"), with rules (`should_have_falloff`, `neighbor_needs_falloff`) about which neighbour pairs get it.

### For `haunted` (an office floor, not a forest, but the same tricks apply)
- **Floor mesh + splat map.** One flat mesh for the floor. An RGBA **splat texture at 1 texel per tile** (or per 0.5 m) marks which material each cell is (carpet, tile, linoleum, "haunted" rot). In the shader, **bilinear-sample the splat and push the blend weights through a noise or height threshold** (height-blend: `w_i = saturate((h_i + splat_i) - max_j(h_j + splat_j) + k)`). This gives ragged, painterly edges with no hand-made edge atlas. It's the procedural equivalent of Klei's edge pieces. A low-res splat is also easy to edit at runtime (rot spreading as influence grows).
- **World-space noise multiply.** Copy Klei directly: `albedo *= lerp(1, noise(worldXZ / 7), 0.35)`, plus a second, larger macro noise (`/31`) to kill tiling. For anti-tiling of the base textures, use stochastic or "texture bombing" sampling (Heitz & Neyret 2018; Quílez "texture repetition").
- **Overlay tints.** Klei's `GROUND_COL0..2` maps nicely to *gloom*: channel R = "cold" tint by weekday, G = rot/mould patches revealed as influence rises, B = the light-pool decal.
- **Edge falloff.** The playfield boundary (the strip in front of the office, the EyeView) is where an ink-drawn **lip** helps: a ribbon mesh along the floor edge with a hand-painted falloff strip.
- **Decals.** Blob shadows, spotlight rings, scorch and scribble marks go in `GroundDecal` as flat quads, or as URP **Decal Projectors**. Use the projectors on PC only: they need the depth texture, which the Mobile/WebGL tier doesn't produce.

---

## 5. Post-processing, atmosphere, darkness and insanity (the important bit)

### 5.1 DST's post stack, exactly [code: `scripts/postprocesseffects.lua`]
DST builds a chain and sorts it relative to the colour-cube pass:

```
ZoomBlur  →  Bloom  →  Distort  →  ColourCube (base)  →  Lunacy  →  MoonPulse  →  MoonPulseGrading
```
- **Bloom**: bright-pass into a **¼×¼ res** buffer, separable `blurh`/`blurv`, added back.
- **Distort** (`postprocess_distort`): uniforms `DISTORTION_PARAMS = (time, factor, inner_radius, outer_radius)` and `FISHEYE_PARAMS`.
- **ColourCube**: a sampler effect, **`combine_colour_cubes`, renders a combined 1024×32 LUT each frame** from **six input cubes** (CC0/CC1/CC2 × source/dest) using `CC_LERP_PARAMS` (the time-of-day blend) and `CC_LAYER_PARAMS` (x = insanity weight, y = lunacy weight), times `INTENSITY_MODIFIER`. The full-screen pass then does **one** lookup into that combined LUT. LUT samplers are point-filtered, and the full-screen shader does **manual bilinear** on the 1024×32 strip "to prevent anisotropic red outline filtering bug" [code: shader comment].
- **Lunacy** (DST's enlightenment, the inverse of insanity): an overlay image `overlays_lunacy.tex` + smoke, blended by `OVERLAY_BLEND`. **ZoomBlur is enabled only when both Distort and Lunacy are on.**

### 5.2 Colour cubes per season and phase [code: `components/colourcube.lua`]
- **Three channels**: 0 = ambient (season × phase), 1 = insanity, 2 = lunacy.
- Ambient table: autumn `day05_cc / dusk03_cc / night03_cc`, winter `snow_cc / snowdusk_cc / night04_cc`, spring `spring_day_cc / spring_dusk_cc`, summer `summer_day/dusk/night_cc`, `purple_moon_cc` on full-moon nights, caves `caves_default`.
- Insanity table **per phase**: `insane_day_cc`, `insane_dusk_cc`, `insane_night_cc`. So the insanity look itself changes with time of day.
- **Blend times**: phase changes take day 4 s, dusk 6 s, night 8 s. Season changes take 10 s. Overrides (e.g. the mole hat's night vision) take 0.25 s. If a new target arrives mid-blend, the blend restarts from the current destination.
- Cube format: **32³ unwrapped to 1024×32** (blue slices laid horizontally). That is **the same layout URP's Color Lookup uses**, and this project's `m_ColorGradingLutSize: 32` already matches. Klei's cubes can be recreated in any grading tool by grading a neutral 1024×32 strip.

### 5.3 Ambient light and the darkness [code: `components/ambientlighting.lua`, `lighting.ksh`, `anim.ksh`/`ground.ksh`, `player_common.lua`, `prefabs/campfirefire.lua`]
- **Ambient colour by phase**: day `(255,230,158)` (warm), dusk `(150,150,150)`, **night `(0,0,0)`**. Full moon is `(84,122,156)`, caves are 0. Lerp times are 4, 6 and 8 s. Night is *literally* black ambient. Anything you can see at night is lit by a light source.
- **Lights render into a lightmap.** Each light draws geometry in world space into an offscreen target. The fragment computes `d = distance(pos.xz, light.xz)` and `t = clamp(exp(K0·(d/R)^(−K1)))`, which with the engine's constants is a **stretched-exponential / generalised-Gaussian falloff** (soft core, long smooth tail, no hard edge), and outputs `mix(black, LIGHT_COLOUR, t)`. Lights accumulate additively.
- **Every sprite and ground pixel** then computes `uv = (worldPos.xz − extents.xy) * extents.zw`, takes `light = clamp(lightmap(uv) + AMBIENT)`, and multiplies by it. A billboard is lit by the lightmap **at its own world XZ**. Because a view-aligned quad barely moves in XZ from foot to head, a character standing at the edge of the firelight is lit almost uniformly. That's why characters "step out of the dark" so cleanly.
- **Campfire levels**: radius 2/3/4/5, intensity 0.8, falloff 0.33, colour `(1, 1, 0.75)`, flickered by animating radius and intensity (`components/lighttweener.lua` lerps radius, intensity and falloff).
- **"Darkness kills you"**: each player has a `LightWatcher` with `SetLightThresh(.075)`, `SetDarkThresh(.05)` (hysteresis) and `SetMinLightThresh(.61)` (the sanity drain threshold). The `grue` component (Charlie) attacks after some time with `IsInLight()` false. The gameplay reads the *same light field the renderer draws*.
- **Vignette** is not a shader. It's an always-on **animated UI overlay** (`vig` build/bank, `PlayAnimation("basic", true)`) in the HUD's overlay root [code: `screens/playerhud.lua`], so it breathes.

### 5.4 Insanity: how DST renders it and how it escalates [code: `colourcube.lua`, `postprocess_distort` shader, `transparentonsanity.lua`, `shadowcreaturespawner.lua`, `tuning.lua`, `easing.lua`]

Let `p` = sanity percent (0..1). Klei uses `easing.outQuad(t, 0, 1, 1) = t(2 − t)`, so:

```
sanity_distortion  s = 1 − outQuad(p) = (1 − p)²          -- quadratic ramp, small until ~50%
insanity LUT weight     CC_LAYER_PARAMS.x = s
distortion factor       f = clamp(1 − s · user_distortion_slider − fisheye, 0, 1)   -- 1 = clean
wobble clock speed      fxspeed = outQuad(1 − p, 0, 0.2, 1) · slider = 0.2·(1 − p²)
fxtime += dt · fxspeed ; shader angle = fxtime · 50  →  up to 10 rad/s (≈1.6 Hz) at p = 0
```
(Characters tagged `dappereffects` square `s` again, which softens the effect.)

**The distortion shader** (`postprocessdistort.ksh`, DS version; DST keeps the same idea and adds a fisheye term):
```glsl
// vertex: a slightly zoomed-in UV that orbits on a small circle
float range = 0.00625;              // 0.625 % of the screen
vec2  offs  = vec2(cos(TIME*50.0 + 0.25), sin(TIME*50.0));
uv1 = uv * (1.0 - 2.0*range) + range + offs * range;
// fragment
vec3 base = tex(SRC, uv);
vec3 dist = tex(SRC, uv1);
float mask = clamp((1.0 - distance(uv, vec2(0.5)) - INNER) / (OUTER - INNER), 0.0, 1.0); // INNER=.5 OUTER=.685
dist = mix(dist, base, FACTOR);      // FACTOR=1 → no distortion
base = mix(dist, base, mask);        // centre (d<0.315) stays clean, full wobble from d≥0.5 (edges/corners)
// …then the colour-cube lookup on `base`
```
So the "insanity wobble" is **a ghosted, slightly magnified copy of the frame orbiting a ~7 px circle, blended in only at the screen edges**. The amplitude in pixels never grows. What grows is how much of the ghost is blended in, `(1−p)²`, and its orbit speed, `1−p²`. It reads as the periphery swimming while the centre stays readable, which is good for gameplay. Users can scale it with the Distortion option (`Profile:GetDistortionModifier`), an accessibility slider worth copying.

**Escalation table** (vanilla values):

| sanity p | LUT weight & wobble blend `(1−p)²` | wobble speed `10(1−p²)` rad/s | shadow alpha max `0.4(1−p)` | shadow-creature cap | state |
|---|---|---|---|---|---|
| 100 % | 0 | 0 | 0 | 0 | sane |
| 75 % | 0.06 | 4.4 | 0.10 | 0 | |
| 50 % | 0.25 | 7.5 | 0.20 | 0→1 (spawns only when p ≤ 0.5) | |
| 30 % | 0.49 | 9.1 | 0.28 | 1 | |
| 15 % | 0.72 | 9.8 | 0.34 | 1 | **goes insane at ≤ 15 %** (`SANITY_BECOME_INSANE_THRESH = 30/200`) |
| 10 % | 0.81 | 9.9 | 0.36 | 1 → **2 below 10 %** | terrorbeaks can spawn below 10 % |
| 0 % | 1 | 10 | 0.40 | 2 | |

Recovery happens at **≥ 17.5 %** (`35/200`). The hysteresis stops the state flickering.

**Shadow creatures** [code: `prefabs/shadowcreature.lua`, `components/transparentonsanity.lua`]
- Client-side only. `SetMultColour(1,1,1,.5)`, then `TransparentOnSanity` overrides alpha to `(1 − p) · 0.4 · (1 + 0.25·(sin(t·rand(0.25..2.25)) − 1))`. That's a per-creature random flicker, approached at 1 alpha unit per second, and **forced to the 0.4 maximum while it's targeting you**.
- It also scales **their sound volume by alpha / 0.4**, so they're heard as faintly as they're seen.
- **`AnimState:UsePointFiltering(true)`**: shadow creatures are point-sampled, so they look subtly "wrong" and crunchy next to the smooth art. It's a cheap, clever touch.
- They're only real (attackable, attacking) for players whose sanity `IsCrazy()`. Population is re-rolled every few seconds (`SANITYMONSTERS_SPAWN_INTERVAL = 5`, `MAXPOP = {1, 2}`).

**Layering summary**: insane LUT (per phase) fading in by `(1−p)²` → peripheral orbit wobble at the same weight, speeding up → translucent, flickering, point-filtered creatures with alpha `0.4(1−p)` → at ≤15 % they become real. Audio follows the same curves. Everything is driven by **one scalar through two easing curves**, which is the main lesson.

---

## 6. Modern improvements beyond Don't Starve

| Technique | What it adds | Cost / WebGL | Notes and sources |
|---|---|---|---|
| **Normal- or height-mapped sprites** | Firelight wraps around ink shapes, rim light from the spotlight | Cheap. One extra texture per atlas | Generate with **Laigter** (free, GPL, batch CLI; makes normal, specular, parallax and AO maps) or SpriteIlluminator, or paint 4-direction light rigs. With a view-aligned billboard, `TBN = (cam.right, cam.up, −cam.forward)`. Quantise N·L through a 2–3-step ramp to keep the flat Klei look |
| **Lightmap + SDF soft shadows** | Lights blocked by desks and people, soft penumbrae on the floor | Medium. Runs on a 256–512² world-XZ texture, not the screen | Rasterise occluder footprints (desk rectangles, demon ellipses) into the lightmap's space → **jump-flood** to an SDF → per light, march the SDF for soft shadows (Quílez-style `k·h/t` penumbra) |
| **2D GI / Radiance Cascades** in the lightmap domain | Coloured bounce: red pads tint the floor, firelight fills corners | Higher. PC tier only, or ~256² on WebGL | Sannikov 2023, used in *Path of Exile 2*. Interactive walkthroughs: jason.today "Building Real-Time Global Illumination" (JFA + distance fields → radiance cascades, bilinear-fix for ringing); *Holographic Radiance Cascades* (arXiv 2505.02041). Because DS lighting already lives in a top-down 2D texture, 2D RC applies **directly**. That's a natural next step for this style |
| **Vertex wind or tremble** | Plants sway; haunted thoughts and papers shiver | Free | In the billboard vertex shader: `offset = cam.right * (sin(t·f + hash(pivot)) + gust(worldXZ,t)) * amp * uv.y²`. The `uv.y²` keeps the feet planted. Port `Pebble`'s CPU tremble here for many objects |
| **Depth-aware fog / darkness gradient** | Distance haze, low-lying fog, darkness that gets *thicker* away from light | Free if computed per object | WebGL has no depth texture, so compute fog **in the sprite and ground shaders from world position** (and the pivot depth for billboards) instead of reconstructing from the depth buffer |
| **Erosion dissolve** (Klei's `FADE_OUT`) | Demons materialise or dissolve, thoughts burn away | Free | Noise texture in camera-projected space plus a threshold ramp, with an ink-coloured edge band |
| **Bloom on lights only** | Fires and the spotlight glow, like DS's ¼-res bloom | URP Bloom is fine on PC; use low-quality bloom on WebGL | Keep its threshold high so white paper doesn't bloom |
| **Paper grain + line boil** | Hand-made feel | One texture fetch in the uber pass | Static paper texture in screen space, slowly panning; line-boil jitter amplitude tied to the sanity scalar |
| **Chromatic split, desaturation, inverted hue** | Extra cues for the pill-inversion window | In the same uber pass | DST's lunacy shows a second "mode" with its own LUT and overlay. Use that model for `Progression.Inverted` |

---

## 7. Recommended pipeline for `haunted`

### 7.1 One scalar to drive the mind effects
Add a client-side `MindFx` (or extend `Mood.cs`) that exposes **`mind ∈ [0,1]`** (0 = calm, 1 = breaking) and **`inverted ∈ [0,1]`** (eased in and out around `Progression.InvertedUntil`). Suggested mapping, which is a design call:
`mind = max(gloom, danger)`, where `gloom` = the existing weekday ramp and `danger = 1 − saturate((influence − fireLine) / margin)`, i.e. how close the run is to "downsized". Then copy DST's curves:
```
s       = mind²                      // DST (1−p)², with p = 1 − mind
speed   = 0.2 · (1 − (1−mind)²)      // DST 0.2(1−p²)
lutW    = s;  wobbleW = s · settingsSlider;  creatureAlpha = 0.4 · mind
```
Add a **Distortion slider** next to the existing "effects" and "screen shake" toggles. DST ships one, and players with motion sensitivity will want it.

### 7.2 Render order (per frame)

| # | Pass | Where | Tier |
|---|---|---|---|
| 0 | **Lightmap**: an orthographic top-down camera (or a `ScriptableRenderPass` at `BeforeRendering`) draws additive radial-light quads into `_LightMap` (RGBA8 or R11G11B10, 256–512², covering the play area). Set global `_LightMap`, `_LightMapExtents = (minX, minZ, 1/sizeX, 1/sizeZ)`, `_Ambient` | Custom | All |
| 0b | (PC, optional) occluder footprints → JFA SDF → shadowed lightmap, or RC GI | Custom compute or fragment | PC |
| 1 | Shadow maps (sun + spotlight). Silhouette cards are `ShadowsOnly` | URP | PC (WebGL: sun only) |
| 2 | **Opaque 3D**: floor, desks, belt, walls (Lit or custom). The floor shader does splat, noise and lightmap | URP opaque | All |
| 3 | **Alpha-clipped sprites** (clutter, pebbles), ZWrite on | AlphaTest queue | All |
| 4 | **Ground decals**: blob shadows, rings, rot patches. Multiply blend, ZWrite off, sorting layer `GroundDecal` | Transparent, first | All |
| 5 | **World billboards**: demons, people, FX. Custom-axis sorted, ZTest on, ZWrite off, pivot depth, lightmap-lit, wind and tremble in the vertex stage | Transparent | All |
| 6 | **Shadow creatures / haunt FX**: `mind`-driven alpha flicker, **point filtering**, optional dissolve | Transparent, after the world | All |
| 7 | **`MindDistort`** (Full Screen Pass Renderer Feature, `BeforeRenderingPostProcessing`): DST edge wobble + inverted-state chromatic split | Custom fullscreen | All |
| 8 | **URP post** (uber): Bloom (PC), **Color Lookup = blended LUT with `contribution`**, Color Adjustments, Vignette (already in `Mood.cs`) | URP | All |
| 9 | Paper grain + animated vignette overlay (UI Toolkit/uGUI image with animated mask, like DST's `vig`) | UI | All |

Notes:
- **LUT layering.** Bake the blend per frame, as Klei does. A 1024×32 `RenderTexture` (linear, not sRGB) gets `lerp(lerp(ambA, ambB, tPhase), lerp(insA, insB, tPhase), lutW)` from a tiny blit shader, and is assigned to URP's `ColorLookup.texture`. URP's `ValidateLUT` accepts a 2D RenderTexture whose width is `size²` and height `size` (32 here), as long as it isn't sRGB (verify on 6000.6). This gives DST's three-channel cube system in one URP lookup. Author `office_mon … office_sun` and `office_haunted_day/night` cubes from a neutral strip.
- **Everything full-screen in one pass on WebGL.** Every extra full-screen pass costs a full read and write. `MindDistort` should do distortion, chromatic split and grain in a single fragment, and skip itself when `wobbleW == 0 && inverted == 0` (`SetActive(false)` on the feature).
- **Render Graph.** URP 17 runs on Render Graph. Write any custom `ScriptableRenderPass` with `RecordRenderGraph` (Compatibility Mode is deprecated). The built-in **Full Screen Pass Renderer Feature** + a Fullscreen Shader Graph/HLSL material needs no custom pass code at all. Use it for step 7.
- Add the features to **both** `PC_Renderer` and `Mobile_Renderer`, because WebGL uses Mobile.

### 7.3 Shader list
1. **`Haunted/BillboardSprite`** (HLSL, SRP-Batcher compatible, works with `SpriteRenderer` and with plain quads): view-aligned expansion around the pivot, pivot depth, wind and tremble, lightmap + ambient, `_TintMult`/`_TintAdd` (hit flash), optional normal-map ramp, optional dissolve, per-object fog.
2. **`Haunted/ShadowCreature`**: variant of (1) with point filtering (set on the texture or via `SamplerState sampler_PointClamp`), alpha `mind`-driven, slight UV jitter.
3. **`Haunted/GroundSplat`**: splat + height-blend + world noise + 3 overlay tints + lightmap.
4. **`Haunted/BlobShadow`**: multiply ellipse, height-scaled.
5. **`Haunted/LightQuad`**: generalised-Gaussian falloff, additive, with flicker by radius and intensity tween (Klei: radius 2–5, intensity 0.8, falloff 0.33, warm white).
6. **`Haunted/MindDistort`** (fullscreen): port of DST's distort shader + chromatic split + grain.
7. **`Haunted/LutBlend`** (blit): builds the 1024×32 LUT.

Core of (1), sketched for URP (pivot = object origin; `SpriteRenderer` meshes are in object space with the pivot at 0):
```hlsl
float3 pivotWS = TransformObjectToWorld(float3(0,0,0));
float3 right   = UNITY_MATRIX_V[0].xyz;  // camera right (world)
float3 up      = UNITY_MATRIX_V[1].xyz;  // camera up (world)
float  sway    = (sin(_Time.y*_SwayFreq + dot(pivotWS.xz, 1.7)) * _SwayAmp + _Tremble * hashNoise(_Time.y*40, pivotWS)) * v.uv.y * v.uv.y;
float3 posWS   = pivotWS + right * (v.positionOS.x * _Scale.x + sway) + up * (v.positionOS.y * _Scale.y);
float4 posCS   = TransformWorldToHClip(posWS);
float4 pivCS   = TransformWorldToHClip(pivotWS + up * 0.05);   // tiny lift: sit just in front of the floor
posCS.z        = pivCS.z / pivCS.w * posCS.w;                  // whole card at the pivot's depth
o.lightUV      = (pivotWS.xz - _LightMapExtents.xy) * _LightMapExtents.zw; // DS: light by world XZ
// fragment
half4 c   = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _TintMult;   // premultiplied
c.rgb    += _TintAdd.rgb * c.a;
half3 L   = saturate(SAMPLE_TEXTURE2D(_LightMap, sampler_LinearClamp, i.lightUV).rgb + _Ambient.rgb);
c.rgb    *= max(L, _LightOverride);
```
(If `SpriteRenderer` flipping or `drawMode` complicates object space, drive the demons with a `MeshRenderer` quad and set the UV rect via MaterialPropertyBlock. That's also friendlier to instancing.)

Core of (6), the DST port:
```hlsl
float2 o   = float2(cos(_FxTime*50 + .25), sin(_FxTime*50)) * 0.00625;
float2 uv1 = uv * (1 - 2*0.00625) + 0.00625 + o;
half3 base = SampleBlit(uv), ghost = SampleBlit(uv1);
half  mask = saturate((1 - distance(uv, 0.5) - 0.5) / 0.185);
ghost      = lerp(ghost, base, 1 - _WobbleW);
half3 col  = lerp(ghost, base, mask);
// inverted-pill state: radial chromatic split
float2 dir = (uv - 0.5) * _Inverted * 0.006;
col.r = lerp(col.r, SampleBlit(uv + dir).r, _Inverted);
col.b = lerp(col.b, SampleBlit(uv - dir).b, _Inverted);
```
`_FxTime` is integrated on the CPU (`fxTime += dt * 0.2f * (1 - (1-mind)*(1-mind))`), not computed from `Time.time`. That's how Klei gets a wobble that speeds up smoothly without jumps in phase.

### 7.4 Gameplay ↔ lighting coupling
Compute "is this demon or thought lit?" on the CPU **with the same falloff function and light list** you feed to the lightmap (an analytic sum over at most 10 lights). Don't read back from the GPU. Use DST's hysteresis (lit above 0.075, dark below 0.05) if darkness becomes a mechanic, for example a therapist spotlight that "cleanses" haunts inside its light.

### 7.5 Asset authoring guidelines
- Canvas: draw at about **2× final on-screen size**. With the current camera, a 1.6-unit-tall demon is about 100 px at 1080p and the closest zoom, so author it ~200–320 px tall (≈150–200 PPU). Re-import at 200 PPU and adjust `Demon` scale.
- Lines: black or near-black ink, **≥ 3 px at authoring size** (≥ 1.5 px on screen at the closest zoom), variable width. Fills are flat with a subtle dry-brush texture. No soft airbrushed shading: lighting comes from the lightmap and the optional normal ramp.
- Silhouette first: every character must read as a solid black shape. That's also what the shadow card casts.
- Pivot at the **feet**, bottom centre (already enforced). Keep sprites padded 4 px. Export **straight-alpha PNG** and let Unity premultiply (`alphaIsTransparency`).
- Facings: `Front`, `Side` (facing right, mirrored for left), `Back`. Name them `<Name>_<Part>_<Facing>` for Sprite Library categories.
- Parts for swaps: head, body, arm_L/R, hand_L/R (holding thought), plus a `swap_*` slot for carried items. This mirrors Klei's `swap_object` symbol.
- Normal maps (optional): generate with Laigter batch from the colour art. Hand-paint a height map for faces where the auto result reads dark lines as grooves.
- LUTs: start from a neutral 32³ strip (1024×32, linear, no compression, no mipmaps, clamp, point or bilinear).

### 7.6 Performance checklist (WebGL is the constraint)
- One custom full-screen pass at most. Disable it when idle.
- Lightmap at 256² RGBA8 is plenty, because the floor is small and the falloffs are soft. Draw lights as quads with additive blend, no stencil. Mipmaps off.
- No reliance on `_CameraDepthTexture` in the Mobile tier (`m_RequireDepthTexture: 0`). Fog, soft particles and decals go in object shaders.
- Make sprites batch: one atlas per character set, the SRP Batcher-compatible custom shader, MaterialPropertyBlocks for per-instance tint and tremble, and **Tight** sprite meshes to cut transparent overdraw.
- Real shadows (silhouette cards) only from the sun on WebGL. Blobs everywhere.
- Shadow creatures: at most a handful. They're alpha-blended over the world, so watch overdraw at full-screen size.
- LUT bake: 32k pixels per frame, effectively free. Skip it when weights haven't changed.

### 7.7 Implementation order
1. Blob shadows + custom-axis sorting + pivot-depth billboard shader (the biggest visual win, low risk).
2. `MindFx` scalar, `MindDistort` fullscreen, LUT blend into Color Lookup, Distortion slider.
3. Lightmap pass + `LightQuad` + lit sprites and floor. Then darken the office ambient with gloom (DST night = black ambient).
4. Floor splat with world noise and rot overlay.
5. Shadow creatures and haunt FX (point-filtered, flicker alpha).
6. PC extras: normal-mapped sprites, SDF soft shadows in the lightmap, then radiance cascades.

---

## Sources

**Primary: Klei game code (DST scripts mirror and DS shader source)**
- DST `postprocesseffects.lua` (post chain, uniforms, LUT combine, effect order): https://github.com/taichunmin/dont-starve-together-game-scripts/blob/master/postprocesseffects.lua
- DST `components/colourcube.lua` (season/phase/insanity/lunacy cubes, blend times, sanity curves): https://github.com/taichunmin/dont-starve-together-game-scripts/blob/master/components/colourcube.lua
- DST `components/ambientlighting.lua` (phase ambient colours): https://github.com/taichunmin/dont-starve-together-game-scripts/blob/master/components/ambientlighting.lua
- DST `cameras/followcamera.lua` (FOV, distance↔pitch, heading): https://github.com/taichunmin/dont-starve-together-game-scripts/blob/master/cameras/followcamera.lua
- DST `components/playercontroller.lua` (45° rotation): https://github.com/taichunmin/dont-starve-together-game-scripts/blob/master/components/playercontroller.lua
- DST `constants.lua` (layers, `ANIM_ORIENTATION`, `TILE_SCALE`): https://github.com/taichunmin/dont-starve-together-game-scripts/blob/master/constants.lua
- DST `prefabs/player_common.lua` (DynamicShadow size, LightWatcher thresholds, four-faced): https://github.com/taichunmin/dont-starve-together-game-scripts/blob/master/prefabs/player_common.lua
- DST `prefabs/campfirefire.lua` (light levels): https://github.com/taichunmin/dont-starve-together-game-scripts/blob/master/prefabs/campfirefire.lua
- DST `components/transparentonsanity.lua`, `prefabs/shadowcreature.lua`, `components/shadowcreaturespawner.lua`, `components/sanity.lua`, `tuning.lua`, `easing.lua`, `tilemanager.lua`, `tiledefs.lua`, `worldtiledefs.lua`, `screens/playerhud.lua` (same repo)
- DS shader sources extracted from `.ksh` (`postprocessdistort`, `anim`, `ground`, `ground_overlay`, `lighting`): https://github.com/l8huang/do-not-starve/tree/master/shaders/together
- DST shader uniform list (incl. `CAMERARIGHT`, `LIGHTMAP_WORLD_EXTENTS`, `EROSION_PARAMS`): https://github.com/DST-Arknights/DST-Arknights-Nexus/blob/main/shaderSource/tips.md
- Single-player DS `sanity.lua` (`SetDistortionFactor(outQuad(p))`, radii 0.5/0.685): https://github.com/guliping-hz/Dontstarve_m/blob/master/data/scripts/components/sanity.lua

**Formats and tools**
- ktools (krane/ktech; KTEX defaults, Spriter fidelity limits): https://github.com/nsimplex/ktools/blob/master/README.md
- Extracting animations (anim.bin/build.bin/atlas-0.tex): https://dontstarve.wiki.gg/wiki/How_to_extract_animations_from_the_game
- Klei forum: creating animation bin files / autocompiler: https://kleiforums.com/forums/topic/31817-creating-animation-bin-files/
- Klei forum: anim tools (Flash origins, Spine): https://kleiforums.com/forums/topic/142963-tool-some-animation-tools , https://forums.kleientertainment.com/forums/topic/159394-tool-new-anim-tool/
- Klei forum: colour cubes via postprocessing / custom cubes: https://kleiforums.com/forums/topic/102751-mod-that-disables-insanity-colour-cube/ , https://forums.kleientertainment.com/forums/topic/158145-how-do-you-makegenerate-a-color-cube/

**Art direction**
- Road to the IGF: Klei's *Don't Starve* (Kevin Forbes on the Burton/Gorey/Addams look): https://gamedeveloper.com/design/road-to-the-igf-klei-entertainment-s-i-don-t-starve-i-
- *Don't Starve*: a Tim Burton take on Minecraft: https://gamedeveloper.com/design/-i-don-t-starve-i-a-tim-burton-take-on-i-minecraft-i-
- Seeing the Forest for the Trees: A Don't Starve interview: https://game-wisdom.com/?p=3185

**Unity / URP**
- Full Screen Pass Renderer Feature: https://docs.unity3d.com/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html
- Fullscreen Shader Graph: https://docs.unity3d.com/Manual/urp/prebuilt-shader-graphs-urp-fullscreen.html
- Custom post-processing (low code): https://docs.unity3d.com/Manual/urp/post-processing/post-processing-custom-effect-low-code.html
- Render Graph intro (80.lv): https://80.lv/articles/introduction-to-urp-s-render-graph-in-unity-6/
- Forward/Forward+ light limits: https://docs.unity3d.com/Manual/urp/lighting/light-limits-in-urp.html , https://docs.unity3d.com/Manual/urp/rendering/forward-rendering-paths.html
- Billboard orientation sub-graph example (URP spritesheets): https://github.com/Gaxil/Unity-URP-Spritesheets
- Godot billboard node (cylindrical vs view-aligned reference): https://docs.godotengine.org/en/stable/classes/class_visualshadernodebillboard.html

**Modern techniques**
- Radiance Cascades walkthrough (jason.today): https://jason.today/rc (and its predecessor on JFA/distance-field GI, https://jason.today/gi)
- Holographic Radiance Cascades for 2D GI (Freeman, Sannikov, Margel 2025): https://arxiv.org/abs/2505.02041
- Exploration and Optimization of Radiance Cascades (Chalmers thesis): https://odr.chalmers.se/items/3ee9fb4e-1880-46c2-802a-a660e38dc9ee
- Laigter normal-map generator: https://azagaya.itch.io/laigter , docs https://laigter.readthedocs.io/en/latest/Introduction/intro.html
- Texture repetition / anti-tiling (Íñigo Quílez): https://iquilezles.org/articles/texturerepetition/
- Heitz & Neyret, *High-Performance By-Example Noise using a Histogram-Preserving Blending Operator* (HPG 2018), the basis of stochastic texturing.
