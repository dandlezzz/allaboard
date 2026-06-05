# Sound Effects Plan — *Trafalgar — Age of Sail*

> A concrete implementation plan for adding sound effects to the web game
> (`web/`, Vite + TypeScript + PixiJS, runs in the browser and on Board
> hardware via the Board Web SDK). This is a **plan only** — no game code is
> changed by this document.
>
> Scope of the first milestone: **two SFX** — *cannon fire* (a broadside/chase
> volley) and *impact* (a ball hitting a hull, distinct from a near-miss
> splash). Everything else (wind, sinking, UI clicks) is deferred to Phase 3.
>
> Conventions follow `web/AGENTS.md`: ESM-only, gate every Board SDK call on
> `Board.isOnDevice`, keep the engine-agnostic combat code free of renderer/DOM
> dependencies, and mind the `.webapp.zip` bundle budget.

---

## TL;DR

- **Tech:** use the **Web Audio API** (not `HTMLAudioElement`) — decode each clip
  once into an `AudioBuffer`, then fire cheap one-shot `AudioBufferSourceNode`s
  through a small per-category gain graph. This is the only option that handles
  many overlapping one-shots with low latency.
- **Unlock:** an `AudioContext` starts `suspended` and must be resumed from a
  user gesture. We already have one at exactly the right moment — the **Setup
  placement tap** (`handleSetupContact` in `web/src/core/game.ts`) — use it to
  `audioCtx.resume()`.
- **Two trigger points (the deliverable hooks):**
  - **Cannon fire** — once per volley in `web/src/combat/combatSystem.ts`, right
    where the volley is committed: after `shooter.notifyFired(side)`
    (combatSystem.ts:54, broadside) and after `shooter.notifyChaseFired(bow)`
    (combatSystem.ts:105, chase). **One shot per volley, never per ball.**
  - **Impact** — tied to the projectile that already carries a hit/miss outcome.
    Each ball is spawned via `effects.spawnProjectile(origin, impactPoint(target, hit), …)`
    (combatSystem.ts:74 and :124). Extend that call to also carry the outcome so
    the renderer plays a **hit thud** vs a **near-miss splash** when the tracer
    *lands* (timing-accurate), and throttle it so a 32-ball volley doesn't stack
    32 impacts.
- **Plumbing:** add a `playSound(...)` method to the existing `Effects` interface
  (`web/src/combat/effects.ts`), implemented by the `Renderer`
  (`web/src/rendering/renderer.ts`), so combat stays PixiJS/audio-agnostic — the
  same pattern as `spawnProjectile` / `spawnText` today.
- **Assets:** two short mono clips in `web/public/audio/` (recommend `.ogg` with
  a `.mp3` fallback, or just `.mp3` for the broadest support), preloaded +
  `decodeAudioData` alongside `preloadArt()`. Budget ≤ ~40 KB each.
- **Mix/UX:** per-category gain (Master → SFX → per-sound), slight random
  pitch/gain jitter so repeated shots aren't robotic, optional distance
  attenuation, and a future tie-in to the **pause-menu `audioTracks` sliders**
  (Master / SFX) documented in `docs/board-sdk-opportunities.md` §4.

---

## 1. Audio tech approach on this stack

### Web Audio API vs `HTMLAudioElement`

Recommendation: **Web Audio API.**

| Concern | `HTMLAudioElement` (`new Audio()`) | **Web Audio API** |
| --- | --- | --- |
| Many overlapping one-shots | Each play needs its own element or a `cloneNode`; GC-heavy, element pool churn | A new `AudioBufferSourceNode` per shot is intentionally cheap and fire-and-forget |
| Latency | Higher / variable (media element pipeline) | Low; sample-accurate scheduling |
| Per-category volume / mixing | Manual per-element `.volume` | Native `GainNode` graph (Master → SFX → sound) |
| Pitch variation | Not really | `playbackRate` per source |
| Decode cost | Re-decoded / streamed per element | Decode **once** to a shared `AudioBuffer` |

A broadside fires *every* gun (up to **32 on a First Rate** — see the comment at
combatSystem.ts:46-49), and both sides fire continuously. That is exactly the
"many short overlapping one-shots" workload Web Audio is built for and
`HTMLAudio` is bad at. (We mitigate further with one-shot-per-volley + voice
limiting below, so we never actually try to play 32 cannon sounds at once.)

