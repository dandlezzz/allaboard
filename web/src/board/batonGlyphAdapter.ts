// Glyph -> Baton of Command adapter (PLAN / DROP-IN, not yet wired).
//
// This is an ADDITIVE module: it is intentionally NOT imported by `input.ts`,
// `game.ts`, or anything under `rendering/` yet, so it does not collide with the
// in-progress mouse-driven Baton of Command control scheme. Once that lands,
// wire `attachBatonGlyphControl(...)` into the bootstrap with a real
// `BatonController` and delete this comment block.
//
// Concept: a physical robot Piece (a Glyph contact) placed on the Board IS the
// Baton of Command. Where the Piece sits = where the baton is placed (commands
// the nearest friendly ship / command bubble). The Piece's facing = the
// commanded course. Lifting the Piece clears the baton, exactly like releasing
// the mouse does in the browser fallback.
//
// It reuses the same optional SDK loader as the live input adapter, so on a
// browser (no SDK / `Board.isOnDevice === false`) this is a no-op and the mouse
// remains the only baton driver.

import { loadBoard, type BoardContactLike, type BoardLike } from "./sdk";

export type Vec = { x: number; y: number };

// `sdk.ts` describes only the fields the live input adapter needs. Glyph (Piece)
// contacts additionally carry `glyphId` (which Piece in the set; 0 = finger,
// 1+ = Piece per the Web SDK). We widen the shared type locally rather than edit
// the shared `sdk.ts`, to stay collision-free with the baton worker.
type GlyphContact = BoardContactLike & { glyphId?: number };

/**
 * The slice of the Baton-of-Command API the mouse control scheme exposes.
 *
 * The baton worker owns the real type; this is the minimal shape this adapter
 * needs. When merging, replace this with an import of the real controller and
 * map these three calls onto whatever the mouse path already calls:
 *   - mouse "press"/"drag" at a point      -> placeBaton(...)
 *   - mouse "drag" to set a heading         -> (orientation arg of placeBaton)
 *   - mouse "release"                        -> clearBaton(...)
 */
export interface BatonController {
  /**
   * Place / move the baton. `position` is canvas-space CSS px (top-left origin),
   * the same space the mouse fallback already emits. `course` is the commanded
   * heading in RADIANS (counter-clockwise from vertical/up), or `undefined` when
   * the piece has no meaningful facing yet. `sourceId` lets multiple physical
   * batons (multiplayer) be tracked independently.
   */
  placeBaton(args: { position: Vec; course?: number; sourceId: number }): void;
  /** Remove the baton previously placed by `sourceId` (piece lifted/cancelled). */
  clearBaton(args: { sourceId: number }): void;
}

export interface BatonGlyphOptions {
  /**
   * Restrict control to a specific Piece type (e.g. the robot Piece's glyphId).
   * Leave undefined to accept ANY Glyph (recommended until you log the real id
   * by placing the robot Piece on the Board once — see BOARD_HARDWARE.md).
   */
  batonGlyphId?: number;
  /**
   * Map the Board panel (always 1920x1080 px, top-left origin, Y down) into the
   * game's canvas coordinate space. Defaults to using the live canvas rect, i.e.
   * treating Board pixels as canvas CSS px offset by the canvas position. Provide
   * a custom function if your renderer uses a virtual/world coordinate system.
   */
  toCanvas?: (boardPx: Vec) => Vec;
}

const BOARD_PANEL = { width: 1920, height: 1080 } as const;

/**
 * Subscribes to Board glyph contacts and drives the Baton of Command. Returns a
 * disposer. On a browser (no SDK / not on device) it does nothing and returns a
 * no-op disposer, so the existing mouse baton path is the sole driver there.
 */
export async function attachBatonGlyphControl(
  canvas: HTMLCanvasElement,
  baton: BatonController,
  options: BatonGlyphOptions = {},
): Promise<() => void> {
  const board = await loadBoard();
  if (!board || !board.isOnDevice) {
    // Browser preview / SDK absent: mouse remains the baton driver.
    return () => {};
  }
  return subscribeGlyphBaton(board, canvas, baton, options);
}

function subscribeGlyphBaton(
  board: BoardLike,
  canvas: HTMLCanvasElement,
  baton: BatonController,
  options: BatonGlyphOptions,
): () => void {
  const toCanvas =
    options.toCanvas ??
    ((boardPx: Vec): Vec => {
      // Default: Board panel px -> canvas CSS px. Scale the 1920x1080 panel to
      // the on-screen canvas rect so placement lines up with what's drawn.
      const rect = canvas.getBoundingClientRect();
      return {
        x: (boardPx.x / BOARD_PANEL.width) * rect.width,
        y: (boardPx.y / BOARD_PANEL.height) * rect.height,
      };
    });

  // Track which contactIds we've placed so we can clear on lift.
  const active = new Set<number>();

  const unsubscribe = board.input.subscribe((contacts: ReadonlyArray<BoardContactLike>) => {
    for (const raw of contacts) {
      const contact = raw as GlyphContact;
      if (!isGlyph(contact.type)) continue; // ignore fingers
      if (options.batonGlyphId !== undefined && Number(contact.glyphId) !== options.batonGlyphId) {
        continue; // not the robot/baton Piece
      }

      const phase = mapPhase(contact.phase);
      const position = toCanvas({ x: contact.x, y: contact.y });
      // Web SDK reports orientation in DEGREES; the game uses radians.
      const course = contact.orientation === undefined ? undefined : degToRad(contact.orientation);

      switch (phase) {
        case "began":
        case "moved":
          active.add(contact.contactId);
          baton.placeBaton({ position, course, sourceId: contact.contactId });
          break;
        case "ended":
          if (active.delete(contact.contactId)) {
            baton.clearBaton({ sourceId: contact.contactId });
          }
          break;
        default:
          break; // stationary / unknown: leave the baton where it is
      }
    }
  });

  return () => unsubscribe?.();
}

function degToRad(deg: number): number {
  return (deg * Math.PI) / 180;
}

// The SDK enums aren't available without the package; compare loosely against
// the documented string / numeric forms (mirrors input.ts).
function isGlyph(type: number | string): boolean {
  const t = String(type).toLowerCase();
  return t === "glyph" || t === "1";
}

function mapPhase(phase: number | string): "began" | "moved" | "ended" | "other" {
  const p = String(phase).toLowerCase();
  if (p === "began" || p === "0") return "began";
  if (p === "moved" || p === "1") return "moved";
  if (p === "ended" || p === "canceled" || p === "cancelled" || p === "3" || p === "4") return "ended";
  return "other"; // stationary (2) etc.
}
