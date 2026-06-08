// Historical battle SCENARIOS — the data that drives a match's fleets, starting
// formations, fixed wind, side labels, and optional cosmetic coastline.
//
// The game keeps its two-faction model unchanged (Faction.British is always the
// Royal Navy, Faction.FrancoSpanish is whatever the enemy of the day is). A
// scenario only supplies DISPLAY labels per side plus the starting layout, so we
// never have to touch the faction/combat/AI systems to add a battle. Gameplay
// (movement, wind tick, gunnery, baton command, AI) is identical across every
// scenario; scenarios just decide WHERE the ships start, HOW MANY, WHICH wind,
// and what (purely cosmetic) land is painted at the edge of the arena.
//
// Fleet counts are capped at ≤12 per side and scaled DOWN from the historical
// order of battle while preserving each action's tactical flavour. The original
// strengths and the scaling rationale are documented inline per scenario, drawn
// from the standard accounts (Wikipedia / threedecks / NPS / USNI verified via
// research). Ship TYPES are limited to the three existing classes:
//   FirstRate  (100+ gun three-decker flagship),
//   ThirdRate  (74-gun ship of the line — the workhorse),
//   Frigate    (small, fast — also used to stand in for brigs/sloops/blockships).

import * as Config from "./config";
import { ShipClass, shipStats } from "../ships/shipClass";
import { headingToVector } from "./nav";
import { add, scale, type Vec2 } from "./vec";

const F1 = ShipClass.FirstRate;
const R3 = ShipClass.ThirdRate;
const FR = ShipClass.Frigate;

// Arena half-extents (long axis X, short axis Z). Anchors below are expressed as
// fractions of these so the layouts scale with the arena.
const W = Config.ArenaHalfX;
const H = Config.ArenaHalfZ;
// Coastline polygons reach beyond the visible field so they meet the screen edge.
const CW = W * 1.35;

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
   * and the front ship ends `arcDeg` off the base heading. Default 0 (straight),
   * so every existing scenario is unaffected. Used for the Combined Fleet's
   * crescent line of battle at Trafalgar. Positive bends to starboard, negative
   * to port (relative to the marching direction).
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
// The five battles.
// ---------------------------------------------------------------------------

