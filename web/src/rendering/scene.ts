// Procedurally builds the static scene furniture — a port of Unity
// `Core/SceneBuilder.cs`. A clean, flat solid light-blue field fills the view,
// with optional COSMETIC coastline painted at/just outside the arena edge for
// scenarios that benefit from it (the Nile shoals, the Copenhagen waterfront,
// the Lake Erie islands). Land is purely decorative: it is drawn BENEATH the
// fleet and never participates in the simulation (no collision, no blocking of
// movement or fire). Ships already turn at the arena boundary, so the coast sits
// in the band beyond where they sail.

import { Container, Graphics } from "pixi.js";
import * as Config from "../core/config";
import type { LandShape } from "../core/scenarios";

/** Default sandy coast fill when a LandShape doesn't override it. */
const COAST_FILL = 0xcdba8a;
/** Darker wet-sand / coast outline. */
const COAST_EDGE = 0x8a7546;

/**
 * (Re)builds the sea field and any scenario coastline into `parent`, clearing
 * whatever was there before (so it is safe to call on every scenario change).
 * Maps world Z → local -Y (matching Renderer.worldLocal) so land polygons given
 * in world coordinates line up with the fleet.
 */
export function buildScene(parent: Container, land?: ReadonlyArray<LandShape>): void {
  parent.removeChildren();

  const seaX = Config.ArenaHalfX * 2.6;
  const seaZ = Config.ArenaHalfZ * 2.6;
  const sea = new Graphics();
  sea.rect(-seaX / 2, -seaZ / 2, seaX, seaZ).fill({ color: 0x6ba8db }); // (0.42,0.66,0.86)
  parent.addChild(sea);

  if (land && land.length > 0) {
    const g = new Graphics();
    for (const shape of land) {
      if (shape.polygon.length < 3) continue;
      const fill = shape.fill ?? COAST_FILL;
      const pts = shape.polygon.map((p) => ({ x: p.x, y: -p.z }));

      // Filled landmass.
      g.moveTo(pts[0].x, pts[0].y);
      for (let i = 1; i < pts.length; i++) g.lineTo(pts[i].x, pts[i].y);
      g.closePath().fill({ color: fill, alpha: 1 });

      // A soft surf/shore line traced along the seaward coast edge for definition.
      g.moveTo(pts[0].x, pts[0].y);
      for (let i = 1; i < pts.length; i++) g.lineTo(pts[i].x, pts[i].y);
      g.closePath().stroke({ width: 1.4 * Config.ShipScale, color: COAST_EDGE, alpha: 0.85 });
    }
    parent.addChild(g);
  }
}

/** Back-compat: build just the sea (no land). */
export function buildSea(parent: Container): void {
  buildScene(parent);
}
