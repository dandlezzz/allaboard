// Per-player firing-range overlay toggles — a pair of code-built HUD buttons,
// one per fleet, positioned in the bottom corner nearest that side's start
// (British / left fleet → bottom-left; Franco-Spanish / right fleet →
// bottom-right). Each toggle controls ONLY its own side's range fans, so the
// two commanders can plan independently. Plain DOM so the browser preview works
// with pointer/mouse fallback (no Board SDK dependency).

import { Faction, accentCss } from "../core/faction";

function el<T extends HTMLElement>(id: string): T {
  const e = document.getElementById(id);
  if (!e) throw new Error(`Missing range-toggle element #${id}`);
  return e as T;
}

export class RangeToggle {
  private readonly british = el<HTMLButtonElement>("range-toggle-british");
  private readonly franco = el<HTMLButtonElement>("range-toggle-franco");
  private readonly state = new Map<Faction, boolean>();

  /** `onToggle` is called whenever a side flips its range overlay on/off. */
  constructor(private readonly onToggle: (faction: Faction, enabled: boolean) => void) {
    this.wire(this.british, Faction.British);
    this.wire(this.franco, Faction.FrancoSpanish);
  }

  private wire(button: HTMLButtonElement, faction: Faction): void {
    this.state.set(faction, false);
    button.style.setProperty("--accent", accentCss(faction));
    button.setAttribute("aria-pressed", "false");
    button.addEventListener("click", () => {
      const next = !this.state.get(faction);
      this.state.set(faction, next);
      button.classList.toggle("active", next);
      button.setAttribute("aria-pressed", String(next));
      this.onToggle(faction, next);
    });
  }
}
