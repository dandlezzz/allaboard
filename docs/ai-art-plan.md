# AI Art Plan — *Trafalgar — Age of Sail* (Web / PixiJS 8)

**Goal:** dramatically raise the visual quality of the live browser port (`web/`,
PixiJS 8 + TypeScript + Vite) by replacing today's fully-procedural
`Graphics` art with cohesive, hand-quality **textured sprites** generated with
AI tools — ships, sea, effects, and UI — while keeping the procedural renderer
as a fallback and staying inside the board.fun `.webapp.zip` asset budget.

> **Scope note:** This is a *research + planning* document. No game code is
> changed here. All code blocks are *proposed* sketches.

> **Convention used below:** lines marked **[FACT]** are confirmed from a cited
> source; lines marked **[REC]** are my recommendation/inference for this
> project. Sources are linked inline and collected at the bottom.

---

## 0. Where we are today (grounding)

Current art is 100% procedural PixiJS `Graphics`, drawn in **world units** inside
a single scaled "camera" container:

- **Ships** — `web/src/rendering/shipView.ts` builds a layered wooden tall-ship
  per `Container`: hull + rail polygon, a **white** faction "gun-stripe"
  silhouette that is `tint`-ed per faction, planked deck, hatches/gratings,
  capstan, masts, bowsprit + figurehead, cannons, rope rigging, translucent
  sails that scale with throttle, a tintable stern flag, plus flat-on-sea
  status rings, on-ring control buttons, and floating hull/rigging bars.
- **Sea** — `web/src/rendering/scene.ts` is a solid light-blue rectangle
  (`0x6ba8db`) with a thin frame.
- **Effects** — `web/src/rendering/renderer.ts` draws cannon tracers as small
  rects and smoke as fading grey circles; projectiles are simple tracers.
- **UI** — `web/src/ui/hud.ts` + `web/index.html`: a DOM HUD with an inline-SVG
  wind arrow, fleet readouts, and a banner. Shot-type/wind/sail icons inside the
  ship rings are drawn procedurally in `shipView.ts`.

Key facts that constrain the art pipeline (from the code):

| Constraint | Value | Where |
|---|---|---|
| PixiJS version | `^8.6.6` | `web/package.json` |
| Coordinate convention | bow toward **+Z**, mapped to screen **−Y** (so bow = "up") | `geometry.ts`, `shipView.ts` `lp()` |
| Ship container transform | `rotation = headingDeg`, position = world | `shipView.ts` `syncTransform()` |
| Faction accent | British = blue `rgb(0.2,0.45,0.85)`, Franco-Spanish = orange `rgb(0.9,0.55,0.2)`; applied via `tint` on a **white** stripe/flag | `core/faction.ts`, `shipView.ts` |
| Ship classes (length × beam, world units) | Frigate ≈ **87.5 × 20.5**, Third-rate ≈ **122.5 × 29.9**, First-rate ≈ **148.75 × 36.2** (≈ 4:1 long:beam) | `ships/shipClass.ts` (`ShipScale=10`, `HullSizeBoost=1.75`) |
| Target frame | 16:9, board.fun tabletop **1920×1080** + desktop browser | brief |
| Asset packaging | board.fun web app → `.webapp.zip` → **payload size matters** | brief / AGENTS.md |
| Existing asset folder | `web/public/assets/` (only `.gitkeep`) | repo |

**Implication for orientation/scale:** because the ship `Container` already
rotates by heading and the world container handles world→screen scaling, every
ship sprite should be authored **bow pointing straight up** (toward −Y), centered,
and we simply scale it so its long axis spans `stats.length` world units. This
keeps AI-art integration a near drop-in for the procedural `ShipView`.

**Target quality:** `_refs/ship_topdown_reference.png` — a richly painted
top-down wooden hull (visible plank grain, rope rails, deck fittings, soft sail
shadow, painterly water). That is the bar: *painterly, warm wood, readable from
overhead, ~4:1 hull.* Note the reference is at a slight diagonal — **our** sprites
must be axis-aligned bow-up so engine rotation reads correctly.

---

## 1. Recommended toolchain (per asset class)

Summary recommendation: **standardize on Scenario** as the primary ship/sea/FX
generator (it is the most game-pipeline-native: top-down styles, transparent
output, sprite-sheet export, custom style models, and an API), use **Recraft V4**
for crisp **vector** UI/icons, and keep a **local ComfyUI + SDXL/Flux**
escape-hatch for fine pose control and zero per-asset cost. Background removal
and packing are open-source (**rembg/BiRefNet** + **free-tex-packer**).

