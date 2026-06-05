# Baton of Command — proposed touch / Piece control scheme

> **Status: DESIGN PROPOSAL for review. No gameplay code changes are made by this
> document.** It describes a Piece-centric control scheme for the Baton of
> Command, grounds every interaction in the documented Board input API, and ends
> with a phased implementation outline. Implement only after sign-off.

---

## 1. Sources reviewed

Board platform docs (read in full for the input/Piece capabilities):

- **AI Assistant quick reference** — <https://docs.dev.board.fun/web/ai-assistant>
  (mirrored locally in [`web/AGENTS.md`](../web/AGENTS.md)).
- **Touch Input guide** (the contact model, side-by-side across SDKs) —
  <https://docs.dev.board.fun/guides/touch-input>.
- **Learn ▸ Pieces** — <https://docs.dev.board.fun/learn/pieces>.
- **Learn ▸ Hardware** — <https://docs.dev.board.fun/learn/hardware>.
- **Overview** — <https://docs.dev.board.fun/> ("Track as many fingers and Pieces
  as you want, all at once, with no performance penalty").
- *(The "Piece Interaction Design" guide,
  <https://docs.dev.board.fun/guides/piece-interaction-design>, was repeatedly
  unreachable — fetch timeouts — at the time of writing. Its sibling pages above
  cover the same primitives, so nothing below depends on it; re-check it before
  implementation in case it adds a recommended idiom.)*

Repo code reviewed: [`web/src/board/input.ts`](../web/src/board/input.ts),
[`web/src/core/game.ts`](../web/src/core/game.ts),
[`web/src/rendering/renderer.ts`](../web/src/rendering/renderer.ts),
[`web/src/core/config.ts`](../web/src/core/config.ts),
[`web/src/board/sdk.ts`](../web/src/board/sdk.ts),
[`web/src/board/batonGlyphAdapter.ts`](../web/src/board/batonGlyphAdapter.ts),
[`BOARD_HARDWARE.md`](../BOARD_HARDWARE.md),
[`docs/baton-of-command-integration.md`](baton-of-command-integration.md).

---

## 2. What the Board hardware actually gives us (capabilities, precisely)

These are the platform facts the scheme is built on. Citations are to the pages
above.

### 2.1 The contact model

- The SDK delivers a **full per-frame snapshot** of all current contacts via
  `Board.input.subscribe(callback)`. The callback "is called once per inference
  frame with the full current contact set." There are **no discrete down/up
  events** — you keep a previous-frame map keyed by `contactId` and diff it to
  detect edges. (Touch Input; AI Assistant.)
- **Unlimited simultaneous contacts.** "Track as many fingers and Pieces as you
  want, all at once, with no performance penalty." Board explicitly removes the
  ~10-point limit. (Overview.) So **many fingers + multiple Pieces can be down at
  the same time** — a Piece resting on the board *and* several fingers tapping
  controls beside it is fully supported.
- Each contact carries:
  | Field | Meaning / Web convention |
  |---|---|
  | `contactId` | Stable identifier for the **physical instance**, constant from appearance to lift. Track Piece instances by this — **never** by `glyphId`. |
  | `type` | `BoardContactType.Finger` / `Glyph` / `Blob`. |
  | `glyphId` | Which Piece *type* in the active Piece Set (`0` = finger, `1+` = a Piece). A **type** id, not an instance id. |
  | `x`, `y` | Display **pixels**, origin **top-left, Y down**, panel **1920×1080** — no flip. |
  | `orientation` | Piece rotation in **degrees** (Web), ~**1° precision**. Fingers have none. |
  | `phase` | `Began` / `Moved` / `Stationary` / `Ended` / `Canceled`. |
  | `isTouched` | **Pieces only:** whether a hand is currently on the Piece (held) vs resting. |

### 2.2 Two facts that the current code does not yet exploit

