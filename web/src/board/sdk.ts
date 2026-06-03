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
// To re-enable the real SDK for on-device builds, install the tarball:
//   npm install ../../board-websdk/harrishill-board-sdk-0.1.0.tgz
// (fetch it from https://dev.board.fun/). The dynamic import below will then
// resolve at runtime and `Board.isOnDevice` will gate the on-device path.

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
  try {
    const specifier = "@harrishill/board-sdk";
    const mod: Record<string, unknown> = await import(/* @vite-ignore */ specifier);
    const board = (mod.Board ?? mod.default) as BoardLike | undefined;
    cached = board ?? null;
  } catch {
    cached = null;
  }
  return cached;
}
