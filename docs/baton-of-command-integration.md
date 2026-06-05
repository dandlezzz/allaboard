# Baton of Command — physical Piece (Glyph) integration plan

How a physical robot **Piece** placed on the Board drives the in-game **Baton of
Command**. This is a **plan + drop-in adapter** to merge *after* the mouse-driven
Baton control scheme lands; it deliberately touches **none** of the in-progress
files (`web/src/board/input.ts`, `web/src/core/game.ts`, `web/src/rendering/*`).

Adapter module (new, standalone, not yet imported anywhere):
[`web/src/board/batonGlyphAdapter.ts`](../web/src/board/batonGlyphAdapter.ts).

---

## 1. Which Piece Set — "the robots from the space game"

**Lead candidate: the `Board Arcade` Piece Set** (the *Retro Arcade Collection*).

Evidence (sources):

- board.fun homepage: *"Open **Board Arcade** when the room shouts 'game night.'
  Grab the **space ships and robots**, pick teams and jump between five reimagined
  arcade classics."* (<https://board.fun/>)
- The Board ships with 7 free games incl. the **Retro Arcade Collection: Board
  Arcade, Cosmic Crush, Snek, Astrofort, Starfire, Space Rocks** — all sharing the
  arcade Piece Set; Snek's description: *"Use your **robot** to guide your snek."*
  (<https://board.fun/pages/games>, <https://board.fun/>)

So the robot figurines almost certainly belong to the **Board Arcade** Piece Set
(`model.tflite` named after "Board Arcade"). Other named Piece Sets — Chop Chop,
Mushka, Thrasos, Omakase, Save the Bloogs — are themed (cooking, pet, etc.) and do
**not** match "robots from the space game."

**What the user must confirm** (cannot be derived from public docs — needs the
auth-gated dev portal):

1. Log in to <https://dev.board.fun/> → Piece Set Models, and download the
   **Board Arcade** model `.tflite` (confirm the exact filename).
2. **Find the robot Pieces' `glyphId`s** empirically: install a build with the
   model, then place each robot Piece on the Board one at a time and log
   `contact.glyphId` (per the docs, `0` = finger, `1+` = Piece). Record which id
   is which robot. Set `batonGlyphId` in the adapter to that id (or leave it
   unset to accept any Piece).

---

## 2. Mapping: Glyph contact → Baton placement

A robot Piece **is** the Baton of Command:

| Physical Piece (Glyph contact) | Baton of Command action |
|---|---|
| Piece placed at position `(x, y)` | Baton placed there → commands the nearest friendly ship / spawns the command bubble at that point (same action the mouse "place" does). |
| Piece **rotated** to orientation θ | Commanded **course/heading** for that ship (optional; the mouse drag sets this). |
| Piece **moved** | Baton drag-moves; re-targets the nearest friendly ship continuously. |
| Piece **lifted** (`Ended`/`Canceled`) | Baton cleared (same as mouse release). |
| Multiple Pieces (multiplayer) | Track independently by `contactId`. |

Coordinate / unit conversions the adapter handles (Web SDK conventions, from
<https://docs.dev.board.fun/guides/touch-input>):

- **Position**: Board panel is `1920×1080`, origin top-left, Y down — the adapter
  scales panel px into the on-screen canvas rect (override via `toCanvas` if your
  renderer uses a world/virtual coordinate space).
- **Orientation**: the Web SDK reports **degrees**; the game uses **radians**, so
  the adapter converts (`degToRad`). ⚠️ Note `web/src/board/input.ts` currently
  comments orientation as "radians" — on device it's degrees; convert at the
  boundary.
- **Filtering**: ignores fingers (only `type === Glyph`); optionally restricts to
  one `batonGlyphId`.

---

## 3. The adapter (drop-in, already in the repo)

[`web/src/board/batonGlyphAdapter.ts`](../web/src/board/batonGlyphAdapter.ts)
exposes:

```ts
export interface BatonController {
  placeBaton(args: { position: Vec; course?: number; sourceId: number }): void;
  clearBaton(args: { sourceId: number }): void;
}

export function attachBatonGlyphControl(
  canvas: HTMLCanvasElement,
  baton: BatonController,
  options?: { batonGlyphId?: number; toCanvas?: (boardPx: Vec) => Vec },
): Promise<() => void>;
```

It reuses the optional SDK loader ([`web/src/board/sdk.ts`](../web/src/board/sdk.ts)):
on a browser (no SDK / `Board.isOnDevice === false`) `attachBatonGlyphControl`
**no-ops**, so the mouse remains the only baton driver off-device. On a Board it
subscribes to `Board.input`, filters to Glyph contacts, converts coordinates, and
calls `placeBaton` / `clearBaton`.

---

## 4. How to wire it in (after the mouse baton lands)

The baton worker owns the real Baton API in `core/game.ts`. When that's merged,
adapt its mouse entry points to the `BatonController` shape and attach the glyph
control alongside the existing mouse path — **mouse and Piece both feed the same
baton action**:

```ts
// In the bootstrap (e.g. main.ts), AFTER the baton control scheme exists.
import { attachBatonGlyphControl } from "./board/batonGlyphAdapter";

// `game` here is whatever the baton worker exposes. Map its mouse-baton calls
// onto the three BatonController methods — do NOT duplicate game logic, just
// forward to the same functions the mouse path already calls:
const batonController = {
  placeBaton: ({ position, course, sourceId }) =>
    game.placeBatonOfCommand(position, course, sourceId), // <- real API TBD
  clearBaton: ({ sourceId }) =>
    game.clearBatonOfCommand(sourceId),                   // <- real API TBD
};

// No-op in the browser; live on a Board. Leave batonGlyphId undefined until you
// log the robot Piece's real id (see §1).
const detachGlyphBaton = await attachBatonGlyphControl(canvas, batonController, {
  // batonGlyphId: <robot piece id from logging>,
});
// call detachGlyphBaton() on teardown.
```

Guarantees / guard rails:

- **Browser fallback preserved**: the adapter is gated on `Board.isOnDevice`; the
  mouse path is untouched.
- **No shared-file edits required to ship the adapter** — only this small
  bootstrap wiring, which you control during the merge.
- **Track Pieces by `contactId`**, never `glyphId` (a `glyphId` is a piece *type*),
  per the repo's AGENTS.md guidance — the adapter already keys on `contactId`.
- The exact `placeBaton`/`clearBaton` → game-function mapping is the only thing to
  finalize once the baton API names exist.