1. **`isTouched` (held vs resting).** Pieces with a conductive body let Board
   report whether a player's hand is on the Piece right now. The docs call this
   out as the intended way to "run different behavior for held vs resting Pieces,
   and detect releases" and to highlight a Piece "when picked up." (Learn ▸
   Pieces; Touch Input ▸ *Piece touch state*.) This is the natural signal for
   "the captain has the baton in hand → he is steering" vs "the baton is set down
   → the order stands."
   *Caveat:* not every Piece body is conductive; if the chosen Piece does not
   report `touched`, the scheme degrades gracefully (see §6.5).

2. **`Canceled` phase.** "When the pause screen opens, all active contacts are
   canceled." We must treat `Canceled` exactly like a lift for cleanup, or the
   command bubble will be orphaned across a pause. (Touch Input ▸ *Phases*.)

### 2.3 Lift detection — the primitive the whole proposal hinges on

Because there are no up events, **"the Piece was lifted off the Board" = its
`contactId` was present last frame and is absent this frame** (or arrives with
phase `Ended`/`Canceled`). [`web/src/board/input.ts`](../web/src/board/input.ts)
**already implements this** for the unified pointer path: it keeps a `previous`
map keyed by `contactId`, and for any id missing from the new snapshot it
synthesises a single `"ended"` sample (lines 94–100). The lift signal therefore
already exists end-to-end; what is missing is that **`game.ts` ignores it for
Pieces** (see §3).

```62:103:web/src/board/input.ts
  const previous = new Map<number, PointerSample>();

  const unsubscribe = board.input.subscribe((contacts: ReadonlyArray<BoardContactLike>) => {
    // ...
    const seen = new Set<number>();
    for (const contact of contacts) {
      seen.add(contact.contactId);
      const existed = previous.has(contact.contactId);
      // ...build sample with phase: existed ? "moved" : "began"...
    }
    // Contacts present last frame but gone now → emit a single synthetic "ended".
    for (const [contactId, last] of previous) {
      if (!seen.has(contactId)) {
        samples.push({ ...last, phase: "ended" });
        previous.delete(contactId);
      }
    }
```

---

## 3. How the Baton works today, and why it's awkward

### 3.1 Current behaviour (as implemented)

- **Setup phase:** each human side drops a command piece on a glowing pad; that
  seeds the side's baton (`seedBaton`) and marks the side ready
  ([`game.ts` `placeCommandPiece`](../web/src/core/game.ts)).
- **Playing, mouse/finger path:** `handleDown` first hit-tests the per-side
  **group command panel** (sail / ammo discs drawn above each baton); a tap there
  trims the whole squadron. Otherwise it arms a **tap-or-drag** gesture: a *tap*
  (`<6 px`) re-places the baton (`placeBaton` → nearest human side), a *drag*
  sets a single fleet heading from the squadron centroid to the release point.
  (`K_DRAG_THRESHOLD_PX = 6`.)
