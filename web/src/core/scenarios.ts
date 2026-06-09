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
// is no formation/rows abstraction. `SCENARIOS` ships a couple of built-in
// battles ("Open Water" sandbox and the premade "Trafalgar"); further battles are
// authored at runtime (persisted via `scenarioStore`). The three ship classes
// are FirstRate (100+ gun three-decker
// flagship), ThirdRate (74-gun ship of the line), and Frigate (small, fast),
// capped at ≤12 per side.

import * as Config from "./config";
import { ShipClass, shipStats } from "../ships/shipClass";
import { headingToVector } from "./nav";
import type { Vec2 } from "./vec";

const W = Config.ArenaHalfX;

/**
 * One placed ship: where it sits, which way its bow points, and its class
 * (which sets its size/stats). This is the authored unit of placement — each
 * ship is positioned individually rather than generated from a formation.
 */
export interface ShipPlacement {
  pos: Vec2;
  headingDeg: number;
  shipClass: ShipClass;
  /** Optional author-given vessel name (e.g. "HMS Victory"); blank/absent = unnamed. */
  name?: string;
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
// the game's old hardcoded default match: a flagship-led LINE AHEAD column per
// side (ships bow-to-stern in single file), mixed classes, and a wind randomised
// at each start. Custom scenarios from the editor are layered on top by
// `scenarioStore`.
// ---------------------------------------------------------------------------

/** Half the on-water hull length (world units) of a placed ship's class. */
function shipHalfLength(c: ShipClass): number {
  return shipStats(c).length / 2;
}

/** Builds one side's LINE AHEAD column: ships in single file at a fixed `x`,
 *  spread along the Z (short) axis and all sharing `headingDeg` so every bow
 *  points along the line at the stern of the ship ahead — a true bow-to-stern
 *  battle line. `headingDeg` must be 0 (sailing north, bow +Z) or 180 (sailing
 *  south, bow −Z); index 0 is the flagship leading the van. Neighbour spacing is
 *  the two hulls' half-lengths plus `Config.ColumnGap`, so hulls never overlap
 *  bow-to-stern whatever the class mix, and the file is centred on z = 0.
 *
 *  This 12-ship column spans ≈ 766 world units centre-to-centre (≈ 817 tip-to-
 *  tip), comfortably inside the ±506 short half-extent (±~94 units of margin). */
function column(x: number, headingDeg: number, classes: ShipClass[]): ShipPlacement[] {
  const n = classes.length;
  // Cumulative centre offset of each ship BEHIND the van (index 0), summing each
  // adjacent pair's half-lengths plus the fixed inter-hull gap.
  const behind: number[] = new Array(n);
  behind[0] = 0;
  for (let i = 1; i < n; i++) {
    behind[i] =
      behind[i - 1] + shipHalfLength(classes[i - 1]) + Config.ColumnGap + shipHalfLength(classes[i]);
  }
  const mid = behind[n - 1] / 2; // centre the file on z = 0
  // +1 when the bow points north (heading 0), −1 when south (heading 180): the
  // van sits at the front in the travel direction, the rear ships trail behind.
  const bowZ = headingToVector(headingDeg).z;
  return classes.map((shipClass, i) => ({
    pos: { x, z: (mid - behind[i]) * bowZ },
    headingDeg,
    shipClass,
  }));
}

// 12 ships per side (24 total): a mixed line of 3 First Rates (flagships in the
// van, centre, and rear), 6 Third Rates (the ships of the line), and 3 Frigates
// (scouts) — the 1:2:1 class mix of the old 4-ship free-play default, scaled ×3.
const OPEN_WATER_LINE: ShipClass[] = [
  ShipClass.FirstRate, // flagship leads the van
  ShipClass.ThirdRate,
  ShipClass.ThirdRate,
  ShipClass.Frigate,
  ShipClass.ThirdRate,
  ShipClass.FirstRate, // centre flagship
  ShipClass.ThirdRate,
  ShipClass.ThirdRate,
  ShipClass.Frigate,
  ShipClass.ThirdRate,
  ShipClass.FirstRate, // rear flagship
  ShipClass.Frigate,
];

// The premade "Trafalgar" battle, baked in from the configuration authored in
// the in-app scenario editor. Ship placements/headings/classes are the exact
// literal values captured from the device's saved custom scenario, so this
// ships with the source instead of living only in per-device save data. Fixed
// wind from 335°; 12 ships per side.
const TRAFALGAR: Scenario = {
  id: "trafalgar",
  name: "Trafalgar",
  year: 1805,
  blurb:
    "Cape Trafalgar, 21 October 1805: Nelson's fleet bears down on the combined Franco-Spanish line in two converging columns. A decisive close-quarters melee under a steady wind.",
  windFromDegrees: 335,
  british: {
    label: "Royal Navy",
    ships: [
      { pos: { x: -715.1361812894402, z: 86.63997894696905 }, headingDeg: 90, shipClass: ShipClass.ThirdRate },
      { pos: { x: -150.38620191854875, z: 47.26023134625598 }, headingDeg: 90, shipClass: ShipClass.FirstRate },
      { pos: { x: -505.42179110308217, z: 80.41765198397547 }, headingDeg: 90, shipClass: ShipClass.ThirdRate },
      { pos: { x: -266.08737073467137, z: 59.19657971227525 }, headingDeg: 89, shipClass: ShipClass.FirstRate },
      { pos: { x: -165.74551098093696, z: -50.8830774410136 }, headingDeg: 90, shipClass: ShipClass.FirstRate },
      { pos: { x: -460.94805548771797, z: -38.334608645967705 }, headingDeg: 90, shipClass: ShipClass.ThirdRate },
      { pos: { x: -607.1990588125468, z: 86.4359388039602 }, headingDeg: 90, shipClass: ShipClass.ThirdRate },
      { pos: { x: -371.15517173837134, z: -50.47499715499589 }, headingDeg: 86, shipClass: ShipClass.ThirdRate },
      { pos: { x: -287.26971455148157, z: -53.63761937163349 }, headingDeg: 90, shipClass: ShipClass.FirstRate },
      { pos: { x: -386.68326441682984, z: 67.46903779736215 }, headingDeg: 93, shipClass: ShipClass.ThirdRate },
      { pos: { x: -623.9915781273312, z: -67.51319926349959 }, headingDeg: 90, shipClass: ShipClass.ThirdRate },
      { pos: { x: -544.1583782103269, z: -51.08711758402262 }, headingDeg: 83, shipClass: ShipClass.Frigate },
    ],
  },
  enemy: {
    label: "Enemy Fleet",
    ships: [
      { pos: { x: 573.8642946386822, z: 382.294146166831 }, headingDeg: 181, shipClass: ShipClass.ThirdRate },
      { pos: { x: 560.0240381209258, z: -210.95256963147722 }, headingDeg: 184, shipClass: ShipClass.ThirdRate },
      { pos: { x: 706.2750414457544, z: 349.0866128921364 }, headingDeg: 180, shipClass: ShipClass.ThirdRate },
      { pos: { x: 708.2160530305619, z: 119.59246204290264 }, headingDeg: 181, shipClass: ShipClass.FirstRate },
      { pos: { x: 736.4029169142852, z: -68.53254981128134 }, headingDeg: 169, shipClass: ShipClass.FirstRate },
      { pos: { x: 699.7768722270521, z: 253.74885607123883 }, headingDeg: 194, shipClass: ShipClass.FirstRate },
      { pos: { x: 672.7714936558198, z: 458.19707936613304 }, headingDeg: 162, shipClass: ShipClass.FirstRate },
      { pos: { x: 578.5902358886478, z: 193.65903395512476 }, headingDeg: 185, shipClass: ShipClass.ThirdRate },
      { pos: { x: 829.7402566011062, z: 436.4668041356877 }, headingDeg: 190, shipClass: ShipClass.FirstRate },
      { pos: { x: 545.846214371029, z: -419.78765600106203 }, headingDeg: 209, shipClass: ShipClass.ThirdRate },
      { pos: { x: 649.8169218702726, z: -348.0675457334421 }, headingDeg: 201, shipClass: ShipClass.ThirdRate },
      { pos: { x: 698.08903606635, z: -224.52123914156743 }, headingDeg: 181, shipClass: ShipClass.Frigate },
    ],
  },
};

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
      // Left column, in line ahead sailing NORTH (bows +Z); flagship leads the van.
      ships: column(-W * 0.55, 0, OPEN_WATER_LINE),
    },
    enemy: {
      label: "Enemy Fleet",
      // Right column, in line ahead sailing SOUTH (bows −Z) on the opposite course
      // — the two battle lines pass each other, broadsides bearing across the gap.
      ships: column(W * 0.55, 180, OPEN_WATER_LINE),
    },
  },
  TRAFALGAR,
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
