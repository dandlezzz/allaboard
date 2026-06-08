// Battle SCENARIOS — the data that drives a match's fleets, starting formations,
// fixed wind, side labels, and optional cosmetic coastline.
//
// The game keeps its two-faction model unchanged (Faction.British is always the
// Royal Navy, Faction.FrancoSpanish is whatever the enemy of the day is). A
// scenario only supplies DISPLAY labels per side plus the starting layout, so we
// never have to touch the faction/combat/AI systems to add a battle. Gameplay
// (movement, wind tick, gunnery, baton command, AI) is identical across every
// scenario; scenarios just decide WHERE the ships start, HOW MANY, WHICH wind,
// and what (purely cosmetic) land is painted at the edge of the arena.
//
// There are NO built-in battles: `SCENARIOS` ships empty and every battle is
// authored at runtime in the in-app editor (persisted via `scenarioStore`). The
// three ship classes are FirstRate (100+ gun three-decker flagship), ThirdRate
// (74-gun ship of the line), and Frigate (small, fast), capped at ≤12 per side.

import { ShipClass, shipStats } from "../ships/shipClass";
import * as Config from "./config";
import { headingToVector } from "./nav";
import { add, scale, type Vec2 } from "./vec";

// Ship-class shorthands, kept for authoring helpers / future seed data.
const F1 = ShipClass.FirstRate;
const R3 = ShipClass.ThirdRate;
const FR = ShipClass.Frigate;

/**
 * One side's starting line/columns. Ships are placed bow-to-stern from a REAR
 * `anchor` marching FORWARD along `headingDeg`; with `columns > 1` the line is
 * split round-robin into that many parallel columns spaced `columnGap` abeam
 * (so the flagship — index 0 — leads the first column). This single primitive
 * expresses a single line-ahead, a long battle line, or Nelson's two attack
 * columns alike.
 */
export interface FleetFormation {
  /** Composition, index 0 = lead/flagship. Length is the ship count (≤12). */
  ships: ShipClass[];
  /** Rear-of-formation anchor in world units. */
  anchor: Vec2;
  /** Bow direction & column axis, compass degrees (0 = +Z/north, 90 = +X/east). */
  headingDeg: number;
  /** Parallel columns to split the line into (default 1). */
  columns?: number;
  /** Abeam spacing between columns in world units (default derived). */
  columnGap?: number;
  /**
   * Optional total heading bend (degrees) spread evenly across each column so
   * the line traces a gentle arc instead of a dead-straight column: the rear
   * ship keeps `headingDeg`, each successive ship forward rotates a little more,
   * and the front ship ends `arcDeg` off the base heading. Default 0 (straight).
   * Positive bends to starboard, negative to port (relative to the march).
   */
  arcDeg?: number;
}

/**
 * One spawned ship's resolved placement: where it sits, which way its bow points
 * (per-ship, so a curved line fans along its arc), and its class (for sizing).
 */
export interface ShipPlacement {
  pos: Vec2;
  headingDeg: number;
  shipClass: ShipClass;
}

/** Per-side scenario data: a display label + the starting formation. */
export interface SideSpec {
  /** Shown in the menus, setup pads, fleet status, and win banner. */
  label: string;
  formation: FleetFormation;
}

/** A purely-cosmetic landmass polygon drawn at/just outside the arena edge. */
export interface LandShape {
  /** Closed polygon in world units (drawn filled, behind the ships). */
  polygon: Vec2[];
  /** Optional override fill colour (0xRRGGBB); defaults to a sandy coast. */
  fill?: number;
}

export interface Scenario {
  id: string;
  name: string;
  /** Year of the action, shown on the chart card. */
  year: number;
  /** 1–2 sentence historical/tactical blurb for the menu. */
  blurb: string;
  /** Fixed initial wind (degrees the wind blows FROM). Veers normally after. */
  windFromDegrees: number;
  /** Royal Navy side (Faction.British). */
  british: SideSpec;
  /** The opposing side (Faction.FrancoSpanish — French/Spanish/Danish/American). */
  enemy: SideSpec;
  /** Optional cosmetic coastline (Nile shoals, Copenhagen waterfront, etc.). */
  land?: LandShape[];
}

