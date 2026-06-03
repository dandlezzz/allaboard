// Board ↔ pointer input adapter.
//
// This mirrors how Board-binho consumes the Board Web SDK: on real hardware we
// subscribe to `Board.input` and translate each contact (finger or glyph) into a
// unified sample; off-device we fall back to mouse/touch pointer events so the
// game is fully playable in a browser preview. It is the web analogue of the
// Unity project's `Input/InputRouter.cs` + `PointerSample`.
//
// The Board SDK is OPTIONAL (see ./sdk.ts): if it isn't installed, this module
// simply uses the pointer fallback and the game runs with the mouse.

import { loadBoard, type BoardContactLike, type BoardLike } from "./sdk";

export type Vec = { x: number; y: number };

/** A unified input sample, independent of whether it came from Board or mouse. */
export type PointerSample = {
  contactId: number;
  /** Canvas-space position in CSS pixels (top-left origin). */
  position: Vec;
  /** Glyph orientation in radians, when the contact is a physical piece. */
  orientation: number;
  /** True for tracked physical glyph pieces, false for fingers / mouse. */
  isGlyph: boolean;
  phase: "began" | "moved" | "ended";
};

export type PointerListener = (samples: ReadonlyArray<PointerSample>) => void;

/**
 * Subscribes to the active input source and invokes `listener` with the current
 * set of samples each event. Returns a disposer.
 *
 * On device (Board SDK present and `Board.isOnDevice`): wires
 * `Board.input.subscribe(...)`. Otherwise: wires canvas pointer events.
 */
export async function createInputAdapter(
  canvas: HTMLCanvasElement,
  listener: PointerListener,
): Promise<() => void> {
  const board = await loadBoard();
  if (board && board.isOnDevice) {
    return subscribeBoard(board, canvas, listener);
  }
  return subscribePointerFallback(canvas, listener);
}

function subscribeBoard(
  board: BoardLike,
  canvas: HTMLCanvasElement,
  listener: PointerListener,
): () => void {
  const unsubscribe = board.input.subscribe((contacts: ReadonlyArray<BoardContactLike>) => {
    const rect = canvas.getBoundingClientRect();
    const samples: PointerSample[] = [];

    for (const contact of contacts) {
      const phase = mapPhase(contact.phase);
      if (!phase) continue;

      samples.push({
        // Track physical piece instances by contactId (a glyphId is a piece
        // *type* identifier only).
        contactId: contact.contactId,
        position: { x: contact.x - rect.left, y: contact.y - rect.top },
        orientation: contact.orientation ?? 0,
        isGlyph: isGlyph(contact.type),
        phase,
      });
    }

    listener(samples);
  });

  return () => unsubscribe?.();
}

function subscribePointerFallback(
  canvas: HTMLCanvasElement,
  listener: PointerListener,
): () => void {
  const active = new Map<number, PointerSample>();

  const toPosition = (event: PointerEvent): Vec => {
    const rect = canvas.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  };

  const emit = (): void => listener(Array.from(active.values()));

  const onDown = (event: PointerEvent): void => {
    canvas.setPointerCapture(event.pointerId);
    active.set(event.pointerId, {
      contactId: event.pointerId,
      position: toPosition(event),
      orientation: 0,
      isGlyph: false,
      phase: "began",
    });
    emit();
  };

  const onMove = (event: PointerEvent): void => {
    const sample = active.get(event.pointerId);
    if (!sample) return;
    sample.position = toPosition(event);
    sample.phase = "moved";
    emit();
  };

  const onUp = (event: PointerEvent): void => {
    const sample = active.get(event.pointerId);
    if (sample) {
      sample.position = toPosition(event);
      sample.phase = "ended";
      emit();
    }
    active.delete(event.pointerId);
  };

  canvas.addEventListener("pointerdown", onDown);
  canvas.addEventListener("pointermove", onMove);
  canvas.addEventListener("pointerup", onUp);
  canvas.addEventListener("pointercancel", onUp);

  return () => {
    canvas.removeEventListener("pointerdown", onDown);
    canvas.removeEventListener("pointermove", onMove);
    canvas.removeEventListener("pointerup", onUp);
    canvas.removeEventListener("pointercancel", onUp);
  };
}

// The SDK enums aren't available without the package, so compare loosely against
// the documented string / numeric forms.
function mapPhase(phase: number | string): PointerSample["phase"] | null {
  const p = String(phase).toLowerCase();
  if (p === "began" || p === "0") return "began";
  if (p === "moved" || p === "stationary" || p === "1" || p === "2") return "moved";
  if (p === "ended" || p === "3") return "ended";
  // canceled / none → ignore.
  return null;
}

function isGlyph(type: number | string): boolean {
  const t = String(type).toLowerCase();
  return t === "glyph" || t === "1";
}
