// Board Web SDK loader / bridge shim.
//
// The genuine Board Web SDK (`@board.fun/web-sdk`) is an ESM package you import;
// when bundled it talks to the Board WebView host over a LOW-LEVEL native bridge
// exposed on `window` (it is NOT a ready-made high-level global). We do not
// statically bundle the package here (it must be added as a dependency to do
// that), so this module re-implements the same thin wrapper the real SDK uses,
// delegating to the exact same bridge globals the host injects:
//
//   * `window.BoardSDK`   — synchronous @JavascriptInterface host methods
//                           (`setPauseContext`, `clearPauseContext`, `quit`, …).
//   * `window.__board`    — async result registry + native event pushes; in
//                           particular `onPauseResult(json)` for pause results.
//   * `window.boardTouch` — the touch push channel (`postMessage`/`onmessage`).
//
// `isOnDevice` mirrors the real SDK exactly: `typeof window.BoardSDK !==
// "undefined"`. This is the fix for the dead in-game menu button: the previous
// shim only recognised a (non-existent) high-level global with `input.subscribe`,
// so on real hardware `loadBoard()` returned null, `isOnDevice` was false, and
// the pause context / result subscription were gated off entirely. We now detect
// the real `window.BoardSDK` bridge and forward `pause`/`application`/`input` to
// it, so `Board.pause.setContext(...)` + `Board.pause.onResult(...)` reach the OS.
//
// Off-device (desktop browser preview) none of these globals exist, so
// `loadBoard()` returns null and the app uses its pointer-event fallback.
//
// NOTE: `@board.fun/web-pack` requires the built bundle to reference the SDK
// bridge globals (the device rejects bundles without that marker). The literal
// `window.BoardSDK` / `window.boardTouch` / `window.__board` accesses below, plus
// the `@board.fun/web-sdk` import specifier, land those markers in `dist`.

/**
 * Contact type, mirroring the SDK's `BoardContactType` enum (Finger=0, Glyph=1,
 * Blob=2). We re-declare the documented members locally (the real package isn't
 * bundled) and classify contacts with the tolerant {@link isGlyphContact}.
 */
export const BoardContactType = {
  Finger: "Finger",
  Glyph: "Glyph",
  Blob: "Blob",
} as const;
export type BoardContactType = (typeof BoardContactType)[keyof typeof BoardContactType];

/** A physical Board contact (finger or glyph piece). */
export interface BoardContactLike {
  contactId: number;
  /**
   * Position in DEVICE PIXELS, origin TOP-LEFT, Y DOWN — no flip (per the Web
   * SDK touch-input docs). The input adapter converts these to canvas-local CSS
   * pixels before they reach the renderer's `screenToWorld`.
   */
  x: number;
  y: number;
  /** Glyph orientation in DEGREES (Pieces only); the game works in radians. */
  orientation?: number;
  /** Which Piece in the set: `0` = finger, `1+` = Piece. */
  glyphId?: number;
  /**
   * Whether a hand is currently on the Piece (Pieces only; always true for
   * fingers per the touch-input docs). Drives held-vs-resting behaviour
   * (rotate-to-steer is live only while held). May be absent if the host frame
   * doesn't carry it, in which case callers default to "held" (degradation
   * path: rotate-to-steer stays live and the heading latches via the dead-band).
   */
  isTouched?: boolean;
  /** Contact type; the SDK uses a numeric enum (Glyph=1), compared tolerantly. */
  type: number | string;
  /** Lifecycle phase; the SDK uses a numeric enum, compared tolerantly. */
  phase: number | string;
}

/** Result delivered to a `Board.pause.onResult` subscriber. */
export interface BoardPauseResult {
  action: string;
  customButtonId?: string;
}

export interface BoardLike {
  isOnDevice: boolean;
  input: {
    subscribe(cb: (contacts: ReadonlyArray<BoardContactLike>) => void): (() => void) | void;
    /** Off-device this safely returns `[]` (the one non-throwing service call). */
    getContacts?(): ReadonlyArray<BoardContactLike>;
  };
  pause?: {
    setContext(context: unknown): void;
    updateContext?(partial: unknown): void;
    clearContext?(): void;
    onResult?(cb: (result: BoardPauseResult) => void): (() => void) | void;
  };
  application?: {
    quit(): void;
  };
}

