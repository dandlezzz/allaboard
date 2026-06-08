// The antique-chart MENU flow — a two-screen front end that runs on top of the
// live canvas before a match begins (and is reopenable any time via the corner
// "Battles" button):
//
//   Screen 1 — pick a SCENARIO (one of the historical battles), shown as a
//              gallery of "chart cards" in the hand-coloured nautical-chart style.
//   Screen 2 — pick the SIDE you command and the OPPONENT (an AI persona or a
//              second human), then begin.
//
// It also owns the dismissible "How to play" overlay (toggled by a persistent
// corner "?" button, openable/closable at any time). All of this is plain DOM so
// the browser build works without any Board hardware; the Game is driven only
// through the onBegin callback. Gameplay is untouched.

import { SCENARIOS, fleetSummary, type Scenario } from "../core/scenarios";
import { Faction, accentCss } from "../core/faction";
import { AIPersona } from "../ai/fleetAI";
import type { Opponent } from "./hud";

export interface MenuCallbacks {
  /** Start a match: chosen battle, the side the player commands, the opponent. */
  onBegin: (scenarioId: string, playerFaction: Faction, opponent: Opponent) => void;
}

interface OpponentOption {
  key: Opponent;
  name: string;
  desc: string;
}

const OPPONENTS: ReadonlyArray<OpponentOption> = [
  { key: AIPersona.Standard, name: "Standard", desc: "A balanced foe" },
  { key: AIPersona.Turtle, name: "Turtle", desc: "Holds station, guns blazing" },
  { key: AIPersona.Tactician, name: "Giga-brain", desc: "Manoeuvres to rake" },
  { key: "human", name: "2 Players", desc: "Human vs human" },
];

function el<T extends HTMLElement>(id: string): T {
  const e = document.getElementById(id);
  if (!e) throw new Error(`Missing menu element #${id}`);
  return e as T;
}

export class Menu {
  private readonly root = el("menu");
  private readonly menuClose = el<HTMLButtonElement>("menu-close");
  private readonly screenScenario = el("screen-scenario");
  private readonly screenSide = el("screen-side");
  private readonly gallery = el("scenario-gallery");
  private readonly sideBack = el<HTMLButtonElement>("side-back");
  private readonly battleName = el("side-battle-name");
  private readonly battleMeta = el("side-battle-meta");
  private readonly battleBlurb = el("side-battle-blurb");
  private readonly sideSelect = el("side-select");
  private readonly opponentSelect = el("opponent-select");
  private readonly confirmButton = el<HTMLButtonElement>("confirm-battle");

  private readonly howtoOverlay = el("howto-overlay");
  private readonly howtoToggle = el<HTMLButtonElement>("howto-toggle");
  private readonly howtoClose = el<HTMLButtonElement>("howto-close");
  private readonly battlesToggle = el<HTMLButtonElement>("battles-toggle");

  private selected: Scenario = SCENARIOS[0];
  private playerFaction: Faction = Faction.British;
  private opponent: Opponent = AIPersona.Standard;
  /** True once at least one match has started (so the menu may be dismissed). */
  private matchStarted = false;

  constructor(private readonly callbacks: MenuCallbacks) {
    this.buildGallery();
    this.buildOpponents();

    this.sideBack.addEventListener("click", () => this.showScreen("scenario"));
    this.confirmButton.addEventListener("click", () => this.begin());
    this.menuClose.addEventListener("click", () => this.close());

    this.battlesToggle.addEventListener("click", () => this.open());
    this.howtoToggle.addEventListener("click", () => this.toggleHowTo());
    this.howtoClose.addEventListener("click", () => this.setHowTo(false));
    this.howtoOverlay.addEventListener("click", (e) => {
      if (e.target === this.howtoOverlay) this.setHowTo(false); // click the scrim to close
    });
  }

  /** Opens the menu at Screen 1. Dismissible only after a match has begun. */
  open(): void {
    this.menuClose.hidden = !this.matchStarted;
    this.showScreen("scenario");
    this.root.hidden = false;
    document.body.classList.add("menu-open");
  }

  /** Closes the menu (resumes whatever match is behind it). */
  close(): void {
    this.root.hidden = true;
    document.body.classList.remove("menu-open");
  }

  // ---- Screen 1: scenario gallery ---------------------------------------

  private buildGallery(): void {
    this.gallery.replaceChildren();
    for (const s of SCENARIOS) {
      const card = document.createElement("button");
      card.type = "button";
      card.className = "chart-card";
      card.innerHTML = `
        <div class="chart-card-head">
          <span class="chart-card-name">${s.name}</span>
          <span class="chart-card-year">${s.year}</span>
        </div>
        ${shipGlyph()}
        <div class="chart-card-legend">
          <span class="legend-row"><i class="swatch" style="background:${accentCss(
            Faction.British,
          )}"></i>${s.british.label}</span>
          <span class="legend-row"><i class="swatch" style="background:${accentCss(
            Faction.FrancoSpanish,
          )}"></i>${s.enemy.label}</span>
        </div>
        <p class="chart-card-blurb">${s.blurb}</p>`;
      card.addEventListener("click", () => this.pickScenario(s));
      this.gallery.appendChild(card);
    }
  }