### The graph

```
                         ┌─────────────┐
 buffer source (shot) ──▶│  sfxGain    │──▶ masterGain ──▶ ctx.destination
 buffer source (shot) ──▶│ (SFX bus)   │       ▲
        …                └─────────────┘       │
                                          (future: musicGain also feeds here)
```

- `masterGain` — overall volume (future "Master" pause slider).
- `sfxGain` — all sound effects (future "Cannons / SFX" pause slider).
- Per-shot: create `AudioBufferSourceNode`, optionally route through a tiny
  short-lived `GainNode` for per-sound volume + jitter, connect to `sfxGain`,
  `start()`, and let it disconnect itself on `ended`.

Keep this in a new module, e.g. `web/src/audio/audio.ts` exporting an
`AudioEngine` (or `Sound`) singleton with:

- `init()` — lazily create the `AudioContext` + gain nodes.
- `unlock()` — `ctx.resume()` (idempotent; safe to call every gesture).
- `load(name, url)` / `preloadSounds()` — fetch + `decodeAudioData`, cache buffers.
- `play(name, opts)` — one-shot with `{ volume, rate, pan?, when? }`.
- `setMasterVolume(0..1)` / `setSfxVolume(0..1)` — for the pause sliders later.

### Autoplay / gesture unlock

Browsers (and the Board's built-in browser — see §5) start an `AudioContext` in
the `suspended` state until a user gesture resumes it. We do **not** need a new
"tap to enable sound" screen — the game already begins in a **Setup** phase where
each side taps a placement pad to take command:

- `Game.handleSetupContact(...)` (`web/src/core/game.ts:357`) runs on that tap
  (mouse click, device finger, or Glyph placement).
- Call `audio.unlock()` there (and/or in `placeCommandPiece`, game.ts:391). By
  the time the countdown ends and the first broadside fires, the context is
  already running.

Belt-and-suspenders: also call `unlock()` on the very first `pointerdown`
anywhere (cheap, idempotent) so audio works even in odd entry paths. All audio
calls must no-op gracefully if the context isn't running yet.

### Pooling / voice limits (so a 32-gun broadside doesn't blow out the mix)

Two layers of protection:

1. **One cannon shot per volley, not per ball.** The cannon SFX is emitted once
   in `tryFireBroadside` / `tryFireChase`, *not* inside the per-gun loop. A
   single, slightly "fat" cannon sample reads as a full broadside. (Optionally, a
   *very* short 2–3 tap micro-burst for big ships — see §4 — but still capped.)
2. **Global voice cap + per-sound throttle.** The `AudioEngine` keeps a count of
   active sources and a per-sound "last played at" timestamp:
   - Hard cap (e.g. **16 simultaneous voices**); beyond it, drop the new shot
     (or steal the oldest). Cheap to track via `start`/`ended`.
   - Per-sound min-interval (e.g. impacts coalesce within ~40–60 ms) so a volley
     landing produces **one or two** impact sounds, not 32. This is the audio
     side of the impact throttle described in §2.

### Per-sound volume

Each `play()` accepts a base `volume` (0..1) folded into `sfxGain`. Defaults:
cannon ~0.9, impact-hit ~0.7, splash ~0.4 (quieter — a miss should be subtler).
Tunable constants live next to the audio module (or in `core/config.ts`).

---

## 2. Where the events are in the code (exact hooks)

The combat simulation is engine-agnostic and already talks to the renderer only
through the `Effects` interface. We keep it that way: **add audio to `Effects`,
emit from `combatSystem.ts`, implement in `renderer.ts`.**

### Cannon fire (the shooting sound)

`web/src/combat/combatSystem.ts`, two volley sites:

- **Broadside** — `tryFireBroadside(...)`. The volley is committed at:

```54:75:web/src/combat/combatSystem.ts
    shooter.notifyFired(side);

    const forward = shooter.forward;
    const beamOffset = shooter.stats.beam * 0.6;
    const zBack = -shooter.stats.length * 0.28;
    const zFront = shooter.stats.length * 0.3;
    let anyHit = false;
    for (let g = 0; g < guns; g++) {
```

  Emit the cannon SFX **once, right after `shooter.notifyFired(side)`** (before
  the per-gun loop), positioned at the shooter (so distance attenuation works):

  ```ts
  // pseudo — not to be added by this doc
  effects.playSound("cannon", { at: shooter.position, count: guns });
  ```

- **Chase guns** — `tryFireChase(...)`, the analogous site at
  combatSystem.ts:105 (`shooter.notifyChaseFired(bow)`), before its per-gun loop.

This guarantees exactly one cannon trigger per broadside and per chase volley
across both fleets, no matter the gun count. (`shooter.notifyFired` already calls
`view.playBroadsideSmoke(side)` for the visual puff, so audio + smoke fire from
the same logical "volley committed" moment.)

> Alternative considered: emitting from `Ship.notifyFired` (ship.ts:168) so smoke
> and sound co-locate. Rejected for the first cut because `Ship` has no `Effects`
> handle and we don't want it to depend on audio; `combatSystem` already holds
> `effects`. Keep the engine/audio boundary at `Effects`.

### Impact (a shot hitting / landing)

Each ball's outcome is decided per gun, and the **tracer endpoint already encodes
hit vs miss** via `impactPoint(target, hit)` (combatSystem.ts:165-170: a hit
lands tight on the hull; a miss scatters wide as a near-miss splash). The ball is
spawned here:

```61:75:web/src/combat/combatSystem.ts
    for (let g = 0; g < guns; g++) {
      const hit = value() < hitChance;
      if (hit) {
        anyHit = true;
        const spread = 1 + rangeFloat(-Config.DamageSpread, Config.DamageSpread);
        target.applyDamage(profile, Config.PerGunDamageScale * damageFalloff * spread * rake);
      }
      …
      effects.spawnProjectile(origin, impactPoint(target, hit), profile.tracerColor);
    }
```

There are **two viable places** to fire the impact sound:

- **(A) Immediately on damage** — call `effects.playSound("impact"…)` inside the
  `if (hit)` block. Simplest, but it plays at *fire* time, while the tracer is
  still flying — the bang precedes the visible splash by the tracer's travel time
  (`life = dist / Config.ProjectileSpeed`, renderer.ts:168).
- **(B) On tracer arrival (recommended)** — let the **renderer** play the impact
  sound when the tracer reaches its target, because the renderer already
  simulates tracer travel and despawns it on arrival (renderer.ts:614-633). This
  keeps audio in sync with the visible landing and is automatically
  distance/timing correct.

Recommended: **(B)**, by extending `spawnProjectile` to carry the outcome, e.g.

```ts
// effects.ts — proposed signature
spawnProjectile(origin: Vec2, target: Vec2, color: number, impact?: "hit" | "splash"): void;
```

The combat code passes `hit ? "hit" : "splash"`. In the renderer's `Tracer`
struct (renderer.ts:19-24) store the `impact` tag; when a tracer reaches its
target in `updateEffects` (the `d <= step` / despawn branch, renderer.ts:619-628)
call the internal audio one-shot, then apply the **per-volley throttle** so a
broadside that lands ~10 balls in the same frame plays only one or two impact
sounds (coalesce within a short window, e.g. 40–60 ms, or cap to N per frame).

Distinguishing **hit vs near-miss splash**: drive it off the same `hit` boolean
the gunnery model already computes — `"hit"` → a wooden/iron thud, `"splash"` → a
softer water plume, quieter (§4). Misses are far more frequent at range, so the
splash must not dominate; the throttle + lower gain keep it tasteful.

### The clean single emit point — extend `Effects`

`web/src/combat/effects.ts` today:

```7:23:web/src/combat/effects.ts
export interface Effects {
  /** Spawns a cannon tracer travelling from `origin` to `target`. */
  spawnProjectile(origin: Vec2, target: Vec2, color: number): void;

  /** Spawns a floating text popup (e.g. "RAKE") at `pos` that rises and fades. */
  spawnText(pos: Vec2, text: string, color: number): void;
}

/** A no-op sink, useful for headless logic / tests. */
export const NullEffects: Effects = {
  spawnProjectile() {
    /* no-op */
  },
  spawnText() {
    /* no-op */
  },
};
```

Proposed additions (plan, not applied):

- `playSound(name: string, opts?: { at?: Vec2; count?: number; volume?: number }): void;`
  for the cannon (and future non-projectile SFX), and
- the optional `impact` arg on `spawnProjectile` for the projectile-timed impact.

Implement both on the `Renderer` (which already implements `Effects` in
practice — `combat.tick(this.ships, this.renderer)` at game.ts:248) by delegating
to the `AudioEngine`. Update `NullEffects` with no-op stubs so headless/test
callers keep compiling. The `Renderer` owns the `AudioEngine` instance (or it's a
module singleton the renderer calls), and converts `at`→ stereo pan / distance
gain via its existing world→screen mapping if we do positional audio (§4).

---

## 3. Assets

### Sourcing the two clips

Use royalty-free / CC0 sources, or synthesize:

- **Libraries:** [freesound.org](https://freesound.org) (filter to CC0),
  [Pixabay Sound Effects](https://pixabay.com/sound-effects/) (cannon, cannon
  impact, water splash — all royalty-free), [OpenGameArt](https://opengameart.org),
  Kenney's free audio packs. Search terms: "cannon shot", "cannon broadside",
  "cannonball impact wood", "water splash".
- **Generated:** a short synth/foley pass (e.g. filtered noise burst + low-end
  thump) is fine and keeps the bundle tiny; tools like
  [sfxr/jsfxr](https://sfxr.me) or a quick DAW bounce work. A procedurally
  generated "boom" can even be made in Web Audio at runtime as a fallback if a
  file fails to load (mirrors how art degrades to procedural drawing).

We need at least:

1. `cannon.*` — a single broadside boom (~0.6–1.2 s, with a tail).
2. `impact.*` — a cannonball hitting timber (~0.3–0.6 s).

Optional third for the miss: `splash.*` (water plume). If we skip it initially,
reuse `impact` at lower gain + lower pitch for misses.

### Format & size budget

- **Format:** short **mono** clips. Recommend **`.ogg` (Vorbis)** for best
  size/quality, with an **`.mp3` fallback** for any browser/WebView that lacks
  Vorbis. If we want a single format with the widest support and minimal
  plumbing, **`.mp3` only** is acceptable (decodes fine via `decodeAudioData`
  everywhere we run). Verify Vorbis support on the Board browser before going
  ogg-only (§5).
- **Sample rate:** 44.1 kHz mono is plenty; 22.05 kHz is fine for SFX and halves
  size.
- **Size budget:** target **≤ ~40 KB per clip**, ≤ ~120 KB total for all SFX.
  This matters because the whole app ships as a flat `.webapp.zip`
  (`@board.fun/web-pack`, see `web/AGENTS.md` "Build & deploy"); audio is small
  next to ship textures but we don't want to balloon the bundle.

### Where they live & how they load

- **Location:** `web/public/audio/` (Vite copies `public/` to the dist root, and
  the app uses **document-relative** URLs — no leading slash — so assets resolve
  under the dev server, the packed bundle, and a `file://` WebView; this is the
  same rule `web/src/rendering/assets.ts:7-10` documents for art). So reference
  `audio/cannon.mp3`, not `/audio/cannon.mp3`.

  > Note: `web/public/` currently holds only `model.tflite` and
  > `assets/.gitkeep`; create the `audio/` subfolder when implementing.

- **Preload + decode:** add a `preloadSounds()` in the audio module that, for
  each clip, `fetch`es the file → `arrayBuffer()` → `ctx.decodeAudioData()` and
  caches the resulting `AudioBuffer` by name. Call it alongside `preloadArt()` in
  `web/src/main.ts:35` (await it, but **don't block startup on failure** — like
  art, a failed clip degrades to silence / procedural fallback, never a crash).
  Each loader is individually guarded (`try/catch`, warn, leave the slot empty),
  mirroring `assets.ts`'s `tryLoad`.

  Caveat: `decodeAudioData` needs the `AudioContext` to exist. We can create the
  context up front (in `suspended` state — allowed) and decode before the unlock
  gesture; only *playback* requires the resume. So preload/decode at boot, unlock
  on the Setup tap.

---

## 4. Mixing / UX

### Volume levels (starting points, all tunable)

| Sound | Base gain | Notes |
| --- | --- | --- |
| Master | 1.0 | Future "Master" pause slider |
| SFX bus | 0.9 | Future "Cannons / SFX" pause slider |
| Cannon volley | ~0.9 | Slightly louder for big ships (scale with `guns`) |
| Impact (hit) | ~0.7 | Wooden/iron thud |
| Near-miss splash | ~0.4 | Deliberately subtle; misses are frequent |

### Pitch / timing randomization (anti-robotic)

Repeated identical samples sound mechanical, and broadsides repeat constantly.
For every one-shot:

- **Pitch jitter:** randomize `source.playbackRate` by ±5–12% (`value()` /
  `rangeFloat` from `web/src/core/rng.ts` keeps it deterministic with the sim
  seed, or use `Math.random()` for pure cosmetics — cosmetic effects elsewhere
  use `Math.random()`, e.g. renderer smoke at renderer.ts:174).
- **Gain jitter:** ±10% on the per-shot gain.
- **Micro-timing (optional):** for a "fat" broadside, schedule 2–3 cannon
  one-shots a few ms apart (`when` offsets of ~15–40 ms) with independent jitter,
  scaled by ship size (a First Rate's 32 guns → 3 taps; a frigate → 1). Still
  **capped** — never one per ball.

### Distance-based volume (optional, nice-to-have)

The renderer maps world→screen and knows the camera framing. For positional
flavor:

- **Gain:** attenuate by distance from arena center (or screen center) so
  far-off duels are quieter. Cheap: `gain *= clamp(1 - dist/maxDist, min, 1)`.
- **Stereo pan:** a `StereoPannerNode` keyed to the event's world `x` mapped to
  [-1, 1] via `worldToScreen`. Subtle (±0.5) so it never feels gimmicky.

This is purely additive — Phase 1 can ship mono, center, full-gain and add this
in Phase 2/3.

### Tie-in: pause-menu audio sliders (`audioTracks`)

`docs/board-sdk-opportunities.md` §4 already flags this as a Low-effort/Med-value
win, and the Board pause API supports it (`docs/unity-sdk/guide-pause-menu.md`,
"Audio sliders"). Plan:

- In `web/src/board/pauseMenu.ts`, extend the `setContext({...})` call
  (pauseMenu.ts:45) with:

  ```ts
  audioTracks: [
    { id: "master", name: "Master", value: 100 },
    { id: "sfx",    name: "Cannons (SFX)", value: 90 },
  ],
  ```

- In the result handler (`handleResult`, pauseMenu.ts:80), **apply
  `result.audioTracks` first, before branching on `action`** (the values come
  back on every dismissal regardless of which button was pressed — see the
  pause-menu guide). Map `master`→`audio.setMasterVolume(value/100)` and
  `sfx`→`audio.setSfxVolume(value/100)`.

  > This requires threading the returned `audioTracks` through `BoardPauseResult`
  > (`web/src/board/sdk.ts:72-76` currently only parses `action` /
  > `customButtonId`); add an `audioTracks` field to the parse in
  > `dispatchPauseResult` (sdk.ts:187-199). Small, isolated change.

- Persist the chosen levels in `localStorage` so they survive restarts (Board is
  wall-powered and can be torn down without warning).

In the browser preview (no Board), there's no pause overlay; sliders are simply
absent and the defaults apply. A small DOM volume control in the HUD could be a
later browser-only nicety, but is out of scope here.

---

## 5. Board specifics

- **Web Audio support:** the Board runs the web app inside its **built-in
  browser** (a Chromium/Android WebView per `web/AGENTS.md` and the Web SDK
  overview). Web Audio is a standard part of that engine, so the API itself is
  expected to be available. **Action item: verify on hardware** during Phase 1
  (a one-line `typeof AudioContext` log + an audible test shot after the Setup
  tap), and confirm **OGG/Vorbis decode** works there before committing to
  ogg-only assets — if in doubt, ship `.mp3` (universally supported) or provide
  both.
- **Gesture-unlock requirement:** the WebView enforces the same
  autoplay/`AudioContext`-suspended policy as desktop browsers, so the
  resume-on-gesture step (§1) is **required**, not optional. The Setup placement
  tap is the natural unlock point and happens before any combat audio.
- **SDK gating:** audio is plain Web Audio, **not** a Board SDK domain, so it
  needs **no** `Board.isOnDevice` gate and runs identically in the browser and on
  device. The only Board-specific code is the optional pause-slider tie-in (§4),
  which is already correctly gated inside `pauseMenu.ts`.
- **Docs:** the Board Web SDK docs don't add audio-playback APIs beyond the pause
  `audioTracks` sliders (`docs/unity-sdk/guide-pause-menu.md`); audio output is
  the browser's job. There is no documented Board-side mixer to route through —
  the pause sliders simply hand back 0–100 values for us to apply to our own
  `GainNode`s.

---

## 6. Phased implementation outline

### Phase 1 — Cannon + impact one-shots (core) — ~0.5–1 day

1. Add `web/src/audio/audio.ts`: `AudioEngine` singleton (context + master/sfx
   gains, `load`/`preloadSounds`, `play`, `unlock`, voice cap + per-sound
   throttle).
2. Add two clips to `web/public/audio/` (`cannon.*`, `impact.*`; optional
   `splash.*`).
3. Preload/decode in `web/src/main.ts` next to `preloadArt()` (guarded; failure
   degrades to silence).
4. Unlock on the Setup tap (`game.ts` `handleSetupContact` / `placeCommandPiece`)
   + a first-pointerdown belt-and-suspenders.
5. Extend `Effects` (`effects.ts`): add `playSound(...)` and the optional
   `impact` arg on `spawnProjectile`; update `NullEffects`.
6. Emit cannon SFX once per volley in `combatSystem.ts` (after `notifyFired`
   :54 and `notifyChaseFired` :105).
7. Pass `hit ? "hit" : "splash"` into `spawnProjectile` (:74, :124); play the
   impact one-shot from the renderer when the tracer lands
   (`renderer.ts` `updateEffects`), with the per-volley throttle.
8. **Verify on hardware** (Web Audio present, OGG decode, unlock works).

Deliverable: broadsides boom; balls land with a thud, misses splash; never more
than a couple of overlapping impacts; works in browser and on Board.

### Phase 2 — Volume control / pause sliders — ~0.5 day

1. Add `audioTracks` (Master, SFX) to the pause context (`pauseMenu.ts:45`).
2. Thread `audioTracks` through `BoardPauseResult` parsing (`sdk.ts`).
3. Apply returned values first in `handleResult` → `audio.setMasterVolume` /
   `setSfxVolume`; persist to `localStorage`.
4. (Optional) distance attenuation + stereo pan via the renderer's world→screen
   mapping.

Deliverable: players adjust Master/SFX from the Board pause overlay; levels
persist.

### Phase 3 — More SFX (polish) — ~1–2 days, incremental

- **Wind/ambience:** a low looping bed whose gain tracks wind strength
  (`web/src/combat/wind.ts`) — a *looping* source (separate from the one-shot
  path), ducked under combat.
- **Sinking:** a creak/groan + final splash when a ship enters `ShipState.Sinking`
  / `beginSinking()` (`ship.ts:220`) — emit via `Effects` from the cull/sink
  path.
- **RAKE sting:** a short stinger when the "RAKE" popup fires
  (`combatSystem.ts:80-82`).
- **UI/Setup:** soft confirm on placing a command piece / countdown ticks.
- Possibly a "Music" track + its own pause slider.

Each Phase 3 item reuses the `AudioEngine` and the `Effects` boundary, so they're
small additive changes.

---

## Risks / things to verify

- **OGG/Vorbis on the Board WebView** — verify before ogg-only; `.mp3` is the safe
  default.
- **Decode-before-unlock** — confirm `decodeAudioData` succeeds with a `suspended`
  context (it should); if not, decode lazily on first unlock.
- **Throttle tuning** — the impact coalescing window needs play-testing so a
  full broadside landing feels punchy, not machine-gunny, and not a single dull
  thud.
- **Bundle size** — keep total SFX ≤ ~120 KB; check the `.webapp.zip` after
  adding assets.
- **No `Board.isOnDevice` gating needed for audio**, but the pause-slider tie-in
  must stay inside the existing gated `pauseMenu.ts` paths.
