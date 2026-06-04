// Heads-up display — a DOM port of Unity `UI/HudController.cs`. Drives the wind
// indicator (an arrow that rotates as the wind veers), per-side fleet status,
// a control hint, and the win banner. The dev-controls (P2 toggle / rematch)
// are wired here too.

import { Faction, accentCss, displayName } from "../core/faction";
import { normalize360 } from "../core/nav";
import { roundToInt } from "../core/mathf";
import { AIPersona } from "../ai/fleetAI";
import type { Ship } from "../ships/ship";
import type { Wind } from "../combat/wind";

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
  private readonly toggleButton = el<HTMLButtonElement>("toggle-ai-button");
  private readonly resetButton = el<HTMLButtonElement>("reset-button");

  // Persona buttons: each starts a fresh game vs that AI; the active one is
  // highlighted to show the currently-selected opponent.
  private readonly personaButtons: ReadonlyArray<{ persona: AIPersona; button: HTMLButtonElement }> = [
    { persona: AIPersona.Standard, button: el<HTMLButtonElement>("persona-standard") },
    { persona: AIPersona.Turtle, button: el<HTMLButtonElement>("persona-turtle") },
    { persona: AIPersona.Tactician, button: el<HTMLButtonElement>("persona-giga") },
  ];

  constructor(
    onToggleSecondPlayer: () => void,
    onReset: () => void,
    onSelectPersona: (persona: AIPersona) => void,
  ) {
    this.fleetBritish.style.color = accentCss(Faction.British);
    this.fleetFranco.style.color = accentCss(Faction.FrancoSpanish);
    this.toggleButton.addEventListener("click", onToggleSecondPlayer);
    this.resetButton.addEventListener("click", onReset);
    for (const { persona, button } of this.personaButtons) {
      button.addEventListener("click", () => onSelectPersona(persona));
    }
  }

  /** Highlights the button for the currently-selected opponent persona. */
  setActivePersona(persona: AIPersona): void {
    for (const entry of this.personaButtons) {
      entry.button.classList.toggle("active", entry.persona === persona);
    }
  }

  setSecondPlayerMode(secondPlayerIsHuman: boolean): void {
    this.toggleButton.textContent = secondPlayerIsHuman
      ? "Franco-Spanish: Human"
      : "Franco-Spanish: AI";
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