export const SCENARIOS: ReadonlyArray<Scenario> = [
  // === TRAFALGAR (1805) =====================================================
  // History: 27 British ships of the line (3 first, 4 second, 20 third) vs 33
  // Franco-Spanish (4 first, 29 third). Nelson attacked in TWO columns sailing
  // down before a WNW wind from the WEST, straight at the long allied line to
  // break it. Scaling: 27→10 British, 33→12 allied. Laid out to the historical
  // noon-21-Oct chart (north up, +X = east): the British bear down from the WEST
  // in their two famous attack columns — Nelson's weather column (Victory) to
  // the north, Collingwood's lee column (Royal Sovereign) to the south — steering
  // due east at mid-height. The Combined Fleet lies in one long near-north–south
  // line down the EAST side, bent into the shallow crescent it actually formed,
  // bowing WEST (concave toward the oncoming British), spanning the field's full
  // height. Wind from the WNW so the British run down before it into the line.
  {
    id: "trafalgar",
    name: "Trafalgar",
    year: 1805,
    blurb:
      "Nelson hurls two columns at right angles into the long Franco-Spanish line to break it apart. Off Cape Trafalgar, the climactic fleet action of the age.",
    windFromDegrees: 300,
    british: {
      label: "Royal Navy",
      formation: {
        // 1 first-rate flagship + 6 seventy-fours + 3 frigates, split into the
        // two attack columns (round-robin keeps a heavy ship at each head). They
        // muster to the west and steer due east; columnGap separates the weather
        // (north) and lee (south) columns abeam of the easterly heading.
        ships: [F1, R3, R3, R3, R3, R3, R3, FR, FR, FR],
        anchor: { x: -W * 0.62, z: 0 },
        headingDeg: 90,
        columns: 2,
        columnGap: 280,
      },
    },
    enemy: {
      label: "Combined Fleet",
      formation: {
        // Two first-rates (Santísima Trinidad / a Spanish three-decker) amidships,
        // eight 74s, two frigates — one long line of battle down the east side,
        // running roughly south→north. A gentle 44° bow centred on due-north
        // (headingDeg = −arcDeg/2) curves the line into the historical crescent
        // that bulges EAST / is concave WEST, toward the attacking British.
        ships: [FR, R3, R3, R3, F1, R3, R3, F1, R3, R3, R3, FR],
        anchor: { x: W * 0.5, z: -H * 0.92 },
        headingDeg: 22,
        arcDeg: -44,
      },
    },
  },

  // === THE NILE / ABOUKIR BAY (1798) ========================================
  // History: 14 British ships of the line vs 13 French anchored line-ahead
  // (incl. the 120-gun L'Orient) along the shoals of Aboukir Bay, with 4
  // frigates. Nelson attacked at dusk, doubling the line by slipping ships
  // between the French and the shore. Scaling: 13→12 French (anchored line),
  // 14→10 British attacking from seaward. The Nile's were almost all 74s; we
  // keep the British all-74 (no first-rate) and give the French L'Orient a
  // first-rate amidships. Coastline + Aboukir island painted along the north.
  {
    id: "nile",
    name: "The Nile",
    year: 1798,
    blurb:
      "A French fleet lies anchored in line along the shoals of Aboukir Bay. Nelson attacks at dusk, doubling the line from both sides in a battle of annihilation.",
    windFromDegrees: 285,
    british: {
      label: "Royal Navy",
      formation: {
        // Eight 74s + two frigates bearing down in line from seaward (SW).
        ships: [R3, R3, R3, R3, R3, R3, R3, R3, FR, FR],
        anchor: { x: -W * 0.58, z: -H * 0.6 },
        headingDeg: 60,
      },
    },
    enemy: {
      label: "Marine Nationale",
      formation: {
        // L'Orient (first-rate) amidships, nine 74s, two frigates — anchored line.
        ships: [FR, R3, R3, R3, R3, F1, R3, R3, R3, R3, R3, FR],
        anchor: { x: -W * 0.52, z: H * 0.5 },
        headingDeg: 90,
      },
    },
    land: [
      // North shore of Aboukir Bay.
      {
        polygon: [
          { x: -CW, z: H * 0.86 },
          { x: -W * 0.45, z: H * 0.99 },
          { x: -W * 0.1, z: H * 0.88 },
          { x: W * 0.25, z: H * 1.02 },
          { x: W * 0.62, z: H * 0.9 },
          { x: CW, z: H * 0.96 },
          { x: CW, z: H * 1.6 },
          { x: -CW, z: H * 1.6 },
        ],
      },
      // Aboukir Island / shoal on which the head of the French line rested.
      {
        polygon: [
          { x: -W * 0.6, z: H * 0.8 },
          { x: -W * 0.5, z: H * 0.84 },
          { x: -W * 0.46, z: H * 0.9 },
          { x: -W * 0.56, z: H * 0.92 },
          { x: -W * 0.64, z: H * 0.86 },
        ],
      },
    ],
  },

  // === THE CHESAPEAKE / VIRGINIA CAPES (1781) ===============================
  // History: de Grasse's 24 French ships of the line (incl. 110-gun Ville de
  // Paris) vs Graves's 19 British. A classic, indecisive line-vs-line passing
  // action off the Virginia Capes — but strategically it sealed Cornwallis's
  // fate at Yorktown. Scaling: 24→12 French, 19→10 British, deployed as two
  // parallel battle lines that exchange broadsides abeam. Open water; wind set
  // ~north so both lines (heading east) sail on the beam.
  {
    id: "chesapeake",
    name: "The Chesapeake",
    year: 1781,
    blurb:
      "Two great battle lines pass and trade broadsides off the Virginia Capes. The French hold the sea — and seal Cornwallis's fate at Yorktown.",
    windFromDegrees: 350,
    british: {
      label: "Royal Navy",
      formation: {
        // Flagship London (first-rate stand-in) + seven 74s + two frigates.
        ships: [F1, R3, R3, R3, R3, R3, R3, R3, FR, FR],
        anchor: { x: -W * 0.46, z: -H * 0.26 },
        headingDeg: 90,
      },
    },
    enemy: {
      label: "Marine Royale",
      formation: {
        // Ville de Paris (first-rate) leading, nine 74s, two frigates.
        ships: [F1, R3, R3, R3, R3, R3, R3, R3, R3, R3, FR, FR],
        anchor: { x: -W * 0.54, z: H * 0.32 },
        headingDeg: 90,
      },
    },
  },

  // === COPENHAGEN (1801) ====================================================
  // History: Nelson took 12 ships of the line (his division) against a Danish
  // line of ~18 moored blockships, hulks and floating batteries covering the
  // city, backed by the Tre Kroner fort and shore batteries. He sailed up the
  // King's Channel and anchored alongside, fighting it out at close range.
  // Scaling: Danish 18→12 moored line (mostly 74-class hulks + a couple of
  // frigate-class floating batteries, no first-rates — they were blockships);
  // British 12→10 (all 74s, as Nelson's division was). Cosmetic city waterfront
  // + Tre Kroner fort island along the north. Wind set southerly (it veered fair
  // for Nelson on the morning of the attack).
  {
    id: "copenhagen",
    name: "Copenhagen",
    year: 1801,
    blurb:
      "Nelson sails up the King's Channel and anchors yardarm-to-yardarm against the moored Danish line of defence, beneath the guns of the Tre Kroner fort.",
    windFromDegrees: 175,
    british: {
      label: "Royal Navy",
      formation: {
        // Nelson's division: eight 74s + two frigates, sailing up the channel.
        ships: [R3, R3, R3, R3, R3, R3, R3, R3, FR, FR],
        anchor: { x: -W * 0.46, z: H * 0.1 },
        headingDeg: 90,
      },
    },
    enemy: {
      label: "Dano-Norwegian",
      formation: {
        // Moored blockships/hulks (ten 74-class) + two floating batteries (frigate).
        ships: [FR, R3, R3, R3, R3, R3, R3, R3, R3, R3, R3, FR],
        anchor: { x: -W * 0.5, z: H * 0.5 },
        headingDeg: 90,
      },
    },
    land: [
      // Copenhagen waterfront (north).
      {
        polygon: [
          { x: -CW, z: H * 0.88 },
          { x: -W * 0.3, z: H * 0.93 },
          { x: W * 0.1, z: H * 0.9 },
          { x: W * 0.5, z: H * 0.94 },
          { x: CW, z: H * 0.92 },
          { x: CW, z: H * 1.6 },
          { x: -CW, z: H * 1.6 },
        ],
      },
      // Tre Kroner fort, on its little island off the harbour mouth.
      {
        polygon: [
          { x: W * 0.55, z: H * 0.76 },
          { x: W * 0.66, z: H * 0.78 },
          { x: W * 0.69, z: H * 0.84 },
          { x: W * 0.6, z: H * 0.86 },
          { x: W * 0.52, z: H * 0.82 },
        ],
        fill: 0x8d8377, // stony grey fort rather than sandy coast
      },
    ],
  },

  // === LAKE ERIE (1813) =====================================================
  // History: Perry's 9-vessel American squadron (brigs Lawrence & Niagara, plus
  // schooners/sloops) vs Barclay's 6-vessel British squadron (ships Detroit &
  // Queen Charlotte, plus smaller craft) near Put-in-Bay. Small fresh-water
  // warships, not ships of the line. Scaling keeps the counts (9 vs 8 — bumping
  // the British up slightly for playability) but represents the two big brigs /
  // ships per side as ThirdRate and everything smaller (brigs, schooners,
  // sloops) as Frigate, since no new ship classes are added. Cosmetic lake
  // islands at the south. Player commanding the Royal Navy fights the historical
  // loser's hand — a fun underdog twist; the U.S. squadron is the larger.
  {
    id: "lake-erie",
    name: "Lake Erie",
    year: 1813,
    blurb:
      "Perry's scratch-built squadron of brigs and schooners meets the British line off Put-in-Bay for control of the lake. \"We have met the enemy and they are ours.\"",
    windFromDegrees: 155,
    british: {
      label: "Royal Navy",
      formation: {
        // Detroit & Queen Charlotte (ThirdRate) + six smaller craft (Frigate).
        ships: [R3, FR, R3, FR, FR, FR, FR, FR],
        anchor: { x: -W * 0.36, z: H * 0.3 },
        headingDeg: 90,
      },
    },
    enemy: {
      label: "U.S. Navy",
      formation: {
        // Brigs Lawrence & Niagara (ThirdRate) + seven smaller craft (Frigate).
        ships: [R3, FR, R3, FR, FR, FR, FR, FR, FR],
        anchor: { x: -W * 0.4, z: -H * 0.32 },
        headingDeg: 90,
      },
    },
    land: [
      // South lake shore.
      {
        polygon: [
          { x: -CW, z: -H * 0.9 },
          { x: -W * 0.55, z: -H * 0.94 },
          { x: -W * 0.32, z: -H * 0.88 },
          { x: -W * 0.36, z: -H * 1.6 },
          { x: -CW, z: -H * 1.6 },
        ],
        fill: 0x8fa05c, // wooded lakeshore green
      },
      // A wooded island (Put-in-Bay / Rattlesnake) in the south of the field.
      {
        polygon: [
          { x: -W * 0.12, z: -H * 0.82 },
          { x: W * 0.06, z: -H * 0.78 },
          { x: W * 0.18, z: -H * 0.84 },
          { x: W * 0.1, z: -H * 0.94 },
          { x: -W * 0.1, z: -H * 0.92 },
        ],
        fill: 0x8fa05c,
      },
    ],
  },
];

/** Looks up a scenario by id (falls back to the first scenario). */
export function getScenario(id: string): Scenario {
  return SCENARIOS.find((s) => s.id === id) ?? SCENARIOS[0];
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
