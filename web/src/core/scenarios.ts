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

import * as Config from "./config";
import { ShipClass } from "../ships/shipClass";
import type { Vec2 } from "./vec";

const W = Config.ArenaHalfX;
const H = Config.ArenaHalfZ;

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
  /** Fixed initial wind (degrees the wind blows FROM). Veers normally after.
   *  Ignored when `randomWind` is set. */
  windFromDegrees: number;
  /** When true, the initial wind is RANDOMISED at each match start (mirrors the
   *  old free-play default) instead of using `windFromDegrees`. See the wind init
   *  in `Game.restart`. Plain boolean → serialises/persists like any other field. */
  randomWind?: boolean;
  /** Royal Navy side (Faction.British). */
  british: SideSpec;
  /** The opposing side (Faction.FrancoSpanish — French/Spanish/Danish/American). */
  enemy: SideSpec;
  /** Optional cosmetic coastline (Nile shoals, Copenhagen waterfront, etc.). */
  land?: LandShape[];
}

// ---------------------------------------------------------------------------
// Built-in battles. "Open Water" is the free-play / sandbox setup that mirrors
// the game's old hardcoded default match: a flagship-led line per side, mixed
// classes, and a wind randomised at each start. Custom scenarios from the editor
// are layered on top of this list by `scenarioStore`.
// ---------------------------------------------------------------------------

/** Builds one side's line: ships spread evenly along Z near `x`, all sharing
 *  `headingDeg` (bows pointed at the opposing fleet). Index 0 (the flagship)
 *  leads. Mirrors the class mix of the old free-play default, scaled to 4/side. */
function line(x: number, headingDeg: number, classes: ShipClass[]): ShipPlacement[] {
  const n = classes.length;
  return classes.map((shipClass, i) => {
    // Even spread across the short axis, centred on z = 0 (e.g. 4 ships →
    // −0.45H, −0.15H, +0.15H, +0.45H), so each side reads as a tidy battle line.
    const t = n === 1 ? 0 : (i / (n - 1)) * 2 - 1; // −1 … +1
    return {
      pos: { x, z: t * H * 0.45 },
      headingDeg,
      shipClass,
    };
  });
}

const OPEN_WATER_LINE: ShipClass[] = [
  ShipClass.FirstRate,
  ShipClass.ThirdRate,
  ShipClass.ThirdRate,
  ShipClass.Frigate,
];

export const SCENARIOS: ReadonlyArray<Scenario> = [
  {
    id: "open-water",
    name: "Open Water",
    year: 1805,
    blurb:
      "Free play on the open sea: two battle lines square off across the water with a fresh, unpredictable wind. Sandbox your tactics with no coast in sight.",
    // Fallback only — `randomWind` makes the real wind a fresh random bearing at
    // each match start.
    windFromDegrees: 135,
    randomWind: true,
    british: {
      label: "Royal Navy",
      // Left side, bows east (90°) toward the enemy.
      ships: line(-W * 0.55, 90, OPEN_WATER_LINE),
    },
    enemy: {
      label: "Enemy Fleet",
      // Right side, bows west (270°) toward the Royal Navy.
      ships: line(W * 0.55, 270, OPEN_WATER_LINE),
    },
  },
];

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