  private pickScenario(s: Scenario): void {
    this.selected = s;
    this.playerFaction = Faction.British;
    this.battleName.textContent = s.name;
    this.battleMeta.textContent = `${s.year}`;
    this.battleBlurb.textContent = s.blurb;
    this.buildSideCards();
    this.refreshOpponentState();
    this.showScreen("side");
  }

  // ---- Screen 2: side + opponent ----------------------------------------

  private buildSideCards(): void {
    this.sideSelect.replaceChildren();
    const sides: { faction: Faction; label: string; ships: Scenario["british"]["formation"]["ships"] }[] = [
      {
        faction: Faction.British,
        label: this.selected.british.label,
        ships: this.selected.british.formation.ships,
      },
      {
        faction: Faction.FrancoSpanish,
        label: this.selected.enemy.label,
        ships: this.selected.enemy.formation.ships,
      },
    ];
    for (const side of sides) {
      const card = document.createElement("button");
      card.type = "button";
      card.className = "side-card";
      card.dataset.faction = String(side.faction);
      card.innerHTML = `
        <i class="swatch big" style="background:${accentCss(side.faction)}"></i>
        <span class="side-name">${side.label}</span>
        <span class="side-fleet">${fleetSummary(side.ships)}</span>`;
      card.addEventListener("click", () => {
        this.playerFaction = side.faction;
        this.refreshSideState();
      });
      this.sideSelect.appendChild(card);
    }
    this.refreshSideState();
  }

  private buildOpponents(): void {
    this.opponentSelect.replaceChildren();
    for (const opt of OPPONENTS) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "opponent-btn";
      btn.dataset.opp = String(opt.key);
      btn.innerHTML = `<span class="opp-name">${opt.name}</span><span class="opp-desc">${opt.desc}</span>`;
      btn.addEventListener("click", () => {
        this.opponent = opt.key;
        this.refreshOpponentState();
      });
      this.opponentSelect.appendChild(btn);
    }
  }

  private refreshSideState(): void {
    for (const child of Array.from(this.sideSelect.children)) {
      const faction = Number((child as HTMLElement).dataset.faction);
      child.classList.toggle("active", faction === this.playerFaction);
    }
  }

  private refreshOpponentState(): void {
    for (const child of Array.from(this.opponentSelect.children)) {
      const key = (child as HTMLElement).dataset.opp;
      child.classList.toggle("active", key === String(this.opponent));
    }
    // In 2-player there is no "your side vs AI"; both sides are human, so the
    // side selection is informational only — dim it to make that clear.
    this.sideSelect.classList.toggle("muted", this.opponent === "human");
  }

  private begin(): void {
    this.matchStarted = true;
    this.callbacks.onBegin(this.selected.id, this.playerFaction, this.opponent);
    this.close();
  }

  private showScreen(which: "scenario" | "side"): void {
    this.screenScenario.hidden = which !== "scenario";
    this.screenSide.hidden = which !== "side";
  }

  // ---- How to play overlay ----------------------------------------------

  private toggleHowTo(): void {
    this.setHowTo(this.howtoOverlay.hidden);
  }

  private setHowTo(open: boolean): void {
    this.howtoOverlay.hidden = !open;
  }
}

/** A tiny engraved-style ship glyph for the chart cards (inline, no assets). */
function shipGlyph(): string {
  return `
    <svg class="chart-card-glyph" viewBox="0 0 120 56" aria-hidden="true">
      <g fill="none" stroke="#5a4327" stroke-width="1.4" stroke-linecap="round">
        <path d="M18 44 Q60 54 102 44 L96 38 L24 38 Z" fill="#cdbb8a" />
        <line x1="40" y1="38" x2="40" y2="8" /><line x1="60" y1="38" x2="60" y2="4" />
        <line x1="80" y1="38" x2="80" y2="8" />
        <path d="M40 12 q14 6 0 12 z" fill="#efe2c4" /><path d="M40 12 q-14 6 0 12 z" fill="#efe2c4" />
        <path d="M60 8 q16 7 0 14 z" fill="#efe2c4" /><path d="M60 8 q-16 7 0 14 z" fill="#efe2c4" />
        <path d="M80 12 q14 6 0 12 z" fill="#efe2c4" /><path d="M80 12 q-14 6 0 12 z" fill="#efe2c4" />
      </g>
    </svg>`;
}
