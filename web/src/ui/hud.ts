// Heads-up display — a DOM port of Unity `UI/HudController.cs`. Drives the wind
// indicator, per-side fleet status, the control hint, the win banner, AND the
// "Broadsides" start screen (opponent selector + placement status) shown during
// the Setup phase. The in-game Rematch button is wired here too.

import { Faction, accentCss, displayName } from "../core/faction";
import { normalize360 } from "../core/nav";
import { roundToInt } from "../core/mathf";
import { AIPersona } from "../ai/fleetAI";
import type { Ship } from "../ships/ship";
import type { Wind } from "../combat/wind";

/** The selected opponent on the start screen: an AI persona, or 2-player. */
export type Opponent = AIPersona | "human";

function el<T extends HTMLElement>(id: string): T {
  const e = document.getElementById(id);
  if (!e) throw new Error(`Missing HUD element #${id}`);
  return e as T;
}

export class Hud {
  private readonly windArrow = el("wind-arrow");
  private readonly windLabel = el("wind-label");
  private readonly fleetBritish = el("fleet-british");
  private readonly fleetFranco = el("fleet-franco");
  private readonly banner = el("banner");
  private readonly setupStatus = el("setup-status");
  private readonly resetButton = el<HTMLButtonElement>("reset-button");

  // Start-screen opponent selector: the three AI personas + a 2-player option.
  // Picking one starts a fresh match vs that opponent; the active one is lit.
  private readonly opponentButtons: ReadonlyArray<{ key: Opponent; button: HTMLButtonElement }> = [
    { key: AIPersona.Standard, button: el<HTMLButtonElement>("persona-standard") },
    { key: AIPersona.Turtle, button: el<HTMLButtonElement>("persona-turtle") },
    { key: AIPersona.Tactician, button: el<HTMLButtonElement>("persona-giga") },
    { key: "human", button: el<HTMLButtonElement>("opponent-human") },
  ];

  constructor(
    onSelectPersona: (persona: AIPersona) => void,
    onSelectVsHuman: () => void,
    onReset: () => void,
  ) {
    this.fleetBritish.style.color = accentCss(Faction.British);
    this.fleetFranco.style.color = accentCss(Faction.FrancoSpanish);
    this.resetButton.addEventListener("click", onReset);
    for (const { key, button } of this.opponentButtons) {
      button.addEventListener("click", () => {
        if (key === "human") onSelectVsHuman();
        else onSelectPersona(key);
      });
    }
  }

  /** Highlights the currently-selected opponent (an AI persona or "human"). */
  setOpponent(active: Opponent): void {
    for (const entry of this.opponentButtons) {
      entry.button.classList.toggle("active", entry.key === active);
    }
  }

  /**
   * Shows/hides the start screen (Setup phase) and sets its status line.
   * Toggling `body.phase-setup` lets the stylesheet reveal the start overlay and
   * hide in-battle chrome (wind/fleet panels, control hint) while players choose
   * an opponent and place their command pieces.
   */
  setSetupOverlay(active: boolean, status: string): void {
    document.body.classList.toggle("phase-setup", active);
    if (active) this.setupStatus.textContent = status;
  }

  refresh(wind: Wind, ships: ReadonlyArray<Ship>, gameOver: boolean, winner: Faction): void {
    this.updateWind(wind);
    this.updateFleetStatus(this.fleetBritish, Faction.British, ships);
    this.updateFleetStatus(this.fleetFranco, Faction.FrancoSpanish, ships);

    if (gameOver) {
      this.banner.textContent =
        winner === Faction.Neutral ? "STALEMATE" : `${displayName(winner)} Fleet Victorious!`;
      this.banner.style.color = winner === Faction.Neutral ? "#ffffff" : accentCss(winner);
      document.body.classList.add("show-banner");
    } else {
      this.banner.textContent = "";
      document.body.classList.remove("show-banner");
    }
  }

  private updateWind(wind: Wind): void {
    // The arrow points the way the wind blows (downwind). Screen-up == north
    // (heading 0); CSS rotation is clockwise, matching compass headings.
    const downwind = normalize360(wind.fromDegrees + 180);
    this.windArrow.style.transform = `rotate(${downwind}deg)`;
    this.windLabel.innerHTML = `WIND<br>from ${roundToInt(wind.fromDegrees)}°`;
  }

  private updateFleetStatus(node: HTMLElement, faction: Faction, ships: ReadonlyArray<Ship>): void {
    let afloat = 0;
    let totalHull = 0;
    for (const s of ships) {
      if (s.isAlive && s.faction === faction) {
        afloat++;
        totalHull += s.hullFraction;
      }
    }
    const avgHull = afloat > 0 ? (totalHull / afloat) * 100 : 0;
    node.innerHTML = `<b>${displayName(faction)}</b><br>Ships: ${afloat} &nbsp; Avg hull: ${roundToInt(avgHull)}%`;
  }
}