| Asset class | Primary | Alternative | Why it fits PixiJS + our top-down style |
|---|---|---|---|
| **Ships** (per class/faction + damaged/sinking) | **Scenario** custom style model (LoRA) → `topdown_asset` style + `removeBg` | **ComfyUI + SDXL + LayerDiffuse + ControlNet** (local, free, max control) | Scenario trains on 5–15 refs for **consistent** top-down hulls with transparent PNGs out of the box; LoRA locks the painterly wood look across all 3 classes × 2 factions and damage states. [FACT: Scenario top-down styles + `removeBg`/sprite-sheet output and custom-model training](https://docs.scenario.com/get-started/generation/third-party-model-generation/third-party-model-generation-retro-diffusion) |
| **Sea / water** (seamless tile + optional animated frames) | **Scenario** tilemap model (`single_tile` / `tileset`) | **SDXL "seamless" LoRA** locally; or a single large painted background | Scenario's tilemap model is *built for seamless, grid-aligned tiles*; a tiled PNG drives a Pixi `TilingSprite`. [FACT: dedicated top-down tilemap model with seamless tile styles](https://docs.scenario.com/get-started/generation/third-party-model-generation/third-party-model-generation-retro-diffusion) |
| **Effects** (smoke, muzzle flash, splash, wake, shot) | **Scenario RD Animation** (`vfx` style → `returnSpritesheet`) | Hand-tuned **SDXL** puffs on transparent bg; or particle textures | RD Animation outputs **grid-aligned VFX sprite sheets** that import straight into a Pixi `AnimatedSprite`. [FACT: RD Animation `vfx` style, `returnSpritesheet`](https://docs.scenario.com/get-started/generation/third-party-model-generation/third-party-model-generation-retro-diffusion) |
| **UI / icons** (round vs bar shot, wind arrow, sail ±, flags, buttons) | **Recraft V4 / V4.1 Vector** → SVG | Scenario `skill_icon` / `ui_element` raster; Recraft raster fallback | Recraft is the only major model that emits **native, editable SVG** (real paths) and generates **cohesive sets of up to 6** with a shared style + HEX palette — ideal for crisp-at-any-size HUD icons that Pixi loads via `loadSvg`. [FACT: Recraft V4 native SVG, image sets, HEX palettes](https://www.recraft.ai/docs/recraft-models/recraft-V4) |
| **Background removal** (cutouts) | **rembg** w/ `birefnet-general` (local, free, MIT BiRefNet) | **BRIA RMBG-2.0 API** (managed, licensed) or remove.bg | Free, scriptable, batch; BiRefNet is current open SOTA for clean edges/rope. [FACT: rembg backends incl. BiRefNet; BiRefNet open SOTA](https://www.bestaiweb.ai/how-to-build-a-production-background-removal-pipeline-with-bria-rmbg-2-0-photoroom-api-and-rembg-in-2026/) |
| **Sprite packing** | **free-tex-packer-core** (`exporter: "Pixi"`) | **TexturePacker** (paid; supports anchors/9-slice/animation groups) | Emits Pixi-ready atlas JSON; MIT; CLI/programmatic. TexturePacker adds anchor + animation-group metadata Pixi understands. [FACT: free-tex-packer Pixi exporter; TexturePacker anchor/animation support](https://pixijs.download/v8.18.0/docs/assets.Spritesheet.html) |

### Licensing & cost notes [FACT unless marked]

- **Scenario** — Free tier 50 daily credits (eval/personal only). Paid plans
  **Starter $15/mo (1,500 CU)**, **Pro $45/mo (5,000 CU, includes API + custom
  Flux training)**, **Max $75/mo (10,000 CU)**. **All paid plans grant a full
  commercial license — you own and may ship the assets.** Image gen ≈ 2–15 CU;
  model training ≈ 100–500 CU. [pricing](https://www.scenario.com/pricing) ·
  [API CU](https://help.scenario.com/articles/7934059476-api-usage-and-credits-compute-units)
  → **[REC]** Pro plan, so we can train a custom "Trafalgar wood" model and script
  batch gen via API.
- **Recraft** — Free tier (50/day, **public + Recraft-owned, no commercial
  rights**). Paid **Basic $10/mo (1,000 credits, commercial rights + private)**.
  API: **$0.04/raster, $0.08/vector** image. [pricing](https://www.recraft.ai/pricing) ·
  [review](https://aipedia.wiki/tools/recraft/) → **[REC]** Basic plan; UI icon
  set is tiny, well within 1,000 credits.
- **Midjourney** — No transparent output (always solid bg; needs Smart Select /
  external cutout). `--sref` style ref + `--oref`/`--ow` omni-ref for consistency.
  Paid subscribers get commercial rights; **Steam now allows AI assets with
  disclosure**; pure AI output may be hard to copyright. [Omni-Ref](https://docs.midjourney.com/hc/en-us/articles/36285124473997-Omni-Reference) ·
  [licensing/Steam](https://aidevdayindia.org/blogs/ai-gaming-world/midjourney-sora-game-asset-workflow.html)
  → **[REC]** great for *moodboard/art-bible* exploration, weaker for production
  cutouts; not the primary tool.
- **Stable Diffusion / SDXL / Flux (local, ComfyUI)** — Free, open weights, no
  per-asset cost, full control (ControlNet pose, LoRA identity, LayerDiffuse
  transparency on SDXL/SD1.5). Needs ~12 GB+ VRAM. Flux has stronger multi-view
  but **no LayerDiffuse → generate on solid bg + rembg**. [transparency/ControlNet guide](https://apatero.com/blog/generate-game-assets-consistency-transparent-backgrounds-2025) ·
  [Flux Canny game-asset workflow](https://runware.ai/docs/models/flux-1-dev/guides/game-assets-canny)
  → **[REC]** keep as the "free + maximal control" fallback; check the licence of
  each checkpoint/LoRA you pull from CivitAI before shipping.
- **BiRefNet** weights: MIT (open SOTA). **BRIA RMBG-2.0**: CC BY-NC; commercial
  use needs a paid BRIA licence or the BRIA API. **remove.bg**: SaaS, ~$0.23–0.27/
  image. [bg-removal pricing](https://www.bestaiweb.ai/how-to-build-a-production-background-removal-pipeline-with-bria-rmbg-2-0-photoroom-api-and-rembg-in-2026/)

---

## 2. Style-consistency strategy (the Art Bible)

Consistency is the make-or-break for AI art at this scale. Two layers of control:

### 2a. Write an Art Bible (`docs/art-bible.md`, ~1 page) — **[REC]**

Lock these so every prompt and every reviewer references the same target:

- **Camera:** strict orthographic **top-down (zenith / 90°)**, no perspective,
  ship's bow pointing **straight up**, centered, ~4:1 long:beam.
- **Palette (from the existing code, keep continuity):** warm hull woods
  `#573821 / #735133 / #b88a52`, sail canvas `#efe7d4`, iron `#17171c`, gold trim
  `#dbb351`, sea base `#6ba8db`/deep `#14283b`. Faction accents: British blue
  `#3373d9`, Franco-Spanish orange `#e68c33` (these match `accentColor()`).
- **Rendering style:** painterly-but-readable, soft single top-light, gentle drop
  shadow under the hull onto the water, visible plank grain + rope rails (match
  `_refs/ship_topdown_reference.png`).
- **Scale rules:** Frigate < Third-rate < First-rate; gun-port count and mast
  count per class match `shipClass.ts` (Frigate 2 masts, others 3).
- **What is "variable" vs "identity":** damage/sail-state/faction are variable;
  hull silhouette, wood treatment, lighting are identity. (This phrasing matters
  for LoRA captioning — see below.)

### 2b. Technical consistency controls

Ordered by strength (use as many as the tool supports):

1. **Custom style model / LoRA (strongest).** Train one **Scenario style model**
   on 5–15 curated reference images (the `_refs` image + a handful of approved
   first-generations). Every later generation "inherits the visual DNA." Studios
   use exactly this to ship thousands of on-style assets. [FACT: train on 5–15 refs for consistency](https://www.scenario.com/blog/ai-sprite-generator) ·
   [FACT: Scenario consistency at scale](https://www.scenario.com/blog/Why-Your-AI-Game-Generator-Keeps-Letting-You-Down).
   In captions, vary pose/faction/damage but keep style notes constant so the
   model learns *wood+lighting = identity*, *faction/damage = variable*. [FACT: captioning identity vs variable](https://help.scenario.com/articles/4280773511-train-a-consistent-character-model)
2. **Fixed seed + locked sampler** per asset family for reproducible re-rolls.
   [FACT: seed locking for reusable consistency](https://selfielab.me/blog/flux-sprite-sheets-instant-game-character-grids-20260217)
3. **Style reference images** (`--sref` in MJ; reference images in Scenario;
   "style" in Recraft) when not using a full LoRA. [FACT: MJ `--sref` style lock](https://docs.midjourney.com/hc/en-us/articles/36285124473997-Omni-Reference)
4. **Shared HEX palette** in Recraft for the whole icon set. [FACT](https://www.recraft.ai/blog/how-to-create-image-sets)
5. **ControlNet (Canny/Depth)** locally to force the *same hull silhouette* across
   faction/damage variants — feed the procedural hull outline as the control map.
   [FACT: ControlNet Canny/Depth for structural consistency](https://apatero.com/blog/generate-game-assets-consistency-transparent-backgrounds-2025)

**[REC] One identity per class, faction as an overlay.** Generate **one neutral
wood hull per class** (no faction colour), then drive British/Franco-Spanish from
**tintable overlay layers** (gun-stripe band, sails, flag) rather than generating
6 separate hulls. This halves ship assets, guarantees the two factions are pixel-
identical except colour, and mirrors how `shipView.ts` already tints a white
stripe. Generate damage as a separate transparent **overlay** (scorch/holes) too.

---

## 3. Production workflow

```
                          ┌─────────────────────────────────────────────┐
  Art Bible + refs  ──▶   │ 1. GENERATE (Scenario style-model / Recraft) │
                          └───────────────┬─────────────────────────────┘
                                          ▼
                          ┌─────────────────────────────────────────────┐
                          │ 2. CUTOUT  (Scenario removeBg, or rembg)     │  ← transparent PNG
                          └───────────────┬─────────────────────────────┘
                                          ▼
                          ┌─────────────────────────────────────────────┐
                          │ 3. NORMALIZE (crop, center, rotate bow-up,   │  ← ImageMagick/sharp script
                          │    resize to per-class px, drop shadow)      │
                          └───────────────┬─────────────────────────────┘
                                          ▼
                          ┌─────────────────────────────────────────────┐
                          │ 4. PACK (free-tex-packer → Pixi atlas JSON)  │  ← + WebP compress
                          └───────────────┬─────────────────────────────┘
                                          ▼
                          ┌─────────────────────────────────────────────┐
                          │ 5. LOAD (Pixi Assets.load → Sprite/Anim)     │
                          └─────────────────────────────────────────────┘
```

### Step 1 — Generation prompt templates

**Ship hull (per class, neutral wood — Scenario, with style model applied):**

```
top-down orthographic view of an 18th-century wooden [frigate | 74-gun third-rate
ship-of-the-line | 100-gun first-rate flagship], bow pointing straight up,
centered, symmetrical, full ship visible, [2 | 3] masts furled, planked deck,
rope rigging and rail netting, gun ports along both sides, painterly warm oak
wood, soft top light, subtle shadow, game asset, transparent background
--- style: topdown_asset   removeBg: true   seed: 70805
negative: perspective, tilt, angle, people, ocean, frame, text, watermark
```
*(Generate Third-rate first as the "golden" hull; reuse seed, swap class words.)*

**Faction stripe / sail / flag overlay (white, for tinting):** generate the same
hull silhouette but output **only** the gun-stripe band / sails / flag as a white
shape on transparent — or, simpler, **author these procedurally** (we already do)
and keep them as the tint layer. **[REC]** keep stripe+flag procedural; generate
*sails* as art (they read large).

**Damage overlay:** `top-down damaged wooden ship deck overlay, scorch marks,
splintered planks, holes, smoke stains, transparent background, matches [class]
hull silhouette` → align via ControlNet to the clean hull.

**Sea tile (Scenario tilemap model):**

```
seamless tileable top-down ocean water, deep blue-teal, gentle swell and foam
flecks, painterly, no horizon, no objects --- style: single_tile  (seamless)
seed: 70805
```

**VFX (Scenario RD Animation, `vfx` style, `returnSpritesheet: true`):**
`cannon muzzle smoke puff, white-grey, expanding, top-down, vfx` ·
`water splash from cannonball impact, top-down, vfx` ·
`ship wake foam trail, top-down, looping`.

**UI icons (Recraft V4 Vector, one image-set of 6, shared palette):**

```
set of 6 minimalist game UI icons, flat, front-facing, uniform 2px strokes,
monochrome white on transparent, naval theme:
1) cannonball (round shot)  2) bar shot (two balls joined by a bar)
3) wind direction arrow  4) sail-up (plus)  5) sail-down (minus)  6) naval flag
palette: #f5f7ff   format: SVG
```

### Step 2 — Cutout

Prefer in-tool transparency (Scenario `removeBg`, [FACT](https://docs.scenario.com/get-started/generation/third-party-model-generation/third-party-model-generation-retro-diffusion)).
For anything generated on a solid bg (Midjourney/Flux), batch with **rembg**:

```bash
pip install "rembg[cpu,cli]"          # or [gpu,cli] with CUDA   (pin >= 2.0.75)
rembg p -m birefnet-general ./raw ./cut   # folder→folder, BiRefNet edges
```
[FACT: rembg install + BiRefNet backend](https://www.bestaiweb.ai/how-to-build-a-production-background-removal-pipeline-with-bria-rmbg-2-0-photoroom-api-and-rembg-in-2026/)

### Step 3 — Normalize (orientation, scale, naming) — proposed `scripts/normalize_ship.mjs`

The single most important step for a top-down RTS: **every** ship sprite must be
bow-up, centered, trimmed, and sized to a fixed px-per-class so in-engine scaling
stays consistent.

```js
// node scripts/normalize_ship.mjs  (uses `sharp`)
import sharp from "sharp";
// Per-class target sprite heights (px) — proportional to world length so all
// classes share one px-per-world-unit. (Third-rate 122.5wu -> 768px => 6.27 px/wu)
const TARGET_H = { frigate: 549, thirdrate: 768, firstrate: 933 };
async function normalize(src, out, cls) {
  await sharp(src)
    .trim()                              // crop transparent margins
    .rotate(/* deg so bow points up; pre-rotate in gen instead when possible */)
    .resize({ height: TARGET_H[cls], fit: "contain",
              background: { r:0, g:0, b:0, alpha:0 } })
    .png().toFile(out);
}
```

Keep a fixed **px-per-world-unit** (≈6.27 in the example) so a First-rate sprite
is visibly bigger than a Frigate without per-asset fudging. Author bow-up at
generation time when you can (prompt "bow pointing straight up"); only rotate as
cleanup.

### Step 4 — Pack into a Pixi atlas

```js
// node scripts/pack.mjs
import packer from "free-tex-packer-core";
import { readFileSync, writeFileSync } from "node:fs";
const images = [/* {path,contents} for each normalized PNG */];
packer(images, {
  textureName: "ships",
  width: 2048, height: 2048, padding: 2,
  allowRotation: false, allowTrim: true, detectIdentical: true,
  exporter: "Pixi",            // emits PixiJS-ready atlas JSON
  removeFileExtension: true,
}, (files) => files.forEach(f =>
     writeFileSync(`web/public/assets/atlas/${f.name}`, f.buffer)));
```
[FACT: free-tex-packer `exporter: "Pixi"`, trim/detectIdentical/OptimalPacker](https://github.com/odrick/free-tex-packer-core).
Then convert the PNG atlas to **WebP/AVIF** for size (Pixi loads both;
`meta.image` can point to a `.webp`). Keep `keepProcedural` fallback (see §4).

### Step 5 — Load in Pixi (see §4 for the full integration).

**Keeping orientation & per-class scale consistent (recap):** author bow-up →
trim+center → fixed px-per-world-unit per class → set sprite scale from world
`stats.length`. The engine's existing `container.rotation = headingDeg` then
handles all in-game rotation; sprites never need runtime re-orientation.

---

## 4. PixiJS integration plan

### 4a. Folder structure (`web/public/assets/`)

```
web/public/assets/
  atlas/
    ships.webp        ships.json        # hulls (per class) + damage/sail overlays
    fx.webp           fx.json           # smoke/splash/muzzle/wake frames
  sea/
    water_tile.webp                     # seamless, for TilingSprite
  ui/
    shot_round.svg  shot_bar.svg  wind.svg  sail_up.svg  sail_down.svg
    flag_british.svg  flag_franco.svg
  manifest.json                         # optional Assets bundle manifest
```

### 4b. Loading (PixiJS 8 `Assets`)

```ts
// web/src/rendering/assets.ts  (proposed)
import { Assets, Spritesheet, Texture } from "pixi.js";

export interface GameArt {
  ships: Spritesheet;
  fx: Spritesheet;
  water: Texture;
  ui: Record<string, Texture>;
}

export async function loadArt(): Promise<GameArt | null> {
  try {
    const [ships, fx, water] = await Promise.all([
      Assets.load<Spritesheet>("/assets/atlas/ships.json"),
      Assets.load<Spritesheet>("/assets/atlas/fx.json"),
      Assets.load<Texture>("/assets/sea/water_tile.webp"),
    ]);
    const uiNames = ["shot_round","shot_bar","wind","sail_up","sail_down",
                     "flag_british","flag_franco"];
    const uiTex = await Assets.load<Texture>(
      uiNames.map(n => `/assets/ui/${n}.svg`));
    const ui: Record<string,Texture> = {};
    uiNames.forEach(n => ui[n] = uiTex[`/assets/ui/${n}.svg`]);
    return { ships, fx, water, ui };
  } catch (e) {
    console.warn("[art] textured assets missing; using procedural fallback", e);
    return null;   // <- triggers the existing Graphics path
  }
}
```
[FACT: `Assets.load(json)` returns a `Spritesheet`; `sheet.textures[name]`,
`sheet.animations[name]`; SVG loads as a Texture](https://pixijs.com/8.x/guides/components/assets).

### 4c. Swapping `Graphics` → `Sprite` with minimal disruption

The cleanest seam: a **boolean + a base-layer swap** inside the *existing*
`ShipView`, not a rewrite. `ShipView` already adds a `hullGfx` and `deckGfx` to
`this.container`. Replace those two with **one textured `Sprite`** when art is
loaded; keep all the dynamic procedural bits (selection ring, status rings,
control buttons, bars — these are data/HUD, not "art").

```ts
// inside ShipView constructor (sketch)
const art = renderer.art;                       // GameArt | null, loaded once
const useTex = !!art;
if (useTex) {
  const key = `hull_${ShipClass[ship.stats.shipClass].toLowerCase()}`;
  const hull = new Sprite(art!.ships.textures[key]);
  hull.anchor.set(0.5);
  // world length -> local px: sprite authored bow-up, scale to stats.length
  const pxPerWorld = hull.texture.height / WORLD_LEN_OF[ship.stats.shipClass];
  hull.scale.set(1 / pxPerWorld);               // 1 sprite-px == 1 world-unit
  this.container.addChild(this.selectionGfx, this.statusContainer,
                          this.controlsContainer, hull);
  this.buildTexSails(art!);                      // sail Sprites (tintable)
  this.buildTexOverlays(art!);                   // stripe/flag/damage Sprites
} else {
  /* ...existing procedural buildHull/buildDeck/... path unchanged... */
}
```

### 4d. Mapping dynamic state onto sprites

| Dynamic state | Procedural today | Textured approach |
|---|---|---|
| **Faction colour** | `tint` on white stripe + flag | Keep stripe/flag as **white overlay `Sprite`s**, set `.tint = accentColor(faction)`. Hull stays a shared neutral texture. ([Sprite tinting is per-display-object in Pixi]) |
| **Reefing sails** (throttle) | scale sail `Graphics` | Either keep the same `sail.scale.set(...)` on a sail **`Sprite`**, **or** swap among `sails_furled/half/full` atlas frames. Start with scale (zero new art). |
| **Damage** | hit-flash + smoke | Cross-fade a **damage overlay `Sprite`**, `alpha = 1 - hullFraction`; keep the white flash `Sprite` and `spawnSmoke`. |
| **Selection / rings / bars / buttons** | `Graphics` | **Leave procedural** — they are dynamic HUD, cheap, and recolour per selector. (Optionally skin button discs with a UI texture later.) |
| **Sinking** | alpha+scale ramp | Unchanged (`updateSinking` operates on the container). |
| **Sea** | solid rect | `TilingSprite(art.water, w, h)` in `seaLayer`; optionally scroll `tilePosition` slowly for life, or swap N frames for animated water. |
| **Tracers / smoke** | rects/circles | Swap fill calls for `fx` atlas frames / `AnimatedSprite`; keep the same spawn/update loop in `renderer.ts`. |

> **Why not just `tint` the whole hull per faction?** `tint` multiplies the whole
> texture and would muddy the wood. Tinting a **separate white overlay** (stripe/
> flag/sails) keeps wood neutral and matches the current design intent.

### 4e. Asset size budget for `.webapp.zip` — **[REC]**

Target **≤ 4 MB total** added art (sprites compress well; ships dominate).

| Bundle | Contents | Format | Budget |
|---|---|---|---|
| `ships` atlas | 3 hull textures + sails + damage + flag overlays, packed | WebP, 2048² | ~0.8–1.5 MB |
| `fx` atlas | smoke/splash/muzzle/wake frames | WebP | ~0.3–0.6 MB |
| `water_tile` | one 512² seamless tile | WebP | ~50–150 KB |
| `ui` | 7 SVGs | SVG (vector, tiny) | ~20–60 KB |

Tactics: one shared hull per class (faction via tint) instead of 6; **WebP/AVIF**
over PNG; `detectIdentical` + `allowTrim` in the packer; 512² seamless water via
`TilingSprite` instead of a giant background; SVG (not PNG) for UI so it's both
tiny and crisp at 1080p. Lazy-load the `fx` atlas after first paint if needed.

---

## 5. Phased roadmap

Effort estimates assume one developer comfortable with the codebase; "art time"
is generation+cleanup, "code time" is integration.

### Phase 0 — Golden-asset spike (½–1 day) — **do this first**
- **Goal:** validate the *entire* pipeline on **one asset**: the **Third-rate
  hull**. Generate in Scenario (style model trained on `_refs/…png` + 4 quick
  approved gens) → `removeBg` → normalize → pack a 1-frame atlas → load via
  `Assets` → drop a single textured `Sprite` behind one ship, bow-up, scaled to
  `stats.length`, with the procedural version as fallback.
- **Exit criteria:** the textured Third-rate sits correctly, rotates with heading,
  matches scale, and looks like the reference. If yes, the architecture in §4 is proven.

### Phase 1 — UI icons + sea (1–2 days)
- **Why first (post-spike):** lowest risk, highest perceived polish, tiny payload.
  Recraft V4 SVG set of 6 icons + 1 wind arrow + 2 flags; one Scenario seamless
  water tile → `TilingSprite`.
- Code: wire `ui` textures into the HUD (`index.html` SVG swap is trivial) and the
  ammo/sail icons in `shipView.ts`; replace `scene.ts` rect with `TilingSprite`.

### Phase 2 — Ships (3–5 days)
- Train/confirm the Scenario ship style model; generate **3 neutral hulls** +
  **sail overlay** + **damage overlay**; pack `ships` atlas; build `buildTexSails`
  / `buildTexOverlays`; keep `keepProcedural` flag. Validate all 3 classes × 2
  factions (tint) + damage + sinking.

### Phase 3 — Effects & animation (2–4 days)
- Scenario RD Animation `vfx` sheets for smoke/splash/muzzle/wake; swap
  `renderer.ts` tracer/smoke draws for `AnimatedSprite`/atlas frames; optional
  animated water frames.

### Phase 4 — Polish & optimization (1–2 days)
- AVIF/WebP tuning, atlas trimming, lazy-load fx, AI-content disclosure note for
  board.fun/store, archive prompts+seeds+style-model id in `docs/art-bible.md`.

**Recommended first concrete experiment:** *the Phase 0 golden Third-rate hull.*
One asset, end-to-end, proves generation → cutout → normalize → pack → Pixi load →
correct orientation/scale/fallback before any bulk generation spend.

---

## 6. Risks / limitations & mitigations

| Risk | Likelihood | Mitigation |
|---|---|---|
| **Style drift** across many ships/states | High without controls | Train one Scenario style model (LoRA) + fixed seed; generate neutral hull once and derive factions by tint, not re-gen. [FACT](https://www.scenario.com/blog/ai-sprite-generator) |
| **Orientation wrong** (AI loves perspective/tilt) | High | Prompt "top-down orthographic, bow straight up"; negative-prompt "perspective/tilt"; ControlNet the procedural hull outline; final rotate in normalize step. [FACT: ControlNet structure lock](https://apatero.com/blog/generate-game-assets-consistency-transparent-backgrounds-2025) |
| **Per-class scale inconsistency** | Medium | Fixed px-per-world-unit table; scale sprite from `stats.length` at load (§3/§4). |
| **Imperfect cutouts** (rigging/rope edges) | Medium | BiRefNet via rembg handles thin features; budget manual touch-up in GIMP/Photoshop for hero hulls. [FACT: BiRefNet edge quality](https://dev.to/om_prakash_3311f8a4576605/birefnet-vs-rembg-vs-u2net-which-background-removal-model-actually-works-in-production-2nj3) |
| **Asset payload bloats `.webapp.zip`** | Medium | One hull/class + tint overlays; WebP/AVIF; trim/detectIdentical; seamless tile + `TilingSprite`; SVG UI; ≤4 MB budget (§4e). |
| **Licensing / copyright** | Medium | Use **paid** Scenario/Recraft tiers (commercial license, you own output); vet any CivitAI checkpoint/LoRA licences; disclose AI use where required (e.g. Steam). [FACT: Scenario paid = full commercial license](https://www.scenario.com/pricing) · [FACT: MJ/Steam disclosure & copyright caveats](https://aidevdayindia.org/blogs/ai-gaming-world/midjourney-sora-game-asset-workflow.html) |
| **Tinting muddies wood** | Medium | Tint only white overlays (stripe/sail/flag), never the hull texture (§4d). |
| **Vendor lock-in / ongoing cost** | Low–Med | Local ComfyUI + SDXL + LayerDiffuse fallback gives a zero-cost, fully-owned path if Scenario costs grow. [FACT](https://apatero.com/blog/open-source-sprite-generation-ai-complete-guide-2025) |
| **Hand-fixups exceed time savings** | Low | Golden-asset spike (Phase 0) measures real cleanup time before committing to bulk gen. |

---

## Appendix — Sources

**Game-asset generators**
- Scenario top-down/sprite/tilemap models, `removeBg`, `returnSpritesheet`: https://docs.scenario.com/get-started/generation/third-party-model-generation/third-party-model-generation-retro-diffusion
- Scenario consistency / custom models: https://www.scenario.com/blog/ai-sprite-generator · https://www.scenario.com/blog/Why-Your-AI-Game-Generator-Keeps-Letting-You-Down · https://help.scenario.com/articles/4280773511-train-a-consistent-character-model
- Scenario pricing & API CU: https://www.scenario.com/pricing · https://help.scenario.com/articles/7934059476-api-usage-and-credits-compute-units
- Layer.ai sprite/style training/export: https://www.layer.ai/use-cases/sprite-generation · https://www.layer.ai/features/style-training · https://www.layer.ai/tools/layer--generate-a-sprite-sheet
- Recraft V4 vector/icons/sets/pricing: https://www.recraft.ai/docs/recraft-models/recraft-V4 · https://www.recraft.ai/generate/icons · https://www.recraft.ai/blog/how-to-create-image-sets · https://www.recraft.ai/pricing · https://aipedia.wiki/tools/recraft/
- Midjourney omni/style ref + licensing/Steam: https://docs.midjourney.com/hc/en-us/articles/36285124473997-Omni-Reference · https://aidevdayindia.org/blogs/ai-gaming-world/midjourney-sora-game-asset-workflow.html

**Open / local pipeline**
- SDXL + LayerDiffuse + ControlNet transparent assets: https://apatero.com/blog/generate-game-assets-consistency-transparent-backgrounds-2025
- Flux ControlNet-Canny game-asset workflow: https://runware.ai/docs/models/flux-1-dev/guides/game-assets-canny
- Open-source sprite stack: https://apatero.com/blog/open-source-sprite-generation-ai-complete-guide-2025
- Flux sprite-sheet grids / seed locking: https://selfielab.me/blog/flux-sprite-sheets-instant-game-character-grids-20260217

**Background removal**
- BRIA/rembg/Photoroom pipeline + pricing: https://www.bestaiweb.ai/how-to-build-a-production-background-removal-pipeline-with-bria-rmbg-2-0-photoroom-api-and-rembg-in-2026/
- BiRefNet vs rembg vs U2Net: https://dev.to/om_prakash_3311f8a4576605/birefnet-vs-rembg-vs-u2net-which-background-removal-model-actually-works-in-production-2nj3
- BRIA RMBG-2.0 (CC BY-NC + API): https://huggingface.co/briaai/RMBG-2.0

**Packing & PixiJS**
- free-tex-packer (Pixi exporter): http://free-tex-packer.com/ · https://github.com/odrick/free-tex-packer-core · https://github.com/odrick/free-tex-packer-cli
- PixiJS 8 Assets + Spritesheet: https://pixijs.com/8.x/guides/components/assets · https://pixijs.download/v8.18.0/docs/assets.Spritesheet.html · https://github.com/pixijs/pixijs-skills/blob/main/skills/pixijs-assets/references/spritesheet.md