// ---------------------------------------------------------------------------
// No built-in battles — the gallery is populated entirely by user-authored
// scenarios from the editor (see `scenarioStore`).
// ---------------------------------------------------------------------------

export const SCENARIOS: ReadonlyArray<Scenario> = [];

/** Looks up a built-in scenario by id; `undefined` if none match (now always). */
export function getScenario(id: string): Scenario | undefined {
  return SCENARIOS.find((s) => s.id === id);
}

/**
 * Resolves a `FleetFormation` to concrete per-ship placements — the single
 * source of truth shared by the live spawner (`Game.spawnFleet`) and the menu's
 * mini starting-position diagram, so the chart card can never drift from where
 * the ships actually start.
 *
 * Ships are placed bow-to-stern from the REAR `anchor` marching FORWARD along
 * `headingDeg`; with `columns > 1` the list is split round-robin into that many
 * parallel columns spaced `columnGap` abeam (the flagship — index 0 — leads
 * column 0). Spacing within a column is cumulative from each ship's half-length
 * plus `Config.ColumnGap`. An optional `arcDeg` bends each column into a gentle
 * arc: the heading is rotated evenly across the gaps from the rear ship (base
 * heading) to the front, and the march follows the mean heading of each gap — so
 * with `arcDeg = 0` this reproduces the old straight cumulative spacing exactly.
 */
export function formationPositions(formation: FleetFormation): ShipPlacement[] {
  const cols = Math.max(1, formation.columns ?? 1);
  const columnGap = formation.columnGap ?? Config.ColumnGap + 8 * Config.ShipScale;
  const right = headingToVector(formation.headingDeg + 90); // abeam (to starboard)
  const arcDeg = formation.arcDeg ?? 0;

  // Distribute ships round-robin across the columns so the flagship (index 0)
  // and the next-heaviest ship head columns 0 and 1.
  const columnLists: ShipClass[][] = Array.from({ length: cols }, () => []);
  formation.ships.forEach((c, i) => columnLists[i % cols].push(c));

  const out: ShipPlacement[] = [];
  for (let ci = 0; ci < cols; ci++) {
    const list = columnLists[ci];
    const n = list.length;
    if (n === 0) continue;
    const lateral = (ci - (cols - 1) / 2) * columnGap;
    const colAnchor = add(formation.anchor, scale(right, lateral));

    const lengths = list.map((c) => shipStats(c).length);
    // Per-ship bow heading: rear keeps the base heading, each ship forward turns
    // by an equal share of `arcDeg` (zero share ⇒ every ship on the base heading).
    const stepDelta = n > 1 ? arcDeg / (n - 1) : 0;
    const headings = new Array<number>(n);
    headings[n - 1] = formation.headingDeg;
    for (let i = n - 2; i >= 0; i--) headings[i] = headings[i + 1] + stepDelta;

    // March bow-to-stern from the rear anchor forward, advancing each inter-ship
    // gap along the mean heading of the two ships it joins.
    const positions = new Array<Vec2>(n);
    positions[n - 1] = colAnchor;
    for (let i = n - 2; i >= 0; i--) {
      const gap = lengths[i + 1] * 0.5 + Config.ColumnGap + lengths[i] * 0.5;
      const segHeading = (headings[i] + headings[i + 1]) * 0.5;
      positions[i] = add(positions[i + 1], scale(headingToVector(segHeading), gap));
    }

    for (let i = 0; i < n; i++) {
      out.push({ pos: positions[i], headingDeg: headings[i], shipClass: list[i] });
    }
  }
  return out;
}

/** Human-readable fleet composition, e.g. "1 First Rate · 6 Third Rates · 3 Frigates". */
export function fleetSummary(ships: ReadonlyArray<ShipClass>): string {
  let first = 0;
  let third = 0;
  let frig = 0;
  for (const c of ships) {
    if (c === ShipClass.FirstRate) first++;
    else if (c === ShipClass.ThirdRate) third++;
    else frig++;
  }
  const parts: string[] = [];
  const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? "" : "s"}`;
  if (first) parts.push(plural(first, "First Rate"));
  if (third) parts.push(plural(third, "Third Rate"));
  if (frig) parts.push(plural(frig, "Frigate"));
  return parts.join(" · ");
}