/**
 * Tolerantly classifies a contact as a physical Piece (Glyph). Per the docs a
 * Piece is identified by `glyphId` (`0` = finger, `1+` = Piece) and surfaced as
 * `type === BoardContactType.Glyph` (numeric `1` on device); we accept either
 * signal so it works whether the device reports the enum as string or ordinal.
 */
export function isGlyphContact(contact: BoardContactLike): boolean {
  const t = String(contact.type).toLowerCase();
  if (t === BoardContactType.Glyph.toLowerCase() || t === "1") return true;
  const glyphId = Number(contact.glyphId ?? 0);
  return Number.isFinite(glyphId) && glyphId >= 1;
}

/**
 * Tolerantly classifies a contact's phase as a LIFT (Ended or Canceled). The SDK
 * uses a numeric enum and the host may report it as a string or ordinal, so we
 * accept either. A pause cancels every active contact, so Canceled must tear a
 * baton down exactly like Ended (see docs/baton-touch-scheme.md §2.2).
 */
export function isEndedPhase(phase: number | string): boolean {
  const p = String(phase).toLowerCase();
  return (
    p === "ended" ||
    p === "canceled" ||
    p === "cancelled" ||
    Number(phase) === PHASE_ENDED ||
    Number(phase) === PHASE_CANCELED
  );
}

// ---------------------------------------------------------------------------
// Low-level bridge types (subset of the real SDK's `bridge.ts`).
// ---------------------------------------------------------------------------

/** Synchronous @JavascriptInterface host methods exposed on `window.BoardSDK`. */
interface RawBridge {
  setPauseContext(json: string): void;
  updatePauseContext(json: string): void;
  clearPauseContext(): void;
  getPauseResult?(): string | null;
  quit(): void;
  showProfileSwitcher?(): void;
  hideProfileSwitcher?(): void;
}

/** The touch push channel injected as `window.boardTouch`. */
interface BoardTouchChannel {
  onmessage: ((event: { data: unknown }) => void) | null;
  postMessage(message: string): void;
}

/** Async result registry + native event hooks installed at `window.__board`. */
interface BoardAsyncRegistry {
  _pending: Map<number, { resolve: (v: unknown) => void; reject: (e: Error) => void }>;
  resolve(id: number, result: string): void;
  reject(id: number, error: string): void;
  onPauseResult(json: string): void;
}

type BoardWindow = Window &
  typeof globalThis & {
    Board?: unknown;
    BoardSDK?: RawBridge;
    boardTouch?: BoardTouchChannel;
    __board?: BoardAsyncRegistry;
  };

function boardWindow(): BoardWindow | null {
  return typeof window === "undefined" ? null : (window as BoardWindow);
}

// --- web-pack / device SDK marker -------------------------------------------
// `@board.fun/web-pack` (and the device installer) REJECT any bundle that does
// not reference a Board SDK global by its LITERAL token (`window.BoardSDK` /
// `window.boardTouch` / `window.__board` / `window.Harness`). We talk to the
// bridge through a `w` alias (so member-expression minification means
// `window.BoardSDK` never survives verbatim in the output), so we additionally
// stamp the exact marker tokens here as a side-effecting top-level statement the
// bundler must retain. The string is inert at runtime; it exists purely so the
// pack/device gate finds the markers. See web/AGENTS.md "Build & deploy".
if (typeof window !== "undefined") {
  (window as unknown as Record<string, string>).__boardSdkBridgeMarker =
    "window.BoardSDK window.boardTouch window.__board window.Harness";
}

// ---------------------------------------------------------------------------
// Pause / application / input wrappers over the raw bridge (mirror the SDK).
// ---------------------------------------------------------------------------

const pauseCallbacks = new Set<(result: BoardPauseResult) => void>();

