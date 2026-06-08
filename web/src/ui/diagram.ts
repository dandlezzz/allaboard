// Shared nautical-chart DIAGRAM rendering: the world↔SVG mapping, the little
// ship-silhouette marker, and the per-scenario starting-position chart drawn on
// the menu's chart cards. The editor reuses the SAME mapping and marker for its
// live preview so what you author lines up exactly with what the card (and the
// game) show.
//
// Mapping convention (kept identical everywhere): world +X → SVG right, world
// +Z → SVG up (north up); the arena [−W, W] × [−H, H] fits the viewBox inside a
// uniform margin.

import * as Config from "../core/config";
import { type ShipPlacement, type Scenario } from "../core/scenarios";
import { shipStats } from "../ships/shipClass";
import { headingToVector } from "../core/nav";
import { accentCss, Faction } from "../core/faction";

/** Card-diagram viewBox (≈16:9, matching the arena) and inner margin. */
export const DIAGRAM_VB = { w: 120, h: 68 } as const;
export const DIAGRAM_MARGIN = 7;

/** A bidirectional world↔SVG mapping for a given viewBox + margin. */
export interface WorldMap {
  toSvgX(x: number): number;
  toSvgY(z: number): number;
  toWorldX(sx: number): number;
  toWorldZ(sy: number): number;
}

/** Builds a {@link WorldMap} that fits the full arena into `vbW × vbH`. */
export function makeWorldMap(vbW: number, vbH: number, margin: number): WorldMap {
  const W = Config.ArenaHalfX;
  const H = Config.ArenaHalfZ;
  const innerW = vbW - 2 * margin;
  const innerH = vbH - 2 * margin;
  return {
    toSvgX: (x) => margin + ((x + W) / (2 * W)) * innerW,
    toSvgY: (z) => margin + ((H - z) / (2 * H)) * innerH,
    toWorldX: (sx) => ((sx - margin) / innerW) * (2 * W) - W,
    toWorldZ: (sy) => H - ((sy - margin) / innerH) * (2 * H),
  };
}

// A top-down hull silhouette in NORMALISED ship coordinates: x = along the keel
// (+1 = bow tip, −1 = stern), y = abeam (±1 = the gunwale). A pointed bow and a
// slightly-tapered square stern, so it reads as a ship even at card size. Scaled
// per ship by half-length (L) and half-beam (B = L · BEAM_RATIO).
const HULL: ReadonlyArray<readonly [number, number]> = [
  [1.0, 0.0], // bow tip
  [0.42, 0.8], // bow shoulder (starboard)
  [-0.72, 0.8], // run aft
  [-0.98, 0.45], // stern quarter
  [-0.98, -0.45], // stern quarter (port)
  [-0.72, -0.8],
  [0.42, -0.8],
];
const BEAM_RATIO = 0.32;
/** Modest legibility boost so even a frigate reads as a hull at card scale. */
const MARKER_SCALE = 1.25;

/**
 * SVG polygon `points` for a ship-silhouette marker at `p`, oriented along its
 * heading (bow pointing the way it faces) and sized by class via half-length.
 * Uses the shared world↔SVG `map`, so cards and the editor preview match.
 */
export function shipMarkerPoints(p: ShipPlacement, map: WorldMap): string {
  const L = shipStats(p.shipClass).length * 0.5 * MARKER_SCALE; // half-length, world units
  const B = L * BEAM_RATIO; // half-beam
  const fwd = headingToVector(p.headingDeg); // +along (bow)
  const right = { x: fwd.z, z: -fwd.x }; // abeam to starboard (heading + 90°)
  return HULL.map(([af, lf]) => {
    const wx = p.pos.x + fwd.x * (af * L) + right.x * (lf * B);
    const wz = p.pos.z + fwd.z * (af * L) + right.z * (lf * B);
    return `${map.toSvgX(wx).toFixed(2)},${map.toSvgY(wz).toFixed(2)}`;
  }).join(" ");
}

/** Dark outline stroked around every hull marker for legibility on parchment. */
export const MARKER_STROKE = "#3a2c1a";

/**
 * The static mini starting-position chart for a menu card: both fleets' ships as
 * little accent-filled hull silhouettes (British vs Franco-Spanish) plus any
 * land, over a faint parchment arena. Pure string output — no assets.
 */
export function scenarioDiagram(scenario: Scenario): string {
  const map = makeWorldMap(DIAGRAM_VB.w, DIAGRAM_VB.h, DIAGRAM_MARGIN);

  const fleet = (ships: ReadonlyArray<ShipPlacement>, color: string) =>
    ships
      .map(
        (p) =>
          `<polygon points="${shipMarkerPoints(p, map)}" fill="${color}" stroke="${MARKER_STROKE}" stroke-width="0.4" stroke-linejoin="round" />`,
      )
      .join("");

  const land = (scenario.land ?? [])
    .map((shape) => {
      const pts = shape.polygon
        .map((p) => `${map.toSvgX(p.x).toFixed(1)},${map.toSvgY(p.z).toFixed(1)}`)
        .join(" ");
      const fill = shape.fill !== undefined ? `#${shape.fill.toString(16).padStart(6, "0")}` : "#cdba8a";
      return `<polygon points="${pts}" fill="${fill}" fill-opacity="0.7" />`;
    })
    .join("");

  const british = fleet(scenario.british.ships, accentCss(Faction.British));
  const enemy = fleet(scenario.enemy.ships, accentCss(Faction.FrancoSpanish));
  return `
    <svg class="chart-card-diagram" viewBox="0 0 ${DIAGRAM_VB.w} ${DIAGRAM_VB.h}" aria-hidden="true">
      <rect x="1.5" y="1.5" width="${DIAGRAM_VB.w - 3}" height="${DIAGRAM_VB.h - 3}" rx="2"
            fill="#e7d4ac" stroke="#8a7546" stroke-width="0.8" />
      <g>${land}</g>
      <g>${british}${enemy}</g>
    </svg>`;
}
