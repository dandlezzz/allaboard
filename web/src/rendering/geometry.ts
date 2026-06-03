// Procedural ship geometry — a port of the hull-shape parts of Unity
// `Rendering/MeshUtil.cs`. Produces outlines in the ship's LOCAL world units
// (bow toward +Z), which the PixiJS view maps to its local draw space by
// flipping Z → -Y (so +Z reads as "up"/forward on screen).

import { lerp, smoothStep } from "../core/mathf";
import type { Vec2 } from "../core/vec";

/**
 * Normalised half-beam (0..1) at longitudinal position `u` (0 = stern, 1 = bow):
 * a narrow flat transom, max beam a little aft of midships, and a long fine
 * entry tapering to a sharp point at the bow.
 */
export function hullHalfWidth(u: number): number {
  const transom = 0.5;
  const widestAt = 0.42;

  if (u <= widestAt) {
    const t = smoothStep(0, 1, u / widestAt);
    return lerp(transom, 1, t);
  }

  const k = (u - widestAt) / (1 - widestAt);
  return Math.pow(Math.cos(k * Math.PI * 0.5), 1.35);
}

/** Closed top-down hull outline (bow → starboard → transom → port). */
export function hullOutline(length: number, beam: number): Vec2[] {
  const hl = length * 0.5;
  const hb = beam * 0.5;
  const stations = 13;
  const outline: Vec2[] = [];

  // Starboard side: bow (u=1) down to stern (u=0).
  for (let i = 0; i < stations; i++) {
    const u = 1 - i / (stations - 1);
    const z = lerp(-hl, hl, u);
    outline.push({ x: hb * hullHalfWidth(u), z });
  }

  // Port side: stern corner (u=0) up to just below the bow.
  for (let i = 0; i < stations - 1; i++) {
    const u = i / (stations - 1);
    const z = lerp(-hl, hl, u);
    outline.push({ x: -hb * hullHalfWidth(u), z });
  }

  return outline;
}

/** Point on the hull gunwale at longitudinal `u` for `side` (+1 stbd, -1 port). */
export function hullEdgePoint(length: number, beam: number, u: number, side: number): Vec2 {
  return { x: side * (beam * 0.5) * hullHalfWidth(u), z: lerp(-length * 0.5, length * 0.5, u) };
}

/** Flattens a local-space outline to a PixiJS polygon array, flipping Z → -Y. */
export function toLocalPoly(outline: Vec2[]): number[] {
  const flat: number[] = [];
  for (const p of outline) {
    flat.push(p.x, -p.z);
  }
  return flat;
}
