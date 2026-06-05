// Board ↔ pointer input adapter.
//
// On real hardware we subscribe to `Board.input` and translate each contact
// (finger or glyph) into a unified sample; off-device we fall back to
// mouse/touch pointer events so the game is fully playable in a browser
// preview. It is the web analogue of the Unity project's `Input/InputRouter.cs`
// + `PointerSample`.
//
// The Board SDK is OPTIONAL (see ./sdk.ts): if it isn't installed, this module
// simply uses the pointer fallback and the game runs with the mouse.

import { loadBoard, isGlyphContact, isEndedPhase, type BoardContactLike, type BoardLike } from "./sdk";

export type Vec = { x: number; y: number };

/** A unified input sample, independent of whether it came from Board or mouse. */
export type PointerSample = {
  contactId: number;
  /** Canvas-space position in CSS pixels (top-left origin, Y down). */
  position: Vec;
  /** Glyph orientation in radians, when the contact is a physical piece. */
  orientation: number;
  /** True for tracked physical glyph pieces, false for fingers / mouse. */
  isGlyph: boolean;
  /** Which Piece in the set (`0` = finger, `1+` = Piece); `0` for mouse. */
  glyphId: number;
  /**
   * Whether a hand is on the Piece right now (held vs resting). Always `true`
   * for fingers / mouse and for Pieces whose body doesn't report touch, so the
   * game can gate rotate-to-steer on "held" while degrading gracefully.
   */
  touched: boolean;
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
  // The Web SDK delivers a full per-frame SNAPSHOT of contacts with NO discrete
  // down/up events. We derive began/moved/ended edges by diffing each frame
  // against the previous one, keyed by `contactId` (a `glyphId` is only a Piece
  // *type* identifier, so it must NOT be used for instance tracking):
  //   - a contactId not seen last frame  -> "began"
  //   - a contactId seen in both frames  -> "moved"
  //   - a contactId gone this frame      -> "ended" (synthesised once)
  const previous = new Map<number, PointerSample>();

  const unsubscribe = board.input.subscribe((contacts: ReadonlyArray<BoardContactLike>) => {
    // Coordinate mapping (must match the mouse path below):
    //   SDK contact x,y are DEVICE pixels, origin top-left, Y down.
    //   `getBoundingClientRect()` is in CSS pixels; CSS = device / devicePixelRatio.
    //   So canvas-local CSS px = contact.x / dpr - rect.left  (and y likewise).
    // The mouse fallback emits `event.clientX - rect.left` (already CSS px, top-left
    // Y-down), so both sources land in the same canvas-local space that the
    // renderer's `screenToWorld` consumes.
    const rect = canvas.getBoundingClientRect();
    const dpr = (typeof window !== "undefined" && window.devicePixelRatio) || 1;

    const samples: PointerSample[] = [];
    const seen = new Set<number>();

    for (const contact of contacts) {
      seen.add(contact.contactId);
      const existed = previous.has(contact.contactId);
      const sample: PointerSample = {
        contactId: contact.contactId,
        position: { x: contact.x / dpr - rect.left, y: contact.y / dpr - rect.top },
        // SDK reports Piece orientation in DEGREES; the game uses radians.
        orientation: degToRad(contact.orientation ?? 0),
        isGlyph: isGlyphContact(contact),
        glyphId: Number(contact.glyphId ?? 0),
        // Fingers / untouch-aware Pieces report no flag → treat as held.
        touched: contact.isTouched ?? true,
        phase: existed ? "moved" : "began",
      };
      // An explicit Ended/Canceled phase (e.g. a pause cancels all contacts) is
      // a LIFT even when the contact still appears in this frame: emit the
      // synthetic "ended" and drop it so the per-contactId diff doesn't re-emit.
      if (isEndedPhase(contact.phase)) {
        samples.push({ ...sample, phase: "ended" });
        previous.delete(contact.contactId);
        seen.delete(contact.contactId);
        continue;
      }
      previous.set(contact.contactId, sample);
      samples.push(sample);
    }

    // Contacts present last frame but gone now → emit a single synthetic "ended".
    for (const [contactId, last] of previous) {
      if (!seen.has(contactId)) {
        samples.push({ ...last, phase: "ended" });
        previous.delete(contactId);
      }
    }

    if (samples.length > 0) listener(samples);
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
      glyphId: 0,
      touched: true,
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

function degToRad(deg: number): number {
  return (deg * Math.PI) / 180;
}
