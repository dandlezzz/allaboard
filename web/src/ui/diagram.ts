// Shared nautical-chart DIAGRAM rendering: the world↔SVG mapping plus the little
// per-scenario starting-position chart drawn on the menu's chart cards. The
// editor reuses the SAME mapping and tick geometry for its live preview so what
// you author lines up exactly with what the card (and the game) show.
//
// Mapping convention (kept identical everywhere): world +X → SVG right, world
// +Z → SVG up (north up); the arena [−W, W] × [−H, H] fits the viewBox inside a
// uniform margin.

import * as Config from "../core/config";
import { formationPositions, type FleetFormation, type Scenario } from "../core/scenarios";
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

/** One ship hull drawn as a short tick along its bow heading, in SVG units. */
export interface Tick {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
}

/** Maps a formation to oriented hull ticks via the shared placement math. */
export function formationTicks(formation: FleetFormation, map: WorldMap): Tick[] {
  return formationPositions(formation).map((p) => {
    const half = shipStats(p.shipClass).length * 0.5;
    const dir = headingToVector(p.headingDeg);
    return {
      x1: map.toSvgX(p.pos.x - dir.x * half),
      y1: map.toSvgY(p.pos.z - dir.z * half),
      x2: map.toSvgX(p.pos.x + dir.x * half),
      y2: map.toSvgY(p.pos.z + dir.z * half),
    };
  });
}

/**
 * The static mini starting-position chart for a menu card: both fleets' ticks
 * (British accent vs Franco-Spanish accent) plus any land, over a faint
 * parchment arena. Pure string output — no assets, consistent with the HUD.
 */
export function scenarioDiagram(scenario: Scenario): string {
  const map = makeWorldMap(DIAGRAM_VB.w, DIAGRAM_VB.h, DIAGRAM_MARGIN);

  const ticks = (formation: FleetFormation, color: string) =>
    formationTicks(formation, map)
      .map(
        (t) =>
          `<line x1="${t.x1.toFixed(1)}" y1="${t.y1.toFixed(1)}" x2="${t.x2.toFixed(1)}" y2="${t.y2.toFixed(1)}" stroke="${color}" />`,
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

  const british = ticks(scenario.british.formation, accentCss(Faction.British));
  const enemy = ticks(scenario.enemy.formation, accentCss(Faction.FrancoSpanish));
  return `
    <svg class="chart-card-diagram" viewBox="0 0 ${DIAGRAM_VB.w} ${DIAGRAM_VB.h}" aria-hidden="true">
      <rect x="1.5" y="1.5" width="${DIAGRAM_VB.w - 3}" height="${DIAGRAM_VB.h - 3}" rx="2"
            fill="#e7d4ac" stroke="#8a7546" stroke-width="0.8" />
      <g>${land}</g>
      <g stroke-width="2" stroke-linecap="round">
        ${british}
        ${enemy}
      </g>
    </svg>`;
}
