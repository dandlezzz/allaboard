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
   * Registers the in-match pause context (called when entering the Playing
   * phase) so the hardware menu button opens an overlay offering Restart and the
   * OS-provided Quit. Save is omitted for now (no save implementation yet).
   */
  enterPlaying(): void {
    if (!this.active) return;
    try {
      this.board!.pause!.setContext({
        offerSaveOption: false,
        customButtons: [{ id: RESTART_BUTTON_ID, title: "Restart", icon: "circulararrow" }],
      });
    } catch {
      /* off-device / unsupported: ignore */
    }
    this.ensureSubscribed();
  }

  /**
   * Clears the pause context (called on game over / when returning to Setup) so
   * the overlay reflects that there's no live match to act on.
   */
  clear(): void {
    if (!this.active) return;
    try {
      this.board!.pause!.clearContext?.();
    } catch {
      /* ignore */
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
    if (result.action === "quit" || result.action === "save_and_quit") {
      try {
        this.board?.application?.quit();
      } catch {
        /* ignore */
      }
      return;
    }
    if (result.action === "custom_button" && result.customButtonId === RESTART_BUTTON_ID) {
      this.callbacks.onRestart();
    }
  }
}
