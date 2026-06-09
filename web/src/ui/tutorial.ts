// In-battle onboarding — the objective banner shown at every battle start and
// the first-battle step-by-step hint strip. Built for the player who skipped
// the How-to-Play cards: within the first minute of a battle it tells them WHAT
// they're trying to do (sink the enemy fleet) and HOW to do it with the real
// controls (mouse fallback in the browser; the physical Baton Piece + fingers
// on Board hardware — wording picked by the `onDevice` flag, same gate as the
// rest of the app).
//
// The hint sequence is event-driven: the Game notifies this module when the
// player actually steers / trims sail / lands a broadside, and the strip
// advances on the real action (steps already performed are auto-skipped). A
// "seen" flag in localStorage (same persistence pattern as the scenario store's
// browser path) keeps it to the FIRST battle only; the "?" how-to overlay
// carries a "Replay battle hints" button to re-arm it.

export type TutorialAction = "steer" | "sail" | "fire";

const SEEN_KEY = "broadsides.battleTutorialSeen.v1";

/** How long the objective banner lingers if the player doesn't interact. */
const BANNER_SECONDS = 8;
/** How long the final (no-action) step lingers before self-dismissing. */
const FINAL_STEP_SECONDS = 14;

interface Step {
  /** The player action that advances this step; null = final, timed step. */
  action: TutorialAction | null;
  /** Hint copy for the browser preview (mouse fallback controls). */
  browser: string;
  /** Hint copy on Board hardware (physical Baton Piece + finger controls). */
  device: string;
}

const STEPS: ReadonlyArray<Step> = [
  {
    action: "steer",
    browser:
      "<b>Set a course:</b> press the gold <b>Baton</b> roundel by your ships and " +
      "<b>drag</b> the way you want to sail. Green line = fast; red = into the wind (you'll stall).",
    device:
      "<b>Set a course:</b> hold your <b>Baton</b> piece and <b>rotate</b> it — " +
      "the whole squadron turns to sail where it points. Green course = fast; red = into the wind.",
  },
  {
    action: "sail",
    browser:
      "<b>Trim sail:</b> drag the <b>mast</b> beside the Baton — top is Full Sail, " +
      "bottom is Heave-To (stop). The disc next to it switches shot type.",
    device:
      "<b>Trim sail:</b> drag a finger up or down the <b>mast</b> beside your Baton — top is Full Sail, " +
      "bottom is Heave-To (stop). Tap the disc next to it to switch shot type.",
  },
  {
    action: "fire",
    browser:
      "<b>Guns fire on their own:</b> steer so an enemy lies off your <b>side</b>, in range — " +
      "then your broadsides let fly. Tap <b>Range</b> (top corner) to see your gun arcs.",
    device:
      "<b>Guns fire on their own:</b> steer so an enemy lies off your <b>side</b>, in range — " +
      "then your broadsides let fly. Tap <b>Range</b> (top corner) to see your gun arcs.",
  },
  {
    action: null,
    browser:
      "<b>Sink every enemy ship to win.</b> Click open sea near other ships to move the " +
      "Baton and take command of them. Good hunting!",
    device:
      "<b>Sink every enemy ship to win.</b> Lift your Baton piece and set it down by other " +
      "ships to take command of them. Good hunting!",
  },
];

function el<T extends HTMLElement>(id: string): T {
  const e = document.getElementById(id);
  if (!e) throw new Error(`Missing tutorial element #${id}`);
  return e as T;
}

export class Tutorial {
  private readonly banner = el("objective-banner");
  private readonly objectiveSub = el("objective-sub");
  private readonly hint = el("tutorial-hint");
  private readonly stepLabel = el("tutorial-step");
  private readonly stepText = el("tutorial-text");
  private readonly skipButton = el<HTMLButtonElement>("tutorial-skip");

  private active = false;
  private inBattle = false;
  private stepIndex = -1;
  /** Actions the player has already performed this battle, so a step whose
   *  action is already done is auto-skipped instead of nagging. */
  private readonly performed = new Set<TutorialAction>();