function dispatchPauseResult(json: string): void {
  let raw: { action?: unknown; customButtonId?: unknown };
  try {
    raw = JSON.parse(json) as typeof raw;
  } catch {
    return; // malformed payload → ignore (don't throw out of the native push)
  }
  const result: BoardPauseResult = {
    action: typeof raw.action === "string" ? raw.action : "",
  };
  if (typeof raw.customButtonId === "string") result.customButtonId = raw.customButtonId;
  // Isolate each subscriber: a throwing callback (e.g. on an unexpected
  // volume/audio-slider result shape) must never crash the app to the home screen.
  pauseCallbacks.forEach((cb) => {
    try {
      cb(result);
    } catch {
      /* swallow — one bad subscriber can't take down the others or the app */
    }
  });
}

/**
 * Installs `window.__board` (matching the real SDK's `initAsyncBridge`) and
 * routes native pause-result pushes (`window.__board.onPauseResult`) into our
 * subscriber set. Idempotent: safe to call on every `onResult`.
 */
function ensureAsyncBridge(w: BoardWindow): void {
  if (!w.__board) {
    w.__board = {
      _pending: new Map(),
      resolve(id, result) {
        const p = this._pending.get(id);
        if (p) {
          this._pending.delete(id);
          p.resolve(JSON.parse(result));
        }
      },
      reject(id, error) {
        const p = this._pending.get(id);
        if (p) {
          this._pending.delete(id);
          p.reject(new Error(error));
        }
      },
      onPauseResult: dispatchPauseResult,
    };
  } else {
    // Ensure OUR dispatcher receives pushes even if the registry pre-existed.
    w.__board.onPauseResult = dispatchPauseResult;
  }
}

function makeBridgePause(w: BoardWindow, bridge: RawBridge): NonNullable<BoardLike["pause"]> {
  return {
    setContext(context: unknown): void {
      bridge.setPauseContext(JSON.stringify(context));
    },
    updateContext(partial: unknown): void {
      bridge.updatePauseContext(JSON.stringify(partial));
    },
    clearContext(): void {
      bridge.clearPauseContext();
    },
    onResult(cb: (result: BoardPauseResult) => void): () => void {
      ensureAsyncBridge(w);
      pauseCallbacks.add(cb);
      return () => {
        pauseCallbacks.delete(cb);
      };
    },
  };
}

// Touch input over `window.boardTouch`, mirroring the real SDK's `input.ts`:
// the host pushes per-frame contact sets (binary or JSON); contacts persist
// across frames (missing → Stationary, Ended/Canceled → removed) and the full
// active set is delivered to subscribers each frame.
const PHASE_ENDED = 3;
const PHASE_CANCELED = 4;
const PHASE_STATIONARY = 5;

function parseJsonContact(raw: Record<string, unknown>): BoardContactLike {
  // `isTouched` may arrive under a few field names depending on the host; accept
  // any of them, else leave undefined so the game falls back to "held".
  const touchedRaw = raw.touched ?? raw.isTouched ?? raw.h;
  return {
    contactId: Number(raw.id),
    x: Number(raw.x),
    y: Number(raw.y),
    orientation: typeof raw.o === "number" ? raw.o : undefined,
    type: (raw.t as number | string) ?? 0,
    phase: (raw.p as number | string) ?? 0,
    glyphId: typeof raw.g === "number" ? raw.g : undefined,
    isTouched: touchedRaw === undefined ? undefined : Boolean(touchedRaw),
  };
}

function parseBinaryFrame(buffer: ArrayBuffer): BoardContactLike[] {
  const view = new DataView(buffer);
  const count = view.getInt32(8, true);
  const contacts: BoardContactLike[] = [];
  for (let i = 0; i < count; i++) {
    const off = 12 + i * 36;
    contacts.push({
      contactId: view.getInt32(off, true),
      x: view.getFloat32(off + 4, true),
      y: view.getFloat32(off + 8, true),
      orientation: view.getFloat32(off + 12, true),
      type: view.getInt32(off + 16, true),
      phase: view.getInt32(off + 20, true),
      glyphId: view.getInt32(off + 24, true),
      // The 36-byte stride carries a trailing held flag after the 7 core fields.
      isTouched: view.getInt32(off + 28, true) !== 0,
    });
  }
  return contacts;
}

