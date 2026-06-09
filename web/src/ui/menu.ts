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

import { fleetSummary, type Scenario } from "../core/scenarios";
import {
  listScenarios,
  isCustomScenario,
  deleteCustomScenario,
  blankScenario,
  duplicateScenario,
  subscribeScenarios,
} from "../core/scenarioStore";
import { Faction, accentCss } from "../core/faction";
import { scenarioDiagram } from "./diagram";
import { Editor } from "./editor";
import { AIPersona } from "../ai/fleetAI";
import type { Opponent } from "./hud";

/** Escapes user-authored text before it goes into card `innerHTML`. */
function escapeHtml(s: string): string {
  return s.replace(/[&<>"']/g, (c) =>
    c === "&" ? "&amp;" : c === "<" ? "&lt;" : c === ">" ? "&gt;" : c === '"' ? "&quot;" : "&#39;",
  );
}

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

  // The single How-to-Play card grid (#howto). Its permanent home is the
  // "choose your command" screen (#side-howto-slot); the corner "?" button
  // borrows it into the overlay (#howto-overlay-slot) and returns it on close,
  // so both places share one copy of the heavy illustrated markup.
  private readonly howto = el("howto");
  private readonly sideHowtoSlot = el("side-howto-slot");
  private readonly howtoOverlaySlot = el("howto-overlay-slot");

  private selected: Scenario | null = null;
  private playerFaction: Faction = Faction.British;
  private opponent: Opponent = AIPersona.Standard;
  /** True once at least one match has started (so the menu may be dismissed). */
  private matchStarted = false;

  /** The in-app scenario editor (its own overlay above the menu). */
  private readonly editor = new Editor({
    onSaved: () => this.buildGallery(),
  });

  constructor(private readonly callbacks: MenuCallbacks) {
    // Dock the How-to-Play cards on the command screen by default; the "?"
    // overlay borrows them on demand.
    this.sideHowtoSlot.appendChild(this.howto);

    this.buildGallery();
    this.buildOpponents();

    // Rebuild the gallery when the custom-scenario list changes out from under
    // us — notably when the async Board.save hydration lands on device.
    subscribeScenarios(() => this.buildGallery());

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
    for (const s of listScenarios()) {
      this.gallery.appendChild(this.buildCard(s));
    }

    // Trailing "Create Battle" card → opens the editor on a blank scenario.
    const create = document.createElement("button");
    create.type = "button";
    create.className = "chart-card create-card";
    create.innerHTML = `<span class="create-plus" aria-hidden="true">＋</span><span class="create-label">Create Battle</span>`;
    create.addEventListener("click", () => this.editor.open(blankScenario(), { isNew: true }));
    this.gallery.appendChild(create);
  }

  /** One scenario chart card, with duplicate/edit/delete affordances. */
  private buildCard(s: Scenario): HTMLElement {
    const card = document.createElement("div");
    card.className = "chart-card";
    card.tabIndex = 0;
    card.setAttribute("role", "button");
    const custom = isCustomScenario(s.id);
    card.innerHTML = `
      <div class="chart-card-head">
        <span class="chart-card-name">${escapeHtml(s.name)}</span>
        <span class="chart-card-year">${s.year}</span>
      </div>
      ${scenarioDiagram(s)}
      <div class="chart-card-legend">
        <span class="legend-row"><i class="swatch" style="background:${accentCss(
          Faction.British,
        )}"></i>${escapeHtml(s.british.label)}</span>
        <span class="legend-row"><i class="swatch" style="background:${accentCss(
          Faction.FrancoSpanish,
        )}"></i>${escapeHtml(s.enemy.label)}</span>
      </div>
      <p class="chart-card-blurb">${escapeHtml(s.blurb)}</p>
      <div class="chart-card-actions">
        ${custom ? `<span class="chart-card-tag">Custom</span>` : ""}
        <button type="button" class="card-action" data-act="duplicate">Duplicate</button>
        ${custom ? `<button type="button" class="card-action" data-act="edit">Edit</button>` : ""}
        ${custom ? `<button type="button" class="card-action danger" data-act="delete">Delete</button>` : ""}
      </div>`;

    const pick = (): void => this.pickScenario(s);
    card.addEventListener("click", pick);
    card.addEventListener("keydown", (e) => {
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        pick();
      }
    });

    for (const btn of Array.from(card.querySelectorAll<HTMLButtonElement>(".card-action"))) {
      btn.addEventListener("click", (e) => {
        e.stopPropagation();
        const act = btn.dataset.act;
        if (act === "duplicate") {
          this.editor.open(duplicateScenario(s), { isNew: true });
        } else if (act === "edit") {
          this.editor.open(JSON.parse(JSON.stringify(s)) as Scenario, { isNew: false });
        } else if (act === "delete") {
          // Two-click confirm (no native dialog, which the Board WebView may block).
          if (btn.dataset.confirm === "1") {
            deleteCustomScenario(s.id);
            if (this.selected?.id === s.id) this.selected = null;
            this.buildGallery();
          } else {
            btn.dataset.confirm = "1";
            btn.textContent = "Confirm?";
            btn.classList.add("armed");
          }
        }
      });
    }
    return card;
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
    const sel = this.selected;
    if (!sel) return;
    const sides: { faction: Faction; label: string; ships: Scenario["british"]["ships"] }[] = [
      {
        faction: Faction.British,
        label: sel.british.label,
        ships: sel.british.ships,
      },
      {
        faction: Faction.FrancoSpanish,
        label: sel.enemy.label,
        ships: sel.enemy.ships,
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
    if (!this.selected) return; // nothing selected (empty gallery) → no-op
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
    // Move the shared #howto grid into whichever home is active so the modal
    // and the command screen never need two copies of the markup.
    if (open) {
      this.howtoOverlaySlot.appendChild(this.howto);
    } else {
      this.sideHowtoSlot.appendChild(this.howto);
    }
    this.howtoOverlay.hidden = !open;
  }
}