  private bannerTimer: ReturnType<typeof setTimeout> | null = null;
  private finalTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(private readonly onDevice: boolean) {
    this.objectiveSub.innerHTML = onDevice
      ? "Your <b>Baton</b> piece commands every ship inside its ring — hold and rotate it to steer."
      : "Click the sea beside your ships to place the <b>Baton of Command</b> — it commands every ship inside its ring.";
    this.skipButton.addEventListener("click", () => this.finish());
  }

  // ---- Battle lifecycle (driven by the Game) -----------------------------

  /** A match just left Setup for Playing: show the objective banner (every
   *  battle), and auto-run the hint sequence on the player's first battle. */
  onBattleStart(): void {
    this.inBattle = true;
    this.showBanner();
    if (!this.hasSeen()) this.begin();
  }

  /** The match ended (game over) or was torn down (restart / new scenario). */
  onBattleEnd(): void {
    this.inBattle = false;
    this.hideBanner();
    if (this.active) {
      this.active = false;
      this.hideHint();
    }
  }

  /** First fresh contact while Playing — dismisses the objective banner. */
  onInteraction(): void {
    this.hideBanner();
  }

  // ---- Player-action notifications ---------------------------------------

  /** The hint sequence is waiting on this action right now (lets the Game skip
   *  the broadside-fired scan when nobody is watching for it). */
  wants(action: TutorialAction): boolean {
    return this.active && STEPS[this.stepIndex]?.action === action;
  }

  /** The player performed an action; advance the matching step. */
  notify(action: TutorialAction): void {
    if (!this.active) return;
    this.performed.add(action);
    if (STEPS[this.stepIndex]?.action === action) this.advance();
  }

  /** Re-arm the hints (the "Replay battle hints" button in the "?" overlay):
   *  starts them immediately mid-battle, or on the next battle start. */
  replay(): void {
    try {
      localStorage.removeItem(SEEN_KEY);
    } catch {
      /* storage unavailable — hints simply re-run in-session */
    }
    if (this.inBattle && !this.active) this.begin();
  }

  // ---- Sequence ----------------------------------------------------------

  private begin(): void {
    this.active = true;
    this.performed.clear();
    this.stepIndex = -1;
    document.body.classList.add("tutorial-active");
    this.advance();
  }

  private advance(): void {
    this.clearFinalTimer();
    this.stepIndex++;
    // Auto-skip anything the player already did out of order.
    while (this.stepIndex < STEPS.length) {
      const a = STEPS[this.stepIndex].action;
      if (a === null || !this.performed.has(a)) break;
      this.stepIndex++;
    }
    if (this.stepIndex >= STEPS.length) {
      this.finish();
      return;
    }

    const step = STEPS[this.stepIndex];
    this.stepLabel.textContent = `${this.stepIndex + 1} / ${STEPS.length}`;
    this.stepText.innerHTML = this.onDevice ? step.device : step.browser;
    const final = step.action === null;
    this.skipButton.textContent = final ? "Got it" : "Skip hints";
    if (final) {
      this.finalTimer = setTimeout(() => this.finish(), FINAL_STEP_SECONDS * 1000);
    }
    this.hint.classList.add("show");
  }

  /** Done (completed, timed out, or skipped): persist "seen" and tidy up. */
  private finish(): void {
    this.clearFinalTimer();
    this.active = false;
    this.hideHint();
    try {
      localStorage.setItem(SEEN_KEY, "1");
    } catch {
      /* storage unavailable — at worst the hints re-run next launch */
    }
  }

  private hasSeen(): boolean {
    try {
      return localStorage.getItem(SEEN_KEY) === "1";
    } catch {
      return false;
    }
  }

  // ---- DOM helpers --------------------------------------------------------

  private showBanner(): void {
    this.banner.classList.add("show");
    if (this.bannerTimer) clearTimeout(this.bannerTimer);
    this.bannerTimer = setTimeout(() => this.hideBanner(), BANNER_SECONDS * 1000);
  }

  private hideBanner(): void {
    this.banner.classList.remove("show");
    if (this.bannerTimer) {
      clearTimeout(this.bannerTimer);
      this.bannerTimer = null;
    }
  }

  private hideHint(): void {
    this.hint.classList.remove("show");
    document.body.classList.remove("tutorial-active");
  }

  private clearFinalTimer(): void {
    if (this.finalTimer) {
      clearTimeout(this.finalTimer);
      this.finalTimer = null;
    }
  }
}
