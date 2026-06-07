// OS pause-overlay controller (Board Web SDK `Board.pause` + `Board.application`).
//
// Board owns the hardware menu button and the pause overlay UI; a game only
// supplies CONTEXT (what options to show) and reads RESULTS (what the player
// chose). Without a registered context the menu button does nothing in-game,
// so we set one whenever the match is in play.
//
// Everything here is gated behind `Board.isOnDevice` and wrapped in try/catch:
// off-device the `pause`/`application` service calls THROW (sync) or REJECT
// (async) because the native bridge is absent, so the browser build must never
// hit them. With no Board (browser preview) every method is a no-op.

import type { BoardLike, BoardPauseResult } from "./sdk";

export interface PauseMenuCallbacks {
  /** Invoked when the player taps the custom "Restart" button in the overlay. */
  onRestart: () => void;
}

const RESTART_BUTTON_ID = "restart";
/** The custom "Restart" button shown in the in-match / game-over overlay. */
const RESTART_BUTTON = { id: RESTART_BUTTON_ID, title: "Restart", icon: "circulararrow" } as const;

export class PauseMenu {
  private readonly board: BoardLike | null;
  private readonly callbacks: PauseMenuCallbacks;
  private subscribed = false;

  constructor(board: BoardLike | null, callbacks: PauseMenuCallbacks) {
    this.board = board;
    this.callbacks = callbacks;
  }

  /** True only on a real Board with the pause domain available. */
  private get active(): boolean {
    return !!this.board?.isOnDevice && !!this.board.pause;
  }

  /**
   * Setup / start screen: there's no live match to Restart, but we STILL register
   * a (Restart-less) context so the hardware menu button — and the OS-provided
   * Quit on it — work on the start screen. Previously the context was CLEARED
   * outside the Playing phase, which left the menu button dead on the start and
   * game-over screens (no way to quit the app from there).
   */
  enterSetup(): void {
    this.applyContext([]);
  }

  /**
   * In-match pause context (Playing phase): the hardware menu button opens an
   * overlay offering Restart and the OS-provided Quit. Save is omitted for now
   * (no save implementation yet).
   */
  enterPlaying(): void {
    this.applyContext([RESTART_BUTTON]);
  }

  /**
   * Game-over context: keep the menu live so the player can start a fresh battle
   * (Restart) or Quit straight from the overlay without tapping the canvas.
   */
  enterGameOver(): void {
    this.applyContext([RESTART_BUTTON]);
  }

  /**
   * Sets the pause context for the current phase. `setContext` is a FULL
   * replacement (unspecified fields revert to defaults), so we always pass the
   * complete context. Subscription to results is ensured FIRST so a choice made
   * in any phase (including a Quit from the start screen) is actually handled.
   */
  private applyContext(customButtons: ReadonlyArray<typeof RESTART_BUTTON>): void {
    if (!this.active) return;
    this.ensureSubscribed();
    try {
      this.board!.pause!.setContext({ offerSaveOption: false, customButtons });
    } catch {
      /* off-device / unsupported: ignore */
    }
  }

  private ensureSubscribed(): void {
    if (this.subscribed || !this.active || typeof this.board!.pause!.onResult !== "function") {
      return;
    }
    try {
      this.board!.pause!.onResult((result) => this.handleResult(result));
      this.subscribed = true;
    } catch {
      /* ignore */
    }
  }

  private handleResult(result: BoardPauseResult): void {
    // Tolerant of any result shape the OS sends (e.g. volume/audio-slider changes
    // come back on every overlay dismissal): act ONLY on the two actions we know,
    // ignore everything else, and never let a handler throw out of the native
    // pause-result push (which would crash the app to the home screen).
    try {
      const action = result?.action;
      if (action === "quit" || action === "save_and_quit") {
        try {
          this.board?.application?.quit();
        } catch {
          /* ignore */
        }
        return;
      }
      if (action === "custom_button" && result?.customButtonId === RESTART_BUTTON_ID) {
        this.callbacks.onRestart();
      }
      // Any other action (resume / dismiss / audio-track change / unknown) → no-op.
    } catch {
      /* never propagate a pause-result error */
    }
  }
}