- **Playing, Piece path:** `onPointerSamples` routes **every** glyph sample
  (`began`/`moved`/**`ended`**) into `handleGlyph`, which ignores `phase`
  entirely: it re-seeds the baton at the Piece position **and** force-sets the
  heading of every commanded ship from the Piece orientation, every frame.
- The `batonGlyphAdapter.ts` that *does* model place/clear-on-lift is written but
  **not wired in** (it is a standalone plan module).

### 3.2 Concrete pain points

1. **Controls never dismiss when the Piece is lifted.** `handleGlyph` is
   phase-blind, so the synthetic `"ended"` sample on lift just **re-seeds the
   baton at the lift position** instead of clearing it. `batonPos` is only ever
   removed on `restart()`. So the command roundel, the sphere ring, and the
   sail/ammo panel **stay on screen forever** after the captain picks the Piece
   up. This is the headline complaint and the thing the user explicitly wants
   fixed. (See `onPointerSamples` lines 259–261 and `handleGlyph` 465–475.)
2. **Heading is rigidly clamped to Piece facing, every frame.** `handleGlyph`
   calls `setTargetHeading(...)` for *all* commanded ships on *every* frame the
   Piece is seen — even when the Piece is just resting and the player isn't
   touching it. Ships can never hold a heading that differs from the Piece, and
   there is no separation between "I'm actively steering" and "leave it be."
3. **The commanded set silently churns.** `seedBaton` re-runs `pickCommandedShips`
   each frame, so ships that drift out of the (frozen-geometry) sphere are
   dropped and others swept in. The visible sphere ring implies a fixed area, but
   membership is recomputed continuously and surprises the player.
4. **Ambiguous tap vs drag (finger path).** A 6 px threshold on a 1080p panel is
   below normal finger jitter, so intended *taps* (place baton / trim) easily
   register as *drags* (set course) and vice-versa.
5. **Finger controls and baton placement fight on the same gesture.** Any finger
   down that misses the panel discs is treated as a baton (re)placement via
   `nearestHumanFaction`. There's no "the Piece owns commanding; fingers only
   operate controls" separation — so a stray touch relocates command.
6. **Panel placement is fixed-offset and can leave the screen.** The sail/ammo
   panel is drawn a fixed `+75` world units above the baton
   (`commandPanelLayout`), with no clamping, so a baton near the top edge pushes
   its controls off-board.
7. **`isTouched` and `Canceled` are unused.** The richest Piece signal (held vs
   resting) is dropped at the input boundary, and a pause leaves the bubble
   orphaned.

---

## 4. Proposed control scheme (Piece-centric)

**One sentence:** *The physical Piece **is** the Baton — set it down to take
command of the squadron in its sphere, **rotate it to steer** them, tap the
floating controls **with your fingers** while it rests to trim sail and shot, and
**lift it off the Board to dismiss command** — detected by the Piece's
`contactId` leaving the per-frame snapshot.*

### 4.1 The Piece lifecycle: Placed → Held / Resting → Lifted

Model each side's baton as a small state machine keyed by the Piece's
`contactId`. The phases come straight from the contact model (§2.1).

| State | Trigger (from the contact stream) | Behaviour |
|---|---|---|
| **Placed** | A glyph `contactId` not seen last frame (`Began`). | Spawn that side's command bubble at the Piece. Capture the commanded squadron = alive friendly ships of that side within `BatonCommandRadius` (see §4.4). Show the sphere ring, the bearing pointer, and the finger controls. |
| **Held** | Piece present **and** `isTouched === true`. | "Captain has the baton." **Rotate-to-steer is live:** the squadron's ordered heading tracks the Piece orientation (§4.2). Bubble is highlighted (brighter ring) to show it is being commanded. |
| **Resting** | Piece present **and** `isTouched === false`. | "Order stands." Heading is **latched** to the last commanded value — the squadron holds its course even as the Piece sits there. Finger controls (§4.3) remain active. |
| **Lifted** | The Piece's `contactId` is absent this frame, or arrives `Ended` / `Canceled`. | **Dismiss everything for that side:** clear `batonPos`, hide the bubble / ring / controls / bearing pointer, and **leave the squadron sailing its last ordered heading** (the captain gave the order; removing the baton doesn't capsize the fleet). |

Notes:

- **Latch, don't clamp.** The key behavioural change vs today: ordered heading is
  written **only while Held** (or on an explicit rotate gesture), then *latched*.
  Resting and Lifted never overwrite it. This fixes pain points #2 and removes the
  "ships can't hold a heading" feel.
- **Lift = dismiss, not = stop.** Lifting tears down the *UI and command binding*,
  not the ships' current orders. (If you prefer "lift = freeze on current
  heading," that's already the result; if you ever want "lift = all-stop," gate it
  behind `isTouched` going false first — but defaulting to "order stands" reads
  better for a fleet game.)
- **`Canceled` is handled identically to `Ended`** so a pause cleanly dismisses.

### 4.2 Orientation steering ("rotate the baton to set the course")

Rotating a physical Piece to point the way you want the squadron to sail is the
single most natural Piece interaction here, and the hardware gives ~1° precision.

- While **Held**, map the Piece `orientation` (degrees, Web) to the squadron's
  ordered heading and apply it to every commanded ship: `setTargetHeading(h)`.
  The existing conversion is already in `handleGlyph`
  (`normalize360(-orientation°)`); keep whatever sign/offset makes "Piece bow
  points north → fleet sails north" true on the device and verify empirically.
- **Apply only while Held, then latch** (per §4.1). When the captain lets go, the
  fleet keeps that heading; nudging a resting Piece by accident does nothing.
- **Dead-band the rotation.** Ignore sub-~3° jitter frame-to-frame so a resting
  Piece's noise can't creep the course. (Hardware precision is ~1°, so a small
  dead-band is comfortably above noise.)
- **Optional per-ship vs whole-fleet:** default to a single common heading for the
  whole commanded squadron (matches today's "fleet order" model and the
  line-ahead fantasy). A later refinement could offset each ship to preserve
  formation bearing; out of scope for v1.
- **Bearing feedback:** draw a heading pointer from the bubble in the commanded
  direction (reuse `showHeadingLines` / the course-preview styling) so the
  rotate-to-steer mapping is legible while turning.

### 4.3 Finger-operated controls while the Piece rests (Piece + finger together)

Because Board tracks unlimited simultaneous contacts, the Piece can stay on the
board (holding command) while the player taps controls **with a finger** right
next to it. This is the intended multi-contact pattern.

- When a baton is **Placed** (Held or Resting), draw a compact **radial control
  cluster** around the Piece: a **Sail-reefing** button and an **Ammunition**
  button (the two orders that already exist:
  `cycleGroupSail` / `cycleGroupAmmo`). Optionally add **Hold / Fire-at-will** and
  **Form line** later.
- These are operated by **finger taps only** (`type === Finger`). A finger tap
  whose position is within a control's hit-radius cycles that order for the whole
  commanded squadron; it must **never** place or move a baton.
- **Disambiguation rule (fixes #5):** while *any* Piece baton is on the board,
  finger contacts are interpreted **only** as control hits — they do not relocate
  command. Command placement/teardown is owned by the **Piece** lifecycle. (In
  the browser, where there is no Piece, the mouse keeps today's place/steer role;
  see §6.)
- **Anchor the cluster to the Piece and clamp on-screen (fixes #6):** lay the
  buttons out around the Piece center on a fixed radius, then clamp the cluster
  into the safe area so a baton near an edge still shows reachable controls.
  Render them as a ring around the Piece rather than a fixed `+75` offset.
- **Reachability:** keep the cluster radius small (a thumb's reach from the Piece)
  so one hand can rest the Piece and the other taps, or the same hand pivots.

### 4.4 Sphere of influence — sizing & feedback

- **Capture membership at placement, then keep it stable.** On **Placed**, compute
  the commanded set once (alive friendly ships within `BatonCommandRadius`). Do
  **not** re-pick every frame (fixes #3). Re-capture only on an explicit
  **re-place** (lift + set down again) or an explicit "re-gather" tap. This makes
  the sphere ring mean what it shows.
- **Show the ring only as a placement aid.** Render the sphere prominently while
  **Held** / just **Placed** (it explains who's being gathered), then fade it to a
  faint tether/among-the-commanded highlight while **Resting**, so the board isn't
  cluttered by a big disc that no longer reflects live membership.
- **Sizing:** `BatonCommandRadius` (currently `15 × ShipScale = 150`) is a
  reasonable squadron-gathering radius; expose it unchanged. Consider a brief
  "gather pulse" animation from the Piece out to the ring on Placed to teach the
  capture rule. (Tuning only — no logic change required.)
- **Commanded highlight:** keep the existing per-ship commanded outline
  (`setCommanded`) so it's always clear which hulls are under this baton, even
  after the ring fades.

### 4.5 Multi-baton / two-player simultaneous use

- Everything above is **per `contactId`** (one captain, one Piece). Board's
  unlimited contacts mean two players can each rest a Piece and steer/trim their
  own squadron at the same time with zero contention.
- Keep the existing **per-faction** baton state (`batonPos`,
  `commandedShips` are already `Map<Faction, …>`), but key the *live Piece
  binding* by `contactId` so two physical Pieces of the same side, or two sides,
  are tracked independently and lift independently.
- **Ownership:** a Piece commands the side whose nearest ship is closest at
  placement (today's `nearestHumanFaction`), decided **once** at Placed and held
  for that Piece's lifetime — so a Piece can't "steal" the other player's fleet by
  drifting.
- Two sphere rings/bubbles already render per side with accent colours
  (`showBatons`), so the visuals scale to two batons unchanged.

---

## 5. Mapping to the documented Board input API

| Scheme interaction | Board API basis |
|---|---|
| "Piece placed" | New `contactId` of `type === Glyph` in the per-frame snapshot (synthetic `Began`). (Touch Input.) |
| "Piece held vs resting" | `contact.isTouched` (Pieces only). (Learn ▸ Pieces; Touch Input ▸ Piece touch state.) |
| "Rotate to steer" | `contact.orientation` (degrees, ~1° precision). (Learn ▸ Pieces; Touch Input ▸ Orientation.) |
| "Piece lifted → dismiss" | `contactId` absent from this frame's snapshot, or `phase === Ended`/`Canceled`. Already synthesised in `input.ts`. (Touch Input ▸ Phases.) |
| "Pause dismisses cleanly" | `phase === Canceled` ("when the pause screen opens, all active contacts are canceled"). (Touch Input ▸ Phases.) |
| "Finger taps trim sail/ammo while Piece rests" | Unlimited simultaneous contacts; split on `type` (`Finger` vs `Glyph`). (Overview; Touch Input ▸ Filtering by type.) |
| "Track each baton independently" | Stable `contactId` per instance; **never** key on `glyphId` (a type id). (Touch Input ▸ Glyph IDs; repo `AGENTS.md`.) |
| Coordinates | Device px, top-left, Y-down, 1920×1080; `input.ts` already maps device px → canvas CSS px. (Touch Input ▸ Position.) |

All device access stays gated on `Board.isOnDevice`, per `AGENTS.md`.

---

## 6. Browser / mouse-fallback emulation

A mouse has no `isTouched` and cannot physically "lift," so the fallback must
emulate Placed / Held / Rotate / Resting / Lifted. Proposal (kept close to
today's mouse feel so browser play doesn't regress):

| Physical (on device) | Mouse emulation (browser) |
|---|---|
| **Place** Piece | **Click on open sea** → place the baton & capture the squadron (today's tap-to-place). |
| **Held** (hand on Piece) | **Mouse button down / dragging** = "held" — rotate-to-steer is live during the drag. |
| **Rotate** to a heading | **Drag** from the baton: heading = baton → cursor bearing (replaces the centroid-drag; more direct and matches "point the way"). Live course preview while dragging (reuse `showCoursePreview`). |
| **Resting** (order stands) | **Mouse up** after a place/steer → the order latches; baton stays, squadron holds heading. |
| **Trim sail / ammo** | **Click the control buttons** around the baton (today's panel taps), unchanged. |
| **Lift** to dismiss | **Click the baton itself again** (toggle off), **or** a dedicated small "✕ / dismiss" hit on the bubble, **or** click far away on open sea to drop command. Pick one primary (recommend: click the baton roundel to dismiss) and keep it discoverable with a tooltip/label. |

Implementation niceties for the mouse path:

- Replace the brittle 6 px tap/drag split with **intent by target**: a press that
  *starts on the baton roundel* begins a rotate-steer drag; a press on a control
  button trims; a press on open sea places (or, if a baton exists, is a no-op /
  dismiss per the chosen rule). This removes pain point #4 without a magic pixel
  threshold.
- Keep the same on-screen affordances (roundel, ring, control cluster) so the
  browser preview teaches the device interaction.

---

## 7. Consistency with Board UX conventions

- **Pieces own meaning; fingers own actions.** The Piece is the persistent token
  of command (position = where, orientation = heading, presence = "command is
  active"); fingers are transient operators of the surfaced controls. This matches
  the docs' framing of Pieces as tracked stateful tokens and fingers as ordinary
  touches.
- **Held vs resting drives behaviour**, exactly as the docs suggest ("run
  different behavior for held vs resting Pieces, and detect releases").
- **Clean teardown on `Canceled`**, per the pause-cancels-contacts rule.
- **No silent roster/finger surprises**, no reliance on `glyphId` for instance
  identity, all device calls gated on `Board.isOnDevice` — consistent with
  `AGENTS.md`.

---

## 8. Phased implementation outline (for later — not done here)

**No gameplay logic is changed by this document.** When approved, implement in
small, separately-reviewable steps:

### Phase 0 — Surface the missing signals (input boundary)
- [`web/src/board/input.ts`](../web/src/board/input.ts): add `touched: boolean`
  to `PointerSample` (from `contact.isTouched`; default `true` for fingers/mouse).
- Pass through `Canceled` as an `"ended"` edge (already collapsed to lift by the
  diff; ensure an explicit `Canceled` phase also synthesises the lift even when the
  contact still appears in the frame). Keep the existing per-`contactId` diff as
  the lift primitive.

### Phase 1 — Baton lifecycle in `game.ts` (the core fix)
- Replace the phase-blind `handleGlyph` with a `contactId`-keyed state machine:
  `Began` → place + capture squadron once; `isTouched` → steer (latch on release);
  `Ended`/`Canceled`/absent → **`clearBaton(contactId)`** that deletes
  `batonPos`/binding for that side and hides its visuals.
- Add a `clearBaton`/dismiss path (the wiring the standalone
  [`batonGlyphAdapter.ts`](../web/src/board/batonGlyphAdapter.ts) already
  anticipates — reuse its `placeBaton`/`clearBaton` shape).
- Stop re-`pickCommandedShips` every frame; capture at Placed, re-capture only on
  re-place/re-gather.
- Apply `setTargetHeading` **only while Held** + dead-band; latch otherwise.
- Gate finger contacts to control-hits only while any baton is on the board.

### Phase 2 — Mouse-fallback parity
- Re-do the mouse path as **intent-by-target** (baton roundel = steer-drag,
  control = trim, sea = place); add an explicit **dismiss** affordance
  (click roundel / ✕) to emulate lift. Remove `K_DRAG_THRESHOLD_PX`.

### Phase 3 — Rendering / feedback
- [`renderer.ts`](../web/src/rendering/renderer.ts): anchor the control cluster as
  a **ring around the baton**, clamped to the safe area (replace fixed `+75`
  offset in `commandPanelLayout`); add a Held highlight on the bubble; fade the
  sphere ring after placement; draw the rotate-to-steer bearing pointer; add a
  dismiss affordance for the mouse.

### Phase 4 — Tuning & validation (on hardware)
- Empirically confirm orientation sign/offset (Piece bow → fleet heading),
  `isTouched` availability for the chosen Piece (Board Arcade robot — see
  [`docs/baton-of-command-integration.md`](baton-of-command-integration.md)),
  dead-band size, control-cluster radius/reachability, and the dismiss feel.
- Tune `BatonCommandRadius`, cluster radius, and ring fade in
  [`config.ts`](../web/src/core/config.ts) (no logic, just knobs).

### Degradation path (Piece without conductive body)
- If the chosen Piece does **not** report `isTouched`, fall back to: **placed =
  Held** (treat presence as command-active), rotate-to-steer always live with a
  larger dead-band, and **lift = dismiss** unchanged (still driven by the
  `contactId` leaving the snapshot). The lift-to-dismiss mechanism does not depend
  on `isTouched` at all.

---

## 9. Summary of the recommended scheme

- **Set the Piece down = take command** of the squadron in its sphere (captured
  once at placement, so the ring means what it shows).
- **Hand on the Piece (`isTouched`) = steer:** rotate the Piece to set the
  squadron's heading (~1° precision, dead-banded); **let go = the order latches**
  and the fleet holds course.
- **Fingers operate floating controls** (sail / ammo) clustered around the resting
  Piece — Board's unlimited simultaneous contacts make Piece-plus-fingers natural;
  fingers never move command.
- **Lift the Piece off the Board = dismiss the bubble and all controls**, detected
  because the Piece's `contactId` disappears from the per-frame snapshot (already
  synthesised as an `"ended"` edge in `input.ts`); `Canceled` (pause) is treated
  the same.
- **Two players, two Pieces** work simultaneously and independently, keyed by
  `contactId`.
- **Mouse fallback** emulates the cycle: click to place, drag the roundel to
  steer, click the controls to trim, click the roundel (or ✕) to dismiss — no
  brittle pixel threshold.
