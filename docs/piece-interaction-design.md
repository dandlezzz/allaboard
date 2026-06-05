# Piece Interaction Design — reference (from docs.dev.board.fun/learn/piece-interaction-design)

> Pasted from the official Board "Piece Interaction Design" guide (the proposal worker
> couldn't fetch it — this is the authoritative source for implementing the Baton of
> Command touch scheme). Our baton = a **Pawn Piece** (the Board Arcade robots, à la
> Cosmic Crush / Snek). Use this to drive the implementation in `docs/baton-touch-scheme.md`.

## Core principle
Pieces ARE the controller. If a player thinks "this would be easier with a mouse," the
mechanic isn't native to Board. Pieces have inertia (pace around hands, not reflexes),
presence (state on a Piece is shared/visible to the table), are tactile, and are fragile in
motion (tracking can drop on fast/angled/hand-covered moves — design forgiving interactions).

## Interaction primitives (each maps to contact phase / position / orientation / isTouched)
- **Place & Lift** — contact `Began` = placed (spawn command bubble); `Ended`/`Canceled` =
  lifted (tear down / dismiss). Right primitive for spawn/despawn and "play this piece."
  Tips: clear glowing placement zone; ~200ms hold-to-confirm to avoid accidental placement;
  confirm with BOTH visual + audio (hand may cover screen).
- **Slide** — `Moved` stream; continuous positioning/aiming. Don't snap mid-motion; show a
  trail; don't add your own smoothing (SDK already smooths). Position = display px, top-left,
  Y-down (Web).
- **Trace** — path-shape gesture (lenient ~70% match, live feedback).
- **Shake** — velocity-based (direction reversals); reset on lift; audio cue.
- **Rotate** — orientation changes, position ~constant. Dials/aiming/mode switch.
  **Web reports DEGREES.** Give angle feedback + **deadbands at cardinals**; forgiving
  tolerance (~5° on a 90° snap, NOT 1°); best with rotationally-symmetric Pieces.
- **Twist** — quick discrete rotational gesture (rate-of-change past a threshold, not resting
  angle); one-shot punctuation (sound/flash).
- **Touch & Release (Hold)** — capacitive coating exposes the **`isTouched`** flag (true while
  a hand is on the Piece). Hold-to-activate / selection / disambiguation / **confirm a slide
  (leave a slide-move uncommitted until the hand releases — how Cosmic Crush confirms robot
  placement)**. No edge signal: diff `isTouched` per-frame keyed by `contactId`. Web: fingers =
  glyphId 0 (no hold state), gate hold on `glyphId > 0`. ~200ms comfortable; >1.5s feels stuck;
  give continuous feedback (fill meter / swelling tone).

## Edge detection (Web) — no down/up events
```ts
import { BoardContactPhase, BoardContactType, type BoardContact } from "@board.fun/web-sdk";
// place/lift:
switch (c.phase) {
  case BoardContactPhase.Began: onPiecePlaced(c); break;
  case BoardContactPhase.Ended:
  case BoardContactPhase.Canceled: onPieceLifted(c); break;
}
// hold edge: keep prevTouched: Map<contactId, boolean>, diff each frame, gate on glyphId>0.
```

## Piece categories (robots = Pawn)
- **Pawn / Character** — player's unit; persistent; primary input is Place + Slide (+ rotation).
  One Pawn per player, distinct shapes/colors. **Examples: the robots in Cosmic Crush and Snek.**
- **Action / Verb** — single-purpose, stateless, fire on the frame `isTouched` flips true.
- **Reaction / Platform** — persistent terrain placed once; the world reacts to its position.
- Branch behavior on **`glyphId`** (Web 1-based; fingers = 0). Learn each id empirically by
  placing one Piece at a time and logging the id.

## Signifiers / feedback to ship
- Clear glowing placement zones; Piece indicators (render + name + consistent icon/color).
- **Placement confirmation is the most important signifier** — glow + outline + audible click;
  rejection = don't advance + blocked outline + brief reason ("Out of range"); never auto-correct.
- Action confirmation distinct from placement confirmation.

## Design rules (apply to the baton)
- Forgiving thresholds (mm of slop, degrees of rotation error).
- Use sound generously (players look at hands, not screen).
- Account for table position (radial / per-seat; any Piece approached from any angle).
- Avoid simultaneous required two-hand interactions; stagger.
- Test on hardware — feel differs hugely from the desktop mouse simulator.
