// Optional Board Web SDK loader.
//
// The private Board Web SDK (`@harrishill/board-sdk`) is auth-gated and is NOT a
// hard dependency of this project, so `npm install` and `npm run build` work
// with public deps only. We therefore:
//   1. describe the (tiny) slice of the SDK we use with LOCAL interfaces here,
//      instead of importing types from the (possibly-absent) package, and
//   2. load the real module at runtime via a guarded dynamic import whose
//      specifier is a variable + `@vite-ignore`, so the bundler never tries to
//      resolve it at build time.
//
// The Board Web SDK is now published publicly as `@board.fun/web-sdk` (it was
// previously the private `@harrishill/board-sdk`). To re-enable the real SDK for
// on-device builds, install it:
//   npm install @board.fun/web-sdk
// The dynamic import below will then resolve at runtime and `Board.isOnDevice`
// will gate the on-device path.
//
// NOTE: `@board.fun/web-pack` requires the built bundle to reference the SDK
// (the device rejects bundles with no `@board.fun/web-sdk` marker). Listing the
// literal package name in `loadBoard()` below lands that marker in `dist`, so the
// app can be packaged into a `.webapp.zip` even when the SDK is dynamically
// (rather than statically) imported.

/** A physical Board contact (finger or glyph piece). */
export interface BoardContactLike {
  contactId: number;
  /** Screen position in CSS pixels (top-left origin), per the SDK. */
  x: number;
  y: number;
  orientation?: number;
  /** Contact type; the SDK uses an enum, compared loosely here. */
  type: number | string;
  /** Lifecycle phase; the SDK uses an enum, compared loosely here. */
  phase: number | string;
}

export interface BoardLike {
  isOnDevice: boolean;
  input: {
    subscribe(cb: (contacts: ReadonlyArray<BoardContactLike>) => void): () => void;
  };
  pause?: {
    setContext(context: unknown): void;
  };
}

let cached: BoardLike | null | undefined;

/**
 * Attempts to load the Board Web SDK. Returns the `Board` singleton if the
 * package is installed, or `null` (public-deps-only / browser dev) otherwise.
 */
export async function loadBoard(): Promise<BoardLike | null> {
  if (cached !== undefined) return cached;

  // 1. On a Board device the WebView host injects the SDK bridge as a global.
  //    Detect it directly. Referencing these `window.*` names also lands the SDK
  //    marker that `@board.fun/web-pack` / the device install gate require in the
  //    built bundle (it scans for window.BoardSDK / window.boardTouch /
  //    window.__board / window.Harness).
  if (typeof window !== "undefined") {
    // Access each global by its full `window.<name>` path (not via an alias) so
    // the literal marker strings survive minification for the install gate.
    const bridge =
      (window as Window & { BoardSDK?: unknown }).BoardSDK ??
      (window as Window & { boardTouch?: unknown }).boardTouch ??
      (window as Window & { __board?: unknown }).__board ??
      (window as Window & { Harness?: unknown }).Harness;
    const board = asBoard(bridge);
    if (board) {
      cached = board;
      return cached;
    }
  }

  // 2. Otherwise try the packaged SDK (current public name, then legacy private).
  const specifiers = ["@board.fun/web-sdk", "@harrishill/board-sdk"];
  for (const specifier of specifiers) {
    try {
      const mod: Record<string, unknown> = await import(/* @vite-ignore */ specifier);
      const board = asBoard(mod.Board ?? mod.default ?? mod);
      if (board) {
        cached = board;
        return cached;
      }
    } catch {
      // Package not present (browser/public-deps build): try the next specifier.
    }
  }
  cached = null;
  return cached;
}

/**
 * Narrows an unknown value (global bridge or imported module) to a `BoardLike`
 * if it exposes the input-subscribe surface this app uses. Some hosts expose the
 * `Board` singleton under a `.Board` property; handle both shapes.
 */
function asBoard(value: unknown): BoardLike | null {
  if (!value || typeof value !== "object") return null;
  const candidate = ((value as { Board?: unknown }).Board ?? value) as BoardLike;
  return typeof candidate?.input?.subscribe === "function" ? candidate : null;
}
