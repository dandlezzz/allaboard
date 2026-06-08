// Heads-up display — a DOM port of Unity `UI/HudController.cs`. Drives the wind
// indicator, per-side fleet status, the win banner, and the in-canvas placement
// prompt shown during the Setup phase. The opponent / scenario selection now
// lives in the antique-chart MENU (see ui/menu.ts); the HUD only renders the
// live battle chrome. The in-game Rematch button is wired here too.

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

  // Per-scenario display labels for the two factions (e.g. "Royal Navy" vs
  // "Combined Fleet" / "U.S. Navy"). Defaults to the generic faction names.
  private britishLabel = displayName(Faction.British);
  private francoLabel = displayName(Faction.FrancoSpanish);

  constructor(onReset: () => void) {
    this.fleetBritish.style.color = accentCss(Faction.British);
    this.fleetFranco.style.color = accentCss(Faction.FrancoSpanish);
    this.resetButton.addEventListener("click", onReset);
  }

  /** Sets the two sides' scenario display labels (fleet panels + win banner). */
  setSideLabels(britishLabel: string, francoLabel: string): void {
    this.britishLabel = britishLabel;
    this.francoLabel = francoLabel;
  }

  private labelFor(faction: Faction): string {
    if (faction === Faction.British) return this.britishLabel;
    if (faction === Faction.FrancoSpanish) return this.francoLabel;
    return displayName(faction);
  }

  /**
   * Shows/hides the in-canvas placement prompt (Setup phase) and sets its text.
   * Toggling `body.phase-setup` lets the stylesheet reveal the placement status
   * and hide in-battle chrome (wind/fleet panels) while players place their
   * command pieces.
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
        winner === Faction.Neutral ? "STALEMATE" : `${this.labelFor(winner)} Victorious!`;
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
    node.innerHTML = `<b>${this.labelFor(faction)}</b><br>Ships: ${afloat} &nbsp; Avg hull: ${roundToInt(avgHull)}%`;
  }
}