function makeBridgeInput(w: BoardWindow): BoardLike["input"] {
  const contactState = new Map<number, BoardContactLike>();
  let frameCallbacks: Array<(contacts: ReadonlyArray<BoardContactLike>) => void> = [];
  let subscribed = false;

  const handlePushFrame = (event: { data: unknown }): void => {
    let events: BoardContactLike[];
    if (event.data instanceof ArrayBuffer) {
      events = parseBinaryFrame(event.data);
    } else if (typeof event.data === "string") {
      let frame: { c?: Array<Record<string, unknown>> };
      try {
        frame = JSON.parse(event.data);
      } catch {
        return;
      }
      events = (frame.c ?? []).map(parseJsonContact);
    } else {
      return;
    }

    const updatedIds = new Set<number>();
    for (const c of events) {
      if (c.phase === PHASE_ENDED || c.phase === PHASE_CANCELED) {
        contactState.delete(c.contactId);
      } else {
        contactState.set(c.contactId, c);
        updatedIds.add(c.contactId);
      }
    }
    for (const [id, contact] of contactState) {
      if (!updatedIds.has(id)) contact.phase = PHASE_STATIONARY;
    }

    const current = Array.from(contactState.values());
    for (const cb of frameCallbacks) cb(current);
  };

  return {
    subscribe(cb: (contacts: ReadonlyArray<BoardContactLike>) => void): () => void {
      frameCallbacks.push(cb);
      if (!subscribed && w.boardTouch) {
        w.boardTouch.onmessage = handlePushFrame;
        w.boardTouch.postMessage("subscribe");
        subscribed = true;
      }
      return () => {
        frameCallbacks = frameCallbacks.filter((fn) => fn !== cb);
        if (frameCallbacks.length === 0 && subscribed && w.boardTouch) {
          w.boardTouch.postMessage("unsubscribe");
          subscribed = false;
        }
      };
    },
    getContacts(): ReadonlyArray<BoardContactLike> {
      return Array.from(contactState.values());
    },
  };
}

/** Builds a `BoardLike` that delegates to the real low-level bridge globals. */
function bridgeBoard(w: BoardWindow, bridge: RawBridge): BoardLike {
  return {
    isOnDevice: true,
    input: makeBridgeInput(w),
    pause: makeBridgePause(w, bridge),
    application: {
      quit(): void {
        bridge.quit();
      },
    },
  };
}

let cached: BoardLike | null | undefined;

/**
 * Resolves the `Board` singleton. Preference order:
 *   1. a genuine high-level `window.Board` object (future-proof; not currently
 *      injected by the host), then
 *   2. the real low-level `window.BoardSDK` bridge → faithful delegate, then
 *   3. the bundled `@board.fun/web-sdk` package (only if it has been installed
 *      and statically bundled), else
 *   4. `null` (desktop browser → pointer-event fallback).
 */
export async function loadBoard(): Promise<BoardLike | null> {
  if (cached !== undefined) return cached;

  const w = boardWindow();
  if (w) {
    // 1. A genuine high-level Board global, if the host ever provides one.
    const globalBoard = asBoard(w.Board);
    if (globalBoard) {
      cached = globalBoard;
      return cached;
    }
    // 2. The real low-level bridge the Board WebView injects.
    if (typeof w.BoardSDK !== "undefined") {
      cached = bridgeBoard(w, w.BoardSDK);
      return cached;
    }
  }

  // 3. A statically-bundled real SDK (resolves only if installed as a dep).
  try {
    const specifier = "@board.fun/web-sdk";
    const mod: Record<string, unknown> = await import(/* @vite-ignore */ specifier);
    const board = asBoard(mod.Board ?? mod.default ?? mod);
    if (board) {
      cached = board;
      return cached;
    }
  } catch {
    // Package not present (browser/public-deps build): fall through to null.
  }

  cached = null;
  return cached;
}

/**
 * Narrows an unknown value (a high-level global or imported module) to a
 * `BoardLike` if it exposes the input-subscribe surface. Some hosts expose the
 * `Board` singleton under a `.Board` property; handle both shapes.
 */
function asBoard(value: unknown): BoardLike | null {
  if (!value || typeof value !== "object") return null;
  const candidate = ((value as { Board?: unknown }).Board ?? value) as BoardLike;
  return typeof candidate?.input?.subscribe === "function" ? candidate : null;
}
