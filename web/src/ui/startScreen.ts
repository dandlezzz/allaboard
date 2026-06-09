// The launch START SCREEN — reuses the existing "How to Play" overlay
// (web/index.html `#howto-overlay`, also reachable any time via the "?" help
// button) as the first thing shown when the app loads.
//
// Flow:
//   1. The how-to-play overlay is shown as the start screen.
//   2. The player DISMISSES it by PLACING THEIR PIECE on the board — detected
//      on hardware via `Board.input.subscribe(...)` as a NEW contact appearing
//      (tracked by `contactId`, never `glyphId`). In the browser preview (no
//      Board) the equivalent fallback is a pointer tap on the screen.
//   3. Placing the piece swaps the "place your piece" prompt for a "Begin
//      Battle" button; tapping it starts the game via the supplied `onStart`
//      callback (which drives the normal match-start path).
//
// The piece-placement listener is torn down as soon as the screen is dismissed
// (or as soon as a piece is detected) so it never competes with the game's own
// piece tracking during play. All SDK access is guarded behind
// `Board.isOnDevice`, so the same build runs with or without hardware.

import { loadBoard } from "../board/sdk";

function el<T extends HTMLElement>(id: string): T {
  const e = document.getElementById(id);
  if (!e) throw new Error(`Missing start-screen element #${id}`);
  return e as T;
}

export class StartScreen {
  private readonly overlay = el("howto-overlay");
  private readonly closeBtn = el<HTMLButtonElement>("howto-close");
  private readonly startBlock = el("howto-start");
  private readonly prompt = el("howto-start-prompt");
  private readonly startBtn = el<HTMLButtonElement>("howto-start-btn");

  /** Disposer for the active piece-placement detector (device or browser). */
  private disposeDetect: (() => void) | null = null;
  private piecePlaced = false;
  private dismissed = false;

  constructor(private readonly onStart: () => void) {
    this.startBtn.addEventListener("click", () => this.start());
  }

  /**
   * Shows the how-to-play overlay as the start screen and arms piece-placement
   * detection. Resolves once the detector is wired (NOT once the game starts).
   */
  async begin(): Promise<void> {
    document.body.classList.add("start-screen");
    this.overlay.hidden = false;
    // The start gate can't be closed with the "×" — you advance by placing a
    // piece. Hide it now and restore it when we hand off to the "?" help modal.
    this.closeBtn.hidden = true;
    this.startBlock.hidden = false;
    this.prompt.hidden = false;
    this.startBtn.hidden = true;
    await this.armPlacementDetection();
  }

  /** Wires the "a piece was placed" trigger: Board contacts on device, a
   *  pointer tap on the overlay in the browser preview. */
  private async armPlacementDetection(): Promise<void> {
    const board = await loadBoard();

    if (board && board.isOnDevice) {
      // Device: watch the live contact frames for a NEW contact appearing
      // (tracked by contactId). Contacts already resting at launch are taken as
      // a baseline so only a freshly-placed piece counts.
      let baseline: Set<number> | null = null;
      const unsubscribe = board.input.subscribe((contacts) => {
        if (baseline === null) {
          baseline = new Set(contacts.map((c) => c.contactId));
          return;
        }
        for (const c of contacts) {
          if (!baseline.has(c.contactId)) {
            this.onPiecePlaced();
            return;
          }
        }
      });
      this.disposeDetect = () => unsubscribe?.();
      return;
    }

    // Browser preview: a pointer tap anywhere on the start screen counts as
    // "placing your piece" (mirrors the pointer fallback used for input).
    const onPointer = (): void => this.onPiecePlaced();
    this.overlay.addEventListener("pointerdown", onPointer);
    this.disposeDetect = () => this.overlay.removeEventListener("pointerdown", onPointer);
  }

  /** First piece placement: tear down the listener and reveal the Start button. */
  private onPiecePlaced(): void {
    if (this.piecePlaced) return;
    this.piecePlaced = true;
    this.teardownDetect();
    this.prompt.hidden = true;
    this.startBtn.hidden = false;
  }

  /** Start button tapped: dismiss the start screen and hand off to the game. */
  private start(): void {
    if (this.dismissed) return;
    this.dismissed = true;
    this.teardownDetect();
    this.overlay.hidden = true;
    this.startBlock.hidden = true;
    this.closeBtn.hidden = false; // restore the "?" help modal's close button
    document.body.classList.remove("start-screen");
    this.onStart();
  }

  private teardownDetect(): void {
    this.disposeDetect?.();
    this.disposeDetect = null;
  }
}
