// Battle SCENARIOS — the data that drives a match's fleets, ship placement,
// fixed wind, side labels, and optional cosmetic coastline.
//
// The game keeps its two-faction model unchanged (Faction.British is always the
// Royal Navy, Faction.FrancoSpanish is whatever the enemy of the day is). A
// scenario only supplies DISPLAY labels per side plus an EXPLICIT list of placed
// ships, so we never have to touch the faction/combat/AI systems to add a battle.
// Gameplay (movement, wind tick, gunnery, baton command, AI) is identical across
// every scenario; scenarios just decide WHERE each ship starts, its TYPE and
// facing, the WIND, and any (purely cosmetic) land at the edge of the arena.
//
// Ships are placed individually ("chessboard"-style) in the in-app editor — there
// is no formation/rows abstraction. There are NO built-in battles: `SCENARIOS`
// ships empty and every battle is authored at runtime (persisted via
// `scenarioStore`). The three ship classes are FirstRate (100+ gun three-decker
// flagship), ThirdRate (74-gun ship of the line), and Frigate (small, fast),
// capped at ≤12 per side.

import { ShipClass } from "../ships/shipClass";
import type { Vec2 } from "./vec";

/**
 * One placed ship: where it sits, which way its bow points, and its class
 * (which sets its size/stats). This is the authored unit of placement — each
 * ship is positioned individually rather than generated from a formation.
 */
export interface ShipPlacement {
  pos: Vec2;
  headingDeg: number;
  shipClass: ShipClass;
}

/**
 * Per-side scenario data: a display label + the explicit list of placed ships
 * (index 0 = lead/flagship, used where a "first" ship matters). A side may have
 * 0 ships while editing; a playable battle wants at least one per side.
 */
export interface SideSpec {
  /** Shown in the menus, setup pads, fleet status, and win banner. */
  label: string;
  ships: ShipPlacement[];
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

/** Human-readable fleet composition, e.g. "1 First Rate · 6 Third Rates · 3 Frigates". */
export function fleetSummary(ships: ReadonlyArray<ShipPlacement>): string {
  let first = 0;
  let third = 0;
  let frig = 0;
  for (const s of ships) {
    if (s.shipClass === ShipClass.FirstRate) first++;
    else if (s.shipClass === ShipClass.ThirdRate) third++;
    else frig++;
  }
  const parts: string[] = [];
  const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? "" : "s"}`;
  if (first) parts.push(plural(first, "First Rate"));
  if (third) parts.push(plural(third, "Third Rate"));
  if (frig) parts.push(plural(frig, "Frigate"));
  return parts.join(" · ");
}
